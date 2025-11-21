module Sleeve


open Tipos
open Helpers
open Unidades


let sleeve (p: SleeveParams) : Operation =
  fun stIn ->
    if stIn.energy <= 0.0m<MMBTU> then Error (Other "Trade: qEnergia <= 0")
    else
      let stOut = { stIn with owner = p.buyer; contract = p.contractRef }
      let amount = stIn.energy * p.adder
      let fee =
        { kind= Sleeve
          qEnergia = stIn.energy
          rate= p.adder
          provider = p.provider
          amount = amount
          meta= [ "seller", box p.seller 
                  "adder", box p.adder
                  "amount", box (decimal amount)
                  ] |> Map.ofList }
      Ok { state=stOut; costs=[fee]; notes= [ "op", box "Sleeve"
                                              "provider", box p.provider        
                                              "seller", box p.seller
                                              "buyer", box p.buyer
                                              "adder", box p.adder
                                              "contractRef", box p.contractRef   ] |> Map.ofList }





