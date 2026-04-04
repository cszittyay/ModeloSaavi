module Escenario

open System
open System.Data
open FsToolkit.ErrorHandling
open Tipos
open ProjectOperations
open DbContext
open Repository.DetailRepo
open Repository.Tx
open ResultRows
open Helpers.FlowBuilderUtils
open Gnx.Persistence.SQL_Data
open FlowBuilderDB

let private printMoney (m: Money) = (decimal m).ToString("0.#####")
let private printRate (r: EnergyPrice) = (decimal r).ToString("0.#####")
let private printQty  (q: Energy) = (decimal q).ToString("0.#####")




let buildInitialFlowState gasDay: State =
    { energy        = 0.0m<MMBTU>
      owner         = "EAX"
      ownerId       = 1001
      transactionId = 0
      location      = Location "AguaDulce"
      locationId    = 501
      gasDay        = gasDay
      meta          = Map.empty }

let buildInitialByFlow
    (flowMasterIds: int list)
    (gasDay: DateOnly)
    : Map<int, State> =

    let initialState = buildInitialFlowState gasDay

    flowMasterIds
    |> List.map (fun flowMasterId -> flowMasterId, initialState)
    |> Map.ofList

type SharedTransportContext = {
    Pools : Map<int, CapacityPool>
}

type FlowBatchItemResult = {
    RunId : int
}


  

let getFlowMasterIdsByPlanta
        (idPlanta: int)
        (gasDay: DateOnly)
        : Result<int list, DomainError> =
        try
            let dtGasDay = gasDay.ToDateTime(TimeOnly.MinValue)

            let flowMasterIds =
                query {
                    for fm in ctx.Fm.FlowMaster do
                    join k in ctx.Dbo.Cliente on (fm.IdCliente = k.IdCliente)
                    join p in ctx.Dbo.Punto on (k.IdPunto.Value = p.IdPunto)
                    where (
                        p.IdPlanta .Value= idPlanta
                        && fm.VigenciaDesde <= dtGasDay
                        && dtGasDay <= fm.VigenciaHasta
                    )
                    // primero los clientes externos (0)
                    sortBy k.Interno
                    select fm.IdFlowMaster
                }
                |> Seq.toList

            Ok flowMasterIds

        with ex ->
            Error (Other ex.Message)


let initSharedTransportContext (gasDay: DateOnly) : Result<SharedTransportContext, DomainError> =
    try
        let pools = buildCapacityPoolsFromTransactions gasDay
        Ok { Pools = pools }
    with ex ->
        Error (Other ex.Message)

let mkTryGetPool (ctx: SharedTransportContext) : TryGetCapacityPool =
    fun tfId ->
        match Map.tryFind tfId ctx.Pools with
        | Some pool -> Ok pool
        | None -> Error (Other $"No existe pool para TF={tfId}")





module FlowRunRepo =
    open Gnx.Persistence.SQL_Data

    let insertAndGetRunId
        (conn   : IDbConnection)
        (tx     : IDbTransaction)
        (gasDay : DateOnly)
        (flowMasterId : int)
        : Result<int, DomainError> =

        try
            let fm = dFlowMaster.[flowMasterId]
            let fr = ctx.Fm.FlowRun.Create()
            fr.GasDay <- gasDay.ToDateTime(TimeOnly.MinValue)
            fr.Modo <- fm.Nombre.Value
            fr.Central <- fm.Codigo
            ctx.SubmitUpdates()
            Ok fr.RunId
        with ex ->
            Error (DomainError.Other ex.Message)

    let persistAll
        (conn : IDbConnection)
        (tx   : IDbTransaction)
        (runId: int)
        (rows : ProjectedRows)
        : Result<unit, DomainError> =
      try
        addSupplyRows    runId rows.supplies
        addTradeRows     runId rows.trades
        addSellRows      runId rows.sells
        addTransportRows runId rows.transports
        addSleeveRows    runId rows.sleeves
        addConsumeRows   runId rows.consumes
    
        ctx.SubmitUpdates()
        Ok ()
      with ex ->
        Error (DomainError.Other ex.Message)
    
    let runFlowAndPersistDB
        (tryGetPool  : TryGetCapacityPool)
        (flowMasterId: int)
        (diaGas      : DateOnly)
        (initial     : State)
        : Result<int * State * Transition list, DomainError> =
    
      result {
        let! paths = getFlowStepsDB flowMasterId diaGas
        let! flowDef = buildFlowDef paths
    
        let stepRunner = runSteps tryGetPool
    
        let! (finalState, transitions) =
          runFlow flowDef initial 0.0m<MMBTU> (+) stepRunner
    
        let! runId, finalState, transitions =
          withTransaction (fun conn tx ->
            result {
              let! runId = insertAndGetRunId conn tx diaGas flowMasterId
              let! rows = projectRows runId transitions
              do! persistAll conn tx runId rows
              return (runId, finalState, transitions)
            })
    
        return (runId, finalState, transitions)
      }
    
    let runFlowsAndPersistDB
        (flowMasterIds : int list)
        (diaGas        : DateOnly)
        (initialByFlow : Map<int, State>)
        : Result<FlowBatchItemResult list, DomainError> =
    
        result {
            let! sharedTransportCtx = initSharedTransportContext diaGas
            let tryGetPool = mkTryGetPool sharedTransportCtx
    
            let mutable resultsRev : FlowBatchItemResult list = []
    
            for flowMasterId in flowMasterIds do
                let! currentInitial =
                    match Map.tryFind flowMasterId initialByFlow with
                    | Some st -> Ok st
                    | None -> Error (Other $"No existe initial state para FlowMasterId={flowMasterId}")
    
                let! runId, finalState, transitions =
                    runFlowAndPersistDB tryGetPool flowMasterId diaGas currentInitial
    
                let item =
                    {RunId = runId}
    
                resultsRev <- item :: resultsRev
    
            return List.rev resultsRev
        }
    let runFlowsAndPersistDBByPlanta
        (idPlanta: int)
        (gasDay: DateOnly)
        : Result<FlowBatchItemResult list, DomainError> =
    
        result {
            let! flowMasterIds =
                getFlowMasterIdsByPlanta idPlanta gasDay
    
            if List.isEmpty flowMasterIds then
                return! Error (Other $"No se encontraron FlowMaster vigentes para IdPlanta={idPlanta} en GasDay={gasDay}")
    
            let initialByFlow =
                buildInitialByFlow flowMasterIds gasDay
    
            return! runFlowsAndPersistDB flowMasterIds gasDay initialByFlow
        }
    
    
    let runFlowBatchIdsByPlanta
        (idPlanta: int)
        (gasDay: DateOnly)
        : Result<int list, DomainError> =
    
        result {
            let! results = runFlowsAndPersistDBByPlanta idPlanta gasDay
            return results |> List.map (fun x -> x.RunId)
        }