module Escenario

open System
open System.Data
open Tipos              // State, Transition, DomainError, SupplierLeg, SupplyParams, CostKind, etc.
open ProjectOperations
open DbContext
open Repository.DetailRepo
open Repository.Tx
open ResultRows
open Helpers.FlowBuilderUtils
open FlowBuilderDB
open Gnx.Persistence.SQL_Data

/// Helpers de impresión (opcionales)
let private printMoney (m: Money) = (decimal m).ToString("0.#####")
let private printRate (r: EnergyPrice) = (decimal r).ToString("0.#####")
let private printQty  (q: Energy) = (decimal q).ToString("0.#####")


 // Datos comunes del día y hub
let gasDay     = DateOnly(2026, 1, 20)
let entryPt = Location "AguaDulce" //  Location "Ramones" // Location "Ehremberg"
let buyer      = "EAVIII"  // Datos comunes del día y hub

// Estado inicial
let st0 : State =
    {   energy = 0.0m<MMBTU>
        owner    = buyer
        ownerId  = 1001
        transactionId =  0
        location = entryPt
        locationId = 501
        gasDay   = gasDay
        meta     = Map.empty }



module FlowRunRepo =
  open Gnx.Persistence.SQL_Data
  
  let insertAndGetRunId
      (conn   : IDbConnection)
      (tx     : IDbTransaction)
      (gasDay : DateOnly)
      (flowMasterId   : int)
      (central: string)
      : Result<int, DomainError> =

    try
      // Contexto ligado a la MISMA conexión y transacción
      let fm = dFlowMaster.[flowMasterId]
      // Crear fila FlowRun
      let fr = ctx.Fm.FlowRun.Create()
      fr.GasDay  <- gasDay.ToDateTime(TimeOnly.MinValue)
      fr.Modo    <- fm.Codigo
      fr.Central <- central
      // fr.CreatedAt lo setea el DEFAULT de SQL Server

      // Persistir
      ctx.SubmitUpdates()

      // SQLProvider rellena el IDENTITY automáticamente
      Ok fr.RunId

    with ex ->
      Error (DomainError.Other ex.Message)


let persistAll
    (conn: IDbConnection)
    (tx  : IDbTransaction)
    (runId: int)
    (rows: ProjectedRows)
    : Result<unit, DomainError> =
  try
    // Contexto “scoped” a la transacción
    

    // IMPORTANTE: usar ctxTx para Create() y para SubmitUpdates()
    // (no el ctx global)
    addSupplyRows   runId rows.supplies
    addTradeRows    runId rows.trades
    addSellRows     runId rows.sells
    addTransportRows runId rows.transports
    addSleeveRows   runId rows.sleeves
    addConsumeRows  runId rows.consumes

    ctx.SubmitUpdates()
    Ok ()
  with ex ->
    Error (DomainError.Other ex.Message)





let runrFlowAndPersistDB
    (flowMasterId : int)
    (path   : string)
    (diaGas    : DateOnly)
    (initial   : State)
    : Result<int * State * Transition list, DomainError> =

  // 1) Leer Excel -> paths
  let paths : Map<FlowId, FlowStep list> = getFlowStepsDB   flowMasterId path diaGas 

  // 2) Topología (Linear/Join) + PathRole
  buildFlowDef paths
  |> Result.bind (fun flowDef ->

      // 3) Ejecutar Flow (puro) -> transitions
      runFlow flowDef initial 0.0m<MMBTU> (+) runSteps
      |> Result.bind (fun (finalState, transitions) ->

          // 4) Persistir en 1 transacción
          withTransaction  (fun conn tx ->

              // 4.1) Insert header FlowRun -> RunId (IDENTITY)
              FlowRunRepo.insertAndGetRunId conn tx diaGas flowMasterId path
              |> Result.bind (fun runId ->

                  // 4.2) Proyección post-ejecución
                  projectRows runId transitions
                  |> Result.bind (fun rows ->

                      // 4.3) Inserts detalle (SQLProvider) dentro de conn/tx
                      persistAll conn tx runId rows
                      |> Result.map (fun () ->
                          // devolvemos runId + resultados en memoria (útil para logs/UI)
                          (runId, finalState, transitions)
                      )
                  )
              )
          )
      )
  )
