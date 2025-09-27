module Trade

open Tipos
open Helpers
open Unidades


let trade (p: TradeParams) : Operation =
  fun stIn ->
    if stIn.qty <= 0.0<mmbtu> then Error "Trade: qty <= 0"
    else
      let stOut = { stIn with owner = p.buyer; cntr = p.contractRef }
      let amount = scaleMoney (decimal (float stIn.qty)) p.adder |> round2
      let fee =
        { kind="FEE-TRADE"
          qty=Some stIn.qty
          rate=Some p.adder
          amount=amount
          meta= [ "seller", box p.seller ] |> Map.ofList }
      Ok { state=stOut; costs=[fee]; notes= Map.empty }



