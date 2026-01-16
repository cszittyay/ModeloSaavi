module Repository
open System.Data
open Tipos
open DbContext
open Helpers

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
  let inline priceToDecimal (p: EnergyPrice) : decimal = decimal p

  // ============ SUPPLY ============
  let addSupplyRows (runId:int) (rows: SupplyResultRow list) =
    rows |> List.iter (fun r ->
      let row = ctx.Fm.FlowSupplyResult.Create()
      row.RunId   <- runId
      row.GasDay  <- r.gasDay.ToDateTime(TimeOnly.MinValue)
      row.IdFlowDetail <- r.flowDetailId
      row.IdTransaccion <- r.transactionId 
      row.Temporalidad <- string r.temporalidad
      row.BuyBack  <- r.buyBack
      row.QtyMmBtu  <- energyToDecimal r.qty
      row.IndexPrice <- Some (priceToDecimal r.index)
      row.Adder     <- Some (priceToDecimal r.adder)
    )

  // ============ TRADE ============
  let addTradeRows (runId:int) (rows: TradeResultRow list) =
    rows |> List.iter (fun r ->
      let row =  ctx.Fm.FlowTradeResult.Create()
      row.RunId   <- runId
      row.GasDay  <- do2dt r.gasDay
      row.IdFlowDetail <- r.flowDetailId
      row.IdTransaccion <- r.transactionId
      row.IdSeller      <- r.sellerId
      row.IdBuyer       <- r.buyerId
      row.IdLocation    <- r.locationId
      row.Adder         <- priceToDecimal r.adder
      row.QtyMmBtu      <- energyToDecimal r.qty
      row.Price <-  priceToDecimal r.price
    )

  // ============ SELL ============
  let addSellRows (runId:int) (rows: SellResultRow list) =
    rows |> List.iter (fun r ->
      let row = ctx.Fm.FlowSellResult.Create()
      row.RunId   <- runId
      row.GasDay  <- do2dt r.gasDay
      row.IdFlowDetail <- r.flowDetailId
      row.IdTransaccion <- r.transactionId
      row.IdVentaGas <- r.ventaGasId
      row.IdBuyer    <- r.buyerId
      row.IdLocation <- r.locationId
      row.IdSeller  <- r.sellerId
      row.QtyMmBtu  <- energyToDecimal r.qty
      row.Price     <- priceToDecimal r.price
      row.Adder <- priceToDecimal r.adder
      
    )

  // ============ TRANSPORT ============
  let addTransportRows (runId:int) (rows: TransportResultRow list) =
    rows |> List.iter (fun r ->
      let row = ctx.Fm.FlowTransportResult.Create()
      row.RunId   <- runId
      row.GasDay  <- do2dt r.gasDay
      row.IdFlowDetail <- r.flowDetailId
      row.IdProvider  <- r.providerId
      row.IdTransaccion <- r.transactionId
      row.Pipeline <- string r.pipeline
      row.IdRuta       <- r.routeId
      row.FuelQtyMmBtu <- energyToDecimal r.fuelQty
      row.QtyInMmBtu   <- energyToDecimal r.qtyIn
      row.QtyOutMmBtu  <- energyToDecimal r.qtyOut

    )
  // ============ SLEEVE ============
  let addSleeveRows (runId:int) (rows: SleeveResultRow list) =
    rows |> List.iter (fun r ->
      let row = ctx.Fm.FlowSleeveResult.Create()
      row.RunId   <- runId
      row.GasDay  <- r.gasDay.ToDateTime(TimeOnly.MinValue)
      row.IdFlowDetail <- r.flowDetailId
      row.IdTransaccion <- r.transactionId
      row.IdLocation    <- r.locationId
      row.Adder         <- priceToDecimal r.adder
      row.SleeveSide    <- string r.sleeveSide
      row.QtyMmBtu      <- energyToDecimal r.qty
      row.Price         <- priceToDecimal r.price   
      row.IndexPrice    <- priceToDecimal r.indexPrice
    )


  // ============ CONSUME ============
  let addConsumeRows (runId:int) (rows: ConsumeResultRow list) =
    rows |> List.iter (fun r ->
      let row = ctx.Fm.FlowConsumeResult.Create()
      row.RunId   <- runId
      row.GasDay  <- do2dt r.gasDay
      row.IdFlowDetail <- r.flowDetailId
      row.AsignadoMmBtu <- energyToDecimal r.qtyAsigned
      row.IdPunto  <- r.locationId
      row.IdProvider <- r.providerId    
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
