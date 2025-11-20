module Consume

open Tipos
open Helpers
open Unidades


/// Es el consumo en el punto de medición final.
// Toma el qty disponible en el State (outQ), lo compara contra lo medido (p.measured),
// aplica tolerancia porcentual y, si corresponde, genera una línea de costo (Fee)
// por el excedente de desbalance a la tasa p.penaltyRate.
// El State sale con qty = 0 (lo consumido deja el sistema).
let consume (p: ConsumeParams) : Operation =
  fun stIn ->
    // Validaciones de dominio
    if stIn.location <> p.meterLocation then
      Error (Other (sprintf "Consume: State@%s, esperado meterLocation=%s" stIn.location p.meterLocation))
    elif p.measured < 0.0m<MMBTU> then
      Error (InvalidUnits (sprintf "Consume.consume: measured=%M < 0" (decimal p.measured)))
    elif p.tolerancePct < 0.0m then
      Error (InvalidUnits (sprintf "Consume.consume: tolerancePct=%M < 0" p.tolerancePct))
    else
      // Cantidad "a la salida" del sistema (lo que se va a consumir)
      let outQ : Energy = stIn.qtyMMBtu
      // Desbalance respecto a lo medido: positivo => sobredespacho; negativo => subdespacho
      let dmb  : Energy = outQ - p.measured
      // Tolerancia absoluta (Energy) a partir del % sobre outQ
      let tol  : Energy = abs outQ * (p.tolerancePct / 100.0m)

      // Penalidad solo si |dmb| > tol
      let penalty : ItemCost  =
          let excedente : Energy = abs dmb - tol
          let amount : Money = excedente * p.penaltyRate   // (MMBTU * USD/MMBTU) = USD
          {
            kind     = CostKind.Nulo
            qtyMMBtu = excedente
            provider = p.provider
            rate     = p.penaltyRate
            amount   = amount
            meta     =
              [ "component"  , box "imbalance_penalty"
                "desbalance" , box (decimal dmb)
                "tolerancia" , box (decimal tol)
                "measured"   , box (decimal p.measured)
                "outQ"       , box (decimal outQ) ]
              |> Map.ofList
          }
      

      // El consumo deja qty = 0 en el estado
      let stOut : State = { stIn with qtyMMBtu = p.measured }

      // Notas para trazabilidad
      let notes =
        [ "op"                 , box "consume"
          "consume.measured"   , box (round(decimal p.measured))
          "consume.out"        , box (round(decimal outQ))
          "consume.desbalance" , box (round (decimal dmb))
          "consume.tolerance[]"  , box (round  (decimal tol)) ]
        |> Map.ofList

      Ok { state = stOut
           costs = penalty :: []
           notes = notes }