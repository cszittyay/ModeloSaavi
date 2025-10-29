module Transport
open Tipos
open Unidades


let transport (p: TransportParams) : Operation =
  fun stIn ->
    if stIn.location <> p.entry then
      Error (Other (sprintf "Transport: estado en %s, se esperaba %s" stIn.location p.entry))
    elif stIn.qtyMMBtu < 0.0m<MMBTU> then
      Error (Other "Transport: qtyMMBtu negativa")
    else
      let fuel  = stIn.qtyMMBtu * p.fuelPct / 100.0m
      let qtyOut = max 0.0m<MMBTU> (stIn.qtyMMBtu - fuel)
      let stOut = { stIn with qtyMMBtu = qtyOut; location = p.exit }
      let cUso =
        let amount =  qtyOut * p.usageRate 
        { kind = Transport
          qtyMMBtu = qtyOut
          rate = p.usageRate
          amount=amount
          meta= [ "shipper", box p.shipper; "fuelMMBtu", box (decimal fuel) ] |> Map.ofList }
      let cRes =
            [{ kind= Transport
               qtyMMBtu = 0.0m<MMBTU>
               rate = p.reservation
               amount = p.reservation * qtyOut // reservation es $/MMBtu, se cobra sobre qtyOut
               meta= [ "shipper", box p.shipper ] |> Map.ofList }]
      let notes =
        [ "transport.fuelPct", box p.fuelPct
          "transport.fuelMMBtu", box (decimal fuel)
          "transport.entry", box p.entry
          "transport.exit",  box p.exit ]
        |> Map.ofList
      Ok { state=stOut; costs=cUso::cRes; notes=notes }


