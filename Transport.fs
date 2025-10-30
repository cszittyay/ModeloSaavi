module Transport
open Tipos
open Unidades
open Helpers


let transport
    (tarifa : decimal<USD/MMBTU>)
    (fromHub: string)
    (toHub  : string)
    (shipper: string)
    : Operation =
  fun stIn ->
    if System.String.IsNullOrWhiteSpace shipper then
      Error (MissingContract "shipper")
    elif stIn.location <> fromHub then
      Error (Other (sprintf "State@%s, esperado origen %s" stIn.location fromHub))
    elif stIn.qtyMMBtu <= 0.0m<MMBTU> then
      Error (QuantityNonPositive "transportOne.qtyMMBtu")
    else
      // amount con unidades correctas: MMBTU * USD/MMBTU = USD
      let amount : decimal<USD> = stIn.qtyMMBtu * tarifa

      // cost ítem (análogo a 'fee' de Trade)
      let cost : CostLine =
        { kind     = Transport
          qtyMMBtu = stIn.qtyMMBtu
          rate     = tarifa
          amount   = amount
          meta     =
            [ "from",    box fromHub
              "to",      box toHub
              "shipper", box shipper ]
            |> Map.ofList }

      // notas de la transición (opcional, para trazabilidad)
      let notes =
        [ "op",                   box "transport"
          "qty.MMBTU",            box (decimal stIn.qtyMMBtu)
          "tariff.USD_per_MMBTU", box (decimal tarifa)
          "cost.total.USD",       box (decimal amount) ]
        |> Map.ofList

      // podés persistir parte de esa info en el meta del STATE si querés
      let newMeta =
        stIn.meta
        |> Map.add "transport.shipper"            (box shipper)
        |> Map.add "transport.tariff.USD/MMBTU"   (box (decimal tarifa))
        |> Map.add "transport.cost.USD"           (box (decimal amount))
        |> Map.add "transport.from"               (box fromHub)
        |> Map.add "transport.to"                 (box toHub)

      let stOut : State =
        { stIn with
            location = toHub
            // si preferís no tocar contract, dejá 'contract = stIn.contract'
            contract = if System.String.IsNullOrWhiteSpace stIn.contract then shipper else stIn.contract
            meta     = newMeta }

      Ok { state = stOut; costs = [ cost ]; notes = notes }