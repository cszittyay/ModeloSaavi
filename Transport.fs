module Transport
open Tipos



let transport (p: TransportParams) : Operation =
  fun stIn ->
    if stIn.loc <> p.entry then
      Error (sprintf "Transport: estado en %s, se esperaba %s" stIn.loc p.entry)
    elif stIn.qty < 0.0<mmbtu> then
      Error "Transport: qty negativa"
    else
      let fuel  = stIn.qty * (p.fuelPct |> float |> LanguagePrimitives.FloatWithMeasure<mmbtu>)
      let qtyOut = max 0.0<mmbtu> (stIn.qty - fuel)
      let stOut = { stIn with qty = qtyOut; loc = p.exit }
      let cUso =
        let amount = scaleMoney (decimal (float qtyOut)) p.usageRate |> round2
        { kind="TRANSPORT-USAGE"
          qty=Some qtyOut
          rate=Some p.usageRate
          amount=amount
          meta= [ "shipper", box p.shipper; "fuelMMBtu", box (float fuel) ] |> Map.ofList }
      let cRes =
        match p.reservation with
        | None -> []
        | Some r ->
            [{ kind="TRANSPORT-RESERVATION"
               qty=None
               rate=Some r
               amount=round2 r
               meta= [ "shipper", box p.shipper ] |> Map.ofList }]
      let notes =
        [ "transport.fuelPct", box p.fuelPct
          "transport.fuelMMBtu", box (float fuel)
          "transport.entry", box p.entry
          "transport.exit",  box p.exit ]
        |> Map.ofList
      Ok { state=stOut; costs=cUso::cRes; notes=notes }


