module Supply

open System
open Unidades
open Tipos
open Helpers


let supply (p: SupplyParams) : Operation =
  fun stIn ->
    if stIn.qtyMMBtu <= 0.0m<MMBTU> then Error "Supply: qtyMMBtu <= 0"
    else
      let stOut = { stIn with owner = p.buyer; contract = p.contractRef }
      let cost =
            // Interpretamos priceFix como $/MMBtu
            let amount = stIn.qtyMMBtu * p.priceFix
            [{ kind="GAS"
               qtyMMBtu = stIn.qtyMMBtu
               rate= p.priceFix
               amount=amount
               meta= [ "seller", box p.seller;  ] |> Map.ofList }]
      Ok { state=stOut; costs=cost; notes= [ "supply.seller", box p.seller;"supply.buyer", box p.buyer; "supply.contract", box p.contractRef; "supply.priceFix", box (decimal p.priceFix) ] |> Map.ofList }
