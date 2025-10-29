module Consume

open Tipos
open Helpers
open Unidades


//Es el consumo del punto de entrega final, donde se mide lo que realmente se entregó
let consume (p: ConsumeParams) : Operation =
  fun stIn ->
    if stIn.location <> p.meterLocation then
      Error (Other (sprintf "¨Punto de Consumo: estado en %s, se esperaba %s" stIn.location p.meterLocation))
    else
      let outQ = stIn.qtyMMBtu
      let dmb  = outQ - p.measured
      let tol  = abs outQ * (p.tolerancePct / 100.0m)

      // Calculate penalty if out of tolerance
      let penalty =
        match p.penaltyRate with
        | rate when abs dmb > tol ->
            let penalQty = abs dmb - tol
            let amount =  penalQty * rate 
            Some { kind=Fee
                   qtyMMBtu = penalQty
                   rate =  rate
                   amount =  amount
                   meta= [ "desbalance", box (decimal dmb); "tolerancia", box (decimal tol) ] |> Map.ofList }
        | _ -> None
      
      let stOut = { stIn with qtyMMBtu = 0.0m<MMBTU> }
      let notes =
        [ "consume.measured",   box (decimal p.measured)
          "consume.out",        box (decimal outQ)
          "consume.desbalance", box (decimal dmb) ] |> Map.ofList
      Ok { state=stOut; costs= penalty |> Option.toList; notes=notes }
