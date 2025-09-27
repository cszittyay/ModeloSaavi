module Consume

open Tipos
open Helpers

let consume (p: ConsumeParams) : Operation =
  fun stIn ->
    if stIn.location <> p.meterLocation then
      Error (sprintf "Consume: estado en %s, se esperaba %s" stIn.location p.meterLocation)
    else
      let outQ = stIn.qtyMMBtu
      let dmb  = p.measured - outQ
      let tol  = abs outQ * (p.tolerancePct / 100.0)
      let penalty =
        match p.penaltyRate with
        | Some rate when abs dmb > tol ->
            let penalQty = abs dmb - tol
            let amount = scaleMoney (decimal (float penalQty)) rate |> round2
            Some { kind="PENALTY-IMBALANCE"
                   qtyMMBtu = penalQty
                   rate = Some rate
                   amount=amount
                   currency= p.currency
                   meta= [ "desbalance", box (float dmb); "tolerancia", box (float tol) ] |> Map.ofList }
        | _ -> None
      let stOut = { stIn with qty = 0.0<mmbtu> }
      let notes =
        [ "consume.measured",   box (float p.measured)
          "consume.out",        box (float outQ)
          "consume.desbalance", box (float dmb) ] |> Map.ofList
      Ok { state=stOut; costs= penalty |> Option.toList; notes=notes }
