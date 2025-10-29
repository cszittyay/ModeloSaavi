module Trade

open Tipos
open Helpers
open Unidades


let trade (p: TradeParams) : Operation =
  fun stIn ->
    if stIn.qtyMMBtu <= 0.0m<MMBTU> then Error (Other "Trade: qtyMMBtu <= 0")
    else
      let stOut = { stIn with owner = p.buyer; contract = p.contractRef }
      let amount = stIn.qtyMMBtu * p.adder
      let fee =
        { kind= Fee
          qtyMMBtu = stIn.qtyMMBtu
          rate= p.adder
          amount = amount
          meta= [ "seller", box p.seller ] |> Map.ofList }
      Ok { state=stOut; costs=[fee]; notes= Map.empty }



