module Escenario

open System
open System.Data
open FsToolkit.ErrorHandling
open Tipos              // State, Transition, DomainError, SupplierLeg, SupplyParams, CostKind, etc.
open ProjectOperations
open DbContext
open Repository.DetailRepo
open Repository.Tx
open ResultRows
open Helpers.FlowBuilderUtils
open Gnx.Persistence.SQL_Data
open FlowBuilderDB

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
      (ctx: FlowDB.Ctx)
      (conn   : IDbConnection)
      (tx     : IDbTransaction)
      (gasDay : DateOnly)
      (flowMasterId   : int)
      : Result<int, DomainError> =

    try
      // Contexto ligado a la MISMA conexión y transacción
      let fm = dFlowMaster.[flowMasterId]
      // Crear fila FlowRun
      let fr = ctx.Fm.FlowRun.Create()
      fr.GasDay  <- gasDay.ToDateTime(TimeOnly.MinValue)
      fr.Modo <- Some "ModoX"  // TODO: parámetro"
      fr.Central <- Some "CentralX" // TODO: parámetro"
      // fr.CreatedAt lo setea el DEFAULT de SQL Server

      // Persistir
      ctx.SubmitUpdates()

      // SQLProvider rellena el IDENTITY automáticamente
      Ok fr.RunId

    with ex ->
      Error (DomainError.Other ex.Message)


let persistAll
    (ctx: FlowDB.Ctx)
    (conn: IDbConnection)
    (tx  : IDbTransaction)
    (runId: int)
    (rows: ProjectedRows)
    : Result<unit, DomainError> =
  try
    // Contexto “scoped” a la transacción
    

    // IMPORTANTE: usar ctxTx para Create() y para SubmitUpdates()
    // (no el ctx global)
    addSupplyRows   ctx runId rows.supplies
    addTradeRows    ctx runId rows.trades
    addSellRows     ctx runId rows.sells
    addTransportRows ctx runId rows.transports
    addSleeveRows   ctx runId rows.sleeves
    addConsumeRows  ctx runId rows.consumes


    ctx.SubmitUpdates()
    Ok ()
  with ex ->
    Error (DomainError.Other ex.Message)





let runFlowAndPersistDB
    (connStr: string)
    (flowMasterId : int)
    (diaGas       : DateOnly)
    (initial      : State)
    : Result<int * State * Transition list, DomainError> =


  let ctx = FlowDB.createCtx connStr

  result {
    // 1) Leer paths
    let! paths = getFlowStepsDB flowMasterId diaGas

    // 2) Topología
    let! flowDef = buildFlowDef paths

    // 3) Ejecutar
    let! (finalState, transitions) = runFlow flowDef initial 0.0m<MMBTU> (+) runSteps

    // 4) Persistir
    let! runId, finalState, transitions =
      withTransaction (fun conn tx ->
        result {
          let! runId = FlowRunRepo.insertAndGetRunId ctx conn tx diaGas flowMasterId
          let! rows = projectRows runId transitions
          do! persistAll ctx conn tx runId rows
          return (runId, finalState, transitions)
        })

    return (runId, finalState, transitions)
  }

