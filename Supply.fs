module Supply

open System
open Unidades
open Tipos
open Helpers


let supply (p: SupplyParams) : Operation =
  fun stIn ->
    if stIn.qty <= 0.0<mmbtu> then Error "Supply: qty <= 0"
    else
      let stOut = { stIn with owner = p.buyer; cntr = p.contractRef }
      let cost =
        match p.priceFix with
        | None -> []
        | Some px ->
            // Interpretamos priceFix como $/MMBtu
            let amount = scaleMoney (decimal (float stIn.qty)) px |> round2
            [{ kind="GAS"
               qty=Some stIn.qty
               rate=Some px
               amount=amount
               meta= [ "seller", box p.seller ] |> Map.ofList }]
      Ok { state=stOut; costs=cost; notes= [ "supply.seller", box p.seller ] |> Map.ofList }
