module Transport
open System
open Tipos
open Unidades
open Helpers

/// Operación de transporte de gas




let transport (p: TransportParams) : Operation =
  fun stIn ->
    // Validaciones de dominio
    if String.IsNullOrWhiteSpace p.shipper then
      Error (MissingContract "shipper")

    elif stIn.location <> p.entry then
      Error (Other (sprintf "State@%s, esperado entry %s" stIn.location p.entry))

    elif stIn.qtyMMBtu <= 0.0m<MMBTU> then
      Error (QuantityNonPositive "transport.qtyMMBtu")

    elif p.fuelPct < 0m || p.fuelPct >= 1m then
      Error (InvalidUnits "transport.fuelPct debe estar en [0,1)")
    else
      // Cálculos: shrink por fuel y costo de uso
      let qtyIn  : Energy = stIn.qtyMMBtu
      let fuel   : Energy = qtyIn * p.fuelPct
      let qtyOut : Energy = qtyIn - fuel

      // Líneas de costo: USAGE y RESERVATION
      let usageAmount : Money = qtyOut * p.usageRate

      // Reservation: registramos como “fijo” (basis=reservation_fixed).
      // Para materializar a USD respetando unidades, multiplicamos por 1<MMBTU>.
      let reservationAmount : Money = 1.0m<MMBTU> * p.reservation

      let costUsage : ItemCost =
        { kind     = CostKind.Transport
          qtyMMBtu = qtyOut
          rate     = p.usageRate
          amount   = usageAmount
          meta     =
            [ "component",  box "usage"
              "shipper",    box p.shipper
              "entry",      box p.entry
              "exit",       box p.exit ]
            |> Map.ofList }

      let costReservation : ItemCost =
        { kind     = CostKind.Transport
          qtyMMBtu = 1.0m<MMBTU>            // basis sintético para obtener USD
          rate     = p.reservation
          amount   = reservationAmount
          meta     =
            [ "component",  box "reservation"
              "basis",      box "reservation_fixed"
              "shipper",    box p.shipper
              "entry",      box p.entry
              "exit",       box p.exit ]
            |> Map.ofList }

      let costs =
        if p.reservation > 0.0m<USD/MMBTU> then [ costUsage; costReservation ]
        else [ costUsage ]

      let notes =
        [ "op",          box "transport"
          "fuelPct",     box p.fuelPct
          "qty.in",      box (decimal qtyIn)
          "qty.fuel",    box (decimal fuel)
          "qty.out",     box (decimal qtyOut)
          "usageRate",   box (decimal p.usageRate)
          "reservation", box (decimal p.reservation)
          "shipper",     box p.shipper
          "entry",       box p.entry
          "exit",        box p.exit ]
        |> Map.ofList

      let stOut : State =
        { stIn with
            qtyMMBtu = qtyOut
            location = p.exit
            contract = if String.IsNullOrWhiteSpace stIn.contract then p.shipper else stIn.contract
            meta     =
              stIn.meta
              |> Map.add "transport.entry"   (box p.entry)
              |> Map.add "transport.exit"    (box p.exit)
              |> Map.add "transport.shipper" (box p.shipper)
              |> Map.add "transport.fuelPct" (box p.fuelPct)
              |> Map.add "transport.usage"   (box (decimal p.usageRate))
              |> Map.add "transport.resv"    (box (decimal p.reservation)) }

      Ok { state = stOut; costs = costs; notes = notes }