module Supply

open System
open Unidades
open Tipos
open Helpers


/// Compra simple (un solo supplier) a partir de una TransactionConfirmation
let supply (sp: SupplyParams) : Operation =
  fun stIn ->
      // La compra aumenta posición del buyer; si tu State ya trae qty, podés sumar:
      let stOut =
        { stIn with
            owner    = sp.buyer
            contract = sp.contractRef
            energy   = sp.qEnergia
            location = sp.deliveryPt }

      let amt = Domain.amount sp.qEnergia sp.price
      let cost =
        [{ kind     = CostKind.Gas
           qEnergia = sp.qEnergia
           rate     = sp.price
           amount   = amt
           provider = sp.seller
           meta     = [ "seller", box sp.seller
                        "tcId"  , box sp.tcId
                        "gasDay", box sp.gasDay ] |> Map.ofList }]

      Ok { state = stOut
           costs = cost
           notes = [ "op", box "supply"
                     "seller", box sp.seller
                     "buyer", box sp.buyer
                     "deliveryPt", box sp.deliveryPt ] |> Map.ofList }


/// Compra multi-supplier, consolidando en una sola Operation
let supplyMany (sps: SupplyParams list) : Operation =
  fun stIn ->
    match Validate.legsConsolidados sps with
    | Error e -> Error (Other (Validate.toString e))
    | Ok (buyer, gasDay, deliveryPt) ->
      let totalQty = sps |> List.sumBy (fun sp -> sp.qEnergia)
      if totalQty <= 0.0m<MMBTU> then Error (QuantityNonPositive "SupplyMany: qty total <= 0")
      else
        // nuevo estado consolidando la compra multi-supplier
        let stOut =
          { stIn with
              owner    = buyer
              contract = "MULTI"
              energy   = totalQty
              location = deliveryPt
              gasDay   = gasDay }

        // costos por cada leg (mantiene trazabilidad por seller/contract/tcId)
        let costs =
          sps
          |> List.map (fun sp ->
              let amt : Money = sp.qEnergia * (sp.price + sp.adder)
              { provider = sp.seller
                kind     = CostKind.Gas
                qEnergia = sp.qEnergia
                rate     = sp.price
                amount   = amt
                meta     = [ "cycle"     , box sp.temporalidad
                             "tradingHub", box sp.tradingHub
                             "adder"    , box sp.adder ] |> Map.ofList })

        // precio promedio ponderado (opcional en notes)
        let amtSum : Money = costs |> List.sumBy (fun c -> c.amount)
        let wavg = if totalQty > 0.0m<MMBTU> then amtSum / totalQty else 0.0m<USD/MMBTU>

        Ok { state = stOut
             costs = costs
             notes = [ "op"        , box "supplyMany"
                       "buyer"     , box buyer
                       "gasDay"    , box gasDay
                       "deliveryPt", box deliveryPt
                       "wavgPrice:[USD/MMBTU]" , box (Math.Round(decimal wavg, 2))
                       "legsCount" , box sps.Length ] |> Map.ofList }
