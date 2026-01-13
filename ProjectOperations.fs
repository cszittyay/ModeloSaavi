module ProjectOperations
open System
open Tipos
open ResultRows
open Helpers


let projectRows (runId: int) (ts: Transition list) : Result<ProjectedRows, DomainError> =
  let inline (>>=) r f = Result.bind f r
  let inline (<!>) r f = Result.map f r

  let empty = {
    supplies = []; trades = []; sells = []
    transports = []; sleeves = []; consumes = []
  }

  let getCommon (t: Transition) =
    Meta.require<string> "modo" t.notes >>= fun modo ->
    Meta.require<string> "central" t.notes >>= fun central ->
    Meta.require<string> "path" t.notes >>= fun path ->
    Meta.require<int>    "order" t.notes >>= fun order ->
    let refOpt = Meta.tryGet<string> "ref" t.notes
    Ok (modo, central, path, order, refOpt)

  // -------- Supply --------
  let projectSupplyRows (runId:int) (t: Transition) : Result<SupplyResultRow list, DomainError> =
      getCommon t
      |> Result.bind (fun (modo, central, path, order, refOpt) ->
          Meta.require<SupplyParams list> "supplyParamsMany" t.notes
          |> Result.map (fun sps ->
              sps
              |> List.mapi (fun i sp ->
                  { runId = runId
                    gasDay = t.state.gasDay
                    buyBack = false
                    transactionId = sp.transactionId
                    flowDetailId = 1 + i // placeholder
                    temporalidad = sp.temporalidad
                    seller = sp.seller
                    qty = (sp.qEnergia : Energy)
                    adder = sp.adder
                    // TODO: Priece
                    index =  0.0m<USD/MMBTU> // sp.index
                    price = sp.price}))
      )



  // -------- Trade --------
  let projectTrade (t: Transition) : Result<TradeResultRow, DomainError> =
        getCommon t >>= fun (modo, central, path, order, refOpt) ->
        Meta.require<TradeParams> "tradeParams" t.notes >>= fun p ->
        Ok {
          runId = runId
          gasDay = t.state.gasDay
          transactionId = p.transactionId
          flowDetailId = 1 // placeholder
          sellerId = p.sellerId
          buyerId = p.buyerId
          locationId = p.locationId
          qty = t.state.energy
          adder =  p.adder
          price = p.price
        }

  // -------- Sell --------
  let projectSell (runId:int) (t: Transition) : Result<SellResultRow, DomainError> =
        getCommon t >>= fun (modo, central, path, order, refOpt) ->
        Meta.require<SellParams> "sellParams" t.notes >>= fun p ->
        Ok {
          ventaGasId = p.idVentaGas
          runId = runId
          gasDay = t.state.gasDay
          flowDetailId = p.flowDetailId
          qty = p.qty
          price = p.price
          adder = p.adder
          locationId = t.state.locationId
          sellerId = p.sellerId
          buyerId = p.buyerId
        }

  let projectSellRows (runId:int) (t: Transition) : Result<SellResultRow list, DomainError> =
      getCommon t
      |> Result.bind (fun (modo, central, path, order, refOpt) ->
          Meta.require<SellParams list> "sellParamsMany" t.notes
          |> Result.map (fun sps ->
              sps
              |> List.mapi (fun i sp ->
                  { 
                  ventaGasId = sp.idVentaGas
                  runId = runId
                  flowDetailId = sp.flowDetailId
                  gasDay = t.state.gasDay
                  locationId = sp.locationId
                  sellerId = sp.sellerId
                  buyerId = sp.buyerId
                  qty = sp.qty
                  price = sp.price
                  adder = sp.adder})
          )
      )


  // -------- Transport --------
  let projectTransport (t: Transition) : Result<TransportResultRow, DomainError> =
    getCommon t >>= fun (modo, central, path, order, refOpt) ->
    Meta.require<TransportParams> "transportParams" t.notes >>= fun p ->
    Meta.require<Energy> "qtyIn"   t.notes >>= fun qtyIn ->
    Meta.require<Energy> "qtyOut"  t.notes >>= fun qtyOut ->
    Meta.require<Energy> "fuelQty" t.notes >>= fun fuelQty ->
    Ok {
      runId = runId
      gasDay = t.state.gasDay
      flowDetailId = p.flowDetailId
      transactionId = p.transactionId
      routeId = p.routeId
      pipeline = p.pipeline
      fuelMode = p.fuelMode

      qtyIn = qtyIn
      qtyOut = qtyOut
      fuelQty = fuelQty

    }

  // -------- Sleeve --------
  let projectSleeve (t: Transition) : Result<SleeveResultRow, DomainError> =
    getCommon t >>= fun (modo, central, path, order, refOpt) ->
    Meta.require<SleeveParams> "sleeveParams" t.notes >>= fun p ->
    Meta.require<Energy> "qty" t.notes >>= fun qty ->
    Ok {
      runId = runId
      gasDay = t.state.gasDay
      flowDetailId = p.flowDetailId
      transactionId = p.transactionId
      locationId = p.locationId
      // TODO: index price from index ref
      indexPrice = 0.0m<USD/MMBTU>  // placeholder
      sleeveSide = p.sleeveSide

      qty = qty
      price = 1.m<USD/MMBTU>  // las ventas sleeve no tienen pric e fijo
      adder = p.adder
    }

  // -------- Consume --------
  let projectConsume (t: Transition) : Result<ConsumeResultRow, DomainError> =
    getCommon t >>= fun (modo, central, path, order, refOpt) ->
    Meta.require<ConsumeParams> "consumeParams" t.notes >>= fun p ->
    Meta.require<Energy> "qtyConsume" t.notes >>= fun qtyConsume ->

    let imbalance =
      Meta.tryGet<Energy> "imbalance" t.notes
      |> Option.defaultValue 0.0m<MMBTU>

    let penaltyAmount =
      Meta.tryGet<Money> "penaltyAmount" t.notes

    Ok {
      runId = runId
      gasDay = t.state.gasDay
      flowDetailId = p.flowDetailId
      // TODO: ProviderId from some meta or param
      providerId = 0 // placeholder
      locationId = p.locationId
      qtyAsigned = qtyConsume
    }



  // -------- Fold principal --------
  let folder (accR: Result<ProjectedRows, DomainError>) (t: Transition) =
    accR >>= fun acc ->
      match Meta.tryGet<string> "op" t.notes with
      | Some "supplyMany" ->
         projectSupplyRows runId t
             |> Result.map (fun rows -> { acc with supplies = rows @ acc.supplies })

      | Some "trade" ->
          projectTrade t <!> fun row -> { acc with trades = row :: acc.trades }

      | Some "sell" ->
         projectSell runId t <!> fun row -> { acc with sells = row :: acc.sells }

      | Some "sellMany" ->
         projectSellRows runId t
             |> Result.map (fun rows -> { acc with sells = rows @ acc.sells })


      | Some "transport" ->
          projectTransport t <!> fun row -> { acc with transports = row :: acc.transports }

      | Some "sleeve" ->
          projectSleeve t <!> fun row -> { acc with sleeves = row :: acc.sleeves }

      | Some "consume" ->
          projectConsume t <!> fun row -> { acc with consumes = row :: acc.consumes }

      | _ ->  Ok acc

  ts
  |> List.fold folder (Ok empty)
  |> Result.map (fun r ->
      { supplies   = List.rev r.supplies
        trades     = List.rev r.trades
        sells      = List.rev r.sells
        transports = List.rev r.transports
        sleeves    = List.rev r.sleeves
        consumes   = List.rev r.consumes })




