module Sleeve


open Tipos
open Helpers
open Unidades


let sleeve (p: SleeveParams) : Operation =
  fun stIn ->
    if stIn.qtyMMBtu <= 0.0m<MMBTU> then Error (Other "Trade: qtyMMBtu <= 0")
    else
      let stOut = { stIn with owner = p.buyer; contract = p.contractRef }
      let amount = stIn.qtyMMBtu * p.adder
      let fee =
        { kind= Sleeve
          qtyMMBtu = stIn.qtyMMBtu
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





