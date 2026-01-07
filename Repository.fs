module Repository
open System.Data
open Tipos
open DbContext

module Tx =

  let withTransaction<'T>
      (f: IDbConnection -> IDbTransaction -> Result<'T, DomainError>)
      : Result<'T, DomainError> =

    use conn = ctx.CreateConnection()
    conn.Open()
    use tx = conn.BeginTransaction()

    try
      match f conn tx with
      | Ok x ->
          tx.Commit()
          Ok x
      | Error e ->
          tx.Rollback()
          Error e
    with ex ->
      try tx.Rollback() with _ -> ()
      Error (DomainError.Other ex.Message)


module DetailRepo =
  open System
  open Tipos
  open ResultRows
  open ProjectOperations
  open DbContext

  let private err msg = Error (DomainError.Other msg)

  // Helpers para mapear Energy (decimal<MMBTU>) a decimal
  let inline energyToDecimal (e: Energy) : decimal = decimal e

  // ============ SUPPLY ============
  let addSupplyRows (runId:int) (rows: SupplyResultRow list) =
    rows |> List.iter (fun r ->
      let row = ctx.Fm.FlowSupplyResult.Create()
      row.RunId   <- runId
      row.GasDay  <- r.gasDay.ToDateTime(TimeOnly.MinValue)
      row.Modo    <- r.modo
      row.Central <- r.central
      row.Path    <- r.path
      row.Order   <- r.order
      row.Ref     <- r.ref
      row.LegNo   <- r.legNo
      row.TcId        <- r.tcId
      row.TradingHub  <- string r.tradingHub
      row.Temporalidad<- string r.temporalidad
      row.DeliveryPt  <- string r.deliveryPt
      row.Seller      <- r.seller
      row.Buyer       <- r.buyer
      row.QtyMmBtu    <- energyToDecimal r.qty
      row.IndexPrice  <- decimal r.index
      row.Adder       <- decimal r.adder
      row.Price       <- decimal r.price
      row.ContractRef <- r.contractRef
    )

  // ============ TRADE ============
  let addTradeRows (runId:int) (rows: TradeResultRow list) =
    rows |> List.iter (fun r ->
      let row =  ctx.Fm.FlowTradeResult.Create()
      row.RunId   <- runId
      row.GasDay  <- r.gasDay.ToDateTime(TimeOnly.MinValue)
      row.Modo    <- r.modo
      row.Central <- r.central
      row.Path    <- r.path
      row.Order   <- r.order
      row.Ref     <- r.ref

      row.Side        <- string r.side
      row.Seller      <- string r.seller
      row.Buyer       <- string r.buyer
      row.Location    <- string r.location
      row.Adder       <- decimal r.adder
      row.ContractRef <- string r.contractRef
    )

  // ============ SELL ============
  let addSellRows (runId:int) (rows: SellResultRow list) =
    rows |> List.iter (fun r ->
      let row = ctx.Fm.FlowSellResult.Create()
      row.RunId   <- runId
      row.GasDay  <- r.gasDay.ToDateTime(TimeOnly.MinValue)
      row.Modo    <- r.modo
      row.Central <- r.central
      row.Path    <- r.path
      row.Order   <- r.order
      row.Ref     <- r.ref

      row.Location    <- string r.location
      row.Seller      <- string r.seller
      row.Buyer       <- string r.buyer
      row.QtyMmBtu    <- energyToDecimal r.qty
      row.Price       <- decimal r.price
      row.Adder       <- decimal r.adder
      row.ContractRef <- string r.contractRef
    )

  // ============ TRANSPORT ============
  let addTransportRows (runId:int) (rows: TransportResultRow list) =
    rows |> List.iter (fun r ->
      let row = ctx.Fm.FlowTransportResult.Create()
      row.RunId   <- runId
      row.GasDay  <- r.gasDay.ToDateTime(TimeOnly.MinValue)
      row.Modo    <- r.modo
      row.Central <- r.central
      row.Path    <- r.path
      row.Order   <- r.order
      row.Ref     <- r.ref

      row.Provider <- string r.provider
      row.Pipeline <- string r.pipeline
      row.EntryLoc <- string r.entry
      row.ExitLoc  <- string r.exit
      row.Shipper  <- string r.shipper
      row.FuelMode <- string r.fuelMode
      row.FuelPct  <- r.fuelPct

      row.QtyInMmBtu    <- energyToDecimal r.qtyIn
      row.QtyOutMmBtu   <- energyToDecimal r.qtyOut
      row.FuelQtyMmBtu  <- energyToDecimal r.fuelQty

      row.UsageRate   <- decimal r.usageRate
      row.Reservation <- decimal r.reservation
      row.AcaRate     <- decimal r.acaRate
    )

  // ============ SLEEVE ============
  let addSleeveRows (runId:int) (rows: SleeveResultRow list) =
    rows |> List.iter (fun r ->
      let row = ctx.Fm.FlowSleeveResult.Create()
      row.RunId   <- runId
      row.GasDay  <- r.gasDay.ToDateTime(TimeOnly.MinValue)
      row.Modo    <- r.modo
      row.Central <- r.central
      row.Path    <- r.path
      row.Order   <- r.order
      row.Ref     <- r.ref

      row.Provider   <- string r.provider
      row.Seller     <- string r.seller
      row.Buyer      <- string r.buyer
      row.Location   <- string r.location
      row.SleeveSide <- string r.sleeveSide

      row.QtyMmBtu   <- energyToDecimal r.qty
      row.IndexPrice <- decimal r.index
      row.Adder      <- decimal r.adder
      row.ContractRef<- string r.contractRef
    )

  // ============ CONSUME ============
  let addConsumeRows (runId:int) (rows: ConsumeResultRow list) =
    rows |> List.iter (fun r ->
      let row = ctx.Fm.FlowConsumeResult.Create()
      row.RunId   <- runId
      row.GasDay  <- r.gasDay.ToDateTime(TimeOnly.MinValue)
      row.Modo    <- r.modo
      row.Central <- r.central
      row.Path    <- r.path
      row.Order   <- r.order
      row.Ref     <- r.ref

      row.Provider      <- string r.provider
      row.MeterLocation <- string r.meterLocation
      row.QtyConsumeMmBtu    <- energyToDecimal r.qtyConsume
      row.MeasuredMmBtu      <- energyToDecimal r.measured
      row.ImbalanceMmBtu     <- energyToDecimal r.imbalance
      row.TolerancePct  <- r.tolerancePct
      row.PenaltyRate   <- decimal r.penaltyRate
      row.PenaltyAmount <- r.penaltyAmount |> Option.map decimal
    )

  let persistAll
      (tx: IDbTransaction)
      (runId: int)
      (rows: ProjectedRows)
      : Result<unit, DomainError> =
    try
      addSupplyRows runId rows.supplies
      addTradeRows runId rows.trades
      addSellRows runId rows.sells
      addTransportRows runId rows.transports
      addSleeveRows runId rows.sleeves
      addConsumeRows runId rows.consumes

      // SubmitUpdates usando la MISMA transacción
      ctx.SubmitUpdates()
      Ok ()
    with ex ->
      Error (DomainError.Other ex.Message)
