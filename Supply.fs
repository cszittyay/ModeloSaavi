module Supply

open System
open Unidades
open Tipos
open Helpers


/// Compra simple (un solo supplier) a partir de una TransactionConfirmation
let supplyFromTc (tc: TransactionConfirmation) : Operation =
  fun stIn ->
    if tc.qtyMMBtu <= 0.0m<MMBTU> then Error (Other "Supply: qtyMMBtu <= 0")
    else
      // La compra aumenta posición del buyer; si tu State ya trae qty, podés sumar:
      let stOut =
        { stIn with
            owner    = tc.buyer
            contract = tc.contractRef
            // si tu State tiene 'qtyMMBtu' como la cantidad del paso, podés usar la qty confirmada:
            qtyMMBtu = tc.qtyMMBtu
            // si tu State tiene 'location' (no visto en el recorte), setear a tc.deliveryPt si aplica
        }

      let cost =
        let amt = Domain.amount tc.qtyMMBtu tc.price
        [{ kind   = Gas              // si migraste a DU: CostKind.Gas
           qtyMMBtu = tc.qtyMMBtu
           rate  = tc.price            // si usás RateGas: tipar acorde a tu CostLine
           amount= amt
           provider = tc.seller
           meta  = [ "seller", box tc.seller
                     "tcId"  , box tc.tcId
                     "gasDay", box tc.gasDay ] |> Map.ofList }]

      Ok { state=stOut
           costs=cost
           notes= [ "op", box "supply"
                    "seller", box tc.seller
                    "buyer", box tc.buyer   
                    "deliveryPt", box tc.deliveryPt ] |> Map.ofList }


let supplyMany (legs: SupplierLeg list) : Operation =
  fun stIn ->
    match Validate.legsConsolidados legs with
    | Error e ->
        // mapear el error de validación a string (o a tu DomainError si usás DU)
        Error (Other (Validate.toString e))

    | Ok (buyer, gasDay, deliveryPt) ->
        let totalQty = legs |> List.sumBy (fun l -> l.tc.qtyMMBtu)
        if totalQty <= 0.0m<MMBTU> then
          Error (QuantityNonPositive "SupplyMany: qty total <= 0")
        else
          // nuevo estado consolidando la compra multi-supplier
          let stOut =
            { stIn with
                owner    = buyer
                contract = "MULTI"          // o stIn.contract; o acumular en notes
                qtyMMBtu = totalQty
                location = deliveryPt
                gasDay   = gasDay
            }

          // costos por cada leg (mantiene trazabilidad por seller/contract/tcId)
          let costs =
            legs
            |> List.map (fun l ->
                let amt : Money = l.tc.qtyMMBtu * (l.tc.price + l.tc.adder)
                { provider = l.tc.seller
                  kind     = CostKind.Gas
                  qtyMMBtu = l.tc.qtyMMBtu
                  rate     = l.tc.price              // RateGas ($/MMBtu) si lo definiste así
                  amount   = amt
                  meta     = [ "cycle"   , box l.tc.cicle
                               "tradingHub",box l.tc.tradingHub
                               "adder"    , box l.tc.adder ] |> Map.ofList })

          // precio promedio ponderado (opcional en notes)
          let amtSum : Money = costs |> List.sumBy (fun c -> c.amount)
          let wavg = amtSum / totalQty

          Ok {
            state = stOut
            costs = costs
            notes = [ "op"        , box "supplyMany"
                      "buyer"     , box buyer
                      "gasDay"    , box gasDay
                      "deliveryPt", box deliveryPt
                      "wavgPrice:[USD/MMBTU]" , box (Math.Round(decimal wavg, 2))
                      "legsCount" , box legs.Length ] |> Map.ofList
          }
