module Trade

open Tipos
open Helpers
open Unidades


let trade (p: TradeParams) : Operation =
  fun stIn ->
    if stIn.energy <= 0.0m<MMBTU> then Error (Other "Trade: qEnergia <= 0")
    else
      let stOut = { stIn with owner = p.buyer; contract = p.contractRef }
      let amount = stIn.energy * p.adder
      let fee =
        { kind= Fee
          provider = Party "S/D"
          qEnergia = stIn.energy
          rate= p.adder
          amount = amount
          meta= [ "seller", box p.seller ] |> Map.ofList }
      Ok { state=stOut; costs=[fee]; notes= [ "op", box "Trade"
                                              "seller", box p.seller
                                              "buyer", box p.buyer
                                              "adder", box p.adder
                                              "contractRef", box p.contractRef   ] |> Map.ofList }



