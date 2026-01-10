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
                    modo = modo; 
                    central = central; 
                    path = path
                    order = order; 
                    ref = refOpt
                    legNo = i + 1

                    tcId = sp.tcId
                    tradingHub = sp.tradingHub
                    temporalidad = sp.temporalidad
                    deliveryPt = sp.deliveryPt
                    seller = sp.seller
                    buyer = sp.buyer
                    qty = (sp.qEnergia : Energy)
                    index = sp.index
                    adder = sp.adder
                    price = sp.price
                    contractRef = sp.contractRef }))
      )



  // -------- Trade --------
  let projectTrade (t: Transition) : Result<TradeResultRow, DomainError> =
    getCommon t >>= fun (modo, central, path, order, refOpt) ->
    Meta.require<TradeParams> "tradeParams" t.notes >>= fun p ->
    Ok {
      runId = runId
      gasDay = t.state.gasDay
      modo = modo; central = central; path = path
      order = order
      ref = refOpt

      side = p.side
      seller = p.seller
      buyer = p.buyer
      location = p.location
      adder = p.adder
      contractRef = p.contractRef
    }

  // -------- Sell --------
  let projectSell (t: Transition) : Result<SellResultRow, DomainError> =
    getCommon t >>= fun (modo, central, path, order, refOpt) ->
    Meta.require<SellParams> "sellParams" t.notes >>= fun p ->
    Ok {
      runId = runId
      gasDay = t.state.gasDay
      modo = modo; central = central; path = path
      order = order
      ref = refOpt

      location = p.location
      seller = p.seller
      buyer = p.buyer
      qty = p.qty
      price = p.price
      adder = p.adder
      contractRef = p.contractRef
    }

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
      modo = modo; central = central; path = path
      order = order
      ref = refOpt

      provider = p.provider
      pipeline = p.pipeline
      entry = p.entry
      exit = p.exit
      shipper = p.shipper
      fuelMode = p.fuelMode
      fuelPct = p.fuelPct

      qtyIn = qtyIn
      qtyOut = qtyOut
      fuelQty = fuelQty

      usageRate = p.usageRate
      reservation = p.reservation
      acaRate = p.acaRate
    }

  // -------- Sleeve --------
  let projectSleeve (t: Transition) : Result<SleeveResultRow, DomainError> =
    getCommon t >>= fun (modo, central, path, order, refOpt) ->
    Meta.require<SleeveParams> "sleeveParams" t.notes >>= fun p ->
    Meta.require<Energy> "qty" t.notes >>= fun qty ->
    Ok {
      runId = runId
      gasDay = t.state.gasDay
      modo = modo; central = central; path = path
      order = order
      ref = refOpt

      provider = p.provider
      seller = p.seller
      buyer = p.buyer
      location = p.location
      sleeveSide = p.sleeveSide

      qty = qty
      index = p.index
      price = 1.m<USD/MMBTU>  // las ventas sleeve no tienen pric e fijo
      adder = p.adder
      contractRef = p.contractRef
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
      modo = modo; central = central; path = path
      order = order
      ref = refOpt

      provider = p.provider
      meterLocation = p.meterLocation
      qtyConsume = qtyConsume
      measured = (p.measured : Energy)
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
          projectSell t <!> fun row -> { acc with sells = row :: acc.sells }

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




