module Escenario

open System
open Unidades           // Energy, RateGas, Money (decimal<...>)
open Tipos              // State, Transition, DomainError, SupplierLeg, TransactionConfirmation, CostKind, etc.
open Supply             // supplyMany : SupplierLeg list -> Operation
open Transport          // transport  : RateGas -> string -> string -> string -> Operation
open Consume            // consume    : decimal<MMBTU> -> decimal<USD/MMBTU> -> Operation
open Trade              // trade      : TradeParams -> Operation
open Kleisli            // runAll     : Operation list -> Plan (State -> Result<Transition list, _>)

/// Helpers de impresión (opcionales)
let private printMoney (m: Money) = (decimal m).ToString("0.#####")
let private printRate (r: RateGas) = (decimal r).ToString("0.#####")
let private printQty  (q: Energy) = (decimal q).ToString("0.#####")

/// Construye un escenario: compra consolidada (2 suppliers) + transporte único
let escenarioSupplyManyMasTransport_1 () =
  // Datos comunes del día y hub
  let gasDay     = DateOnly(2025, 10, 22)
  let deliveryPt = Location "Waha"
  let entryPt    = Location "Elhemberg"
  let buyer      = "INDUSTRIA_X"

  // TC 1
  let tc1 : TransactionConfirmation =
    { tcId        = "TC-001"
      gasDay      = gasDay
      deliveryPt  = deliveryPt
      seller      = "SUPPLIER_A"
      buyer       = buyer
      qtyMMBtu    = 8000.0m<MMBTU>
      price       = 3.10m<USD/MMBTU>
      contractRef = "C-A-2025"
      meta        = Map.empty }

  // TC 2
  let tc2 : TransactionConfirmation =
    { tcId        = "TC-002"
      gasDay      = gasDay
      deliveryPt  = deliveryPt
      seller      = "SUPPLIER_B"
      buyer       = buyer
      qtyMMBtu    = 5000.0m<MMBTU>
      price       = 3.35m<USD/MMBTU>
      contractRef = "C-B-2025"
      meta        = Map.empty }

  let legs : SupplierLeg list = [ { tc = tc1 }; { tc = tc2 } ]

  // Estado inicial
  let st0 : State =
    { qtyMMBtu = 0.0m<MMBTU>
      owner    = buyer
      contract = "INIT"
      location = deliveryPt
      gasDay   = gasDay
      meta     = Map.empty }

  // Pipeline: supplyMany -> transport
  let rate : RateGas = 0.08m<USD/MMBTU>


  //type TransportParams =
  //{ entry       : Location
  //  exit        : Location
  //  shipper     : Party
  //  fuelPct     : decimal
  //  usageRate   : decimal<USD/MMBTU>       // $/MMBtu sobre salida
  //  reservation : decimal<USD/MMBTU>           // monto fijo (ej. diario o mensual), fuera del qtyMMBtu
  //}
  // transport : RateGas -> string -> string -> string -> Operation
  let tp = { entry = entryPt; exit = deliveryPt; shipper = buyer; fuelPct = 0.01m; usageRate = rate; reservation = 0.2m<USD/MMBTU> }
  
  let tr : Operation = transport tp

  let ops : Operation list =
    [ supplyMany legs
      transport tp]

  // Ejecutar y acumular transiciones
  match runAll ops st0 with
  | Error e ->
      printfn "ERROR en escenario: %A" e

  | Ok transitions ->
      printfn "Transiciones ejecutadas: %d" (List.length transitions)
      transitions
      |> List.iteri (fun i t ->
          let s = t.state
          printfn "#%d op=%A qty=%s MMBtu owner=%s loc=%s contract=%s"
            (i+1)
            (t.notes |> Map.tryFind "op" |> Option.defaultValue (box ""))
            (printQty s.qtyMMBtu) s.owner s.location s.contract

          // listar costos de la transición
          if not (List.isEmpty t.costs) then
            t.costs
            |> List.iter (fun c ->
                printfn "   cost kind=%A qty=%s rate=%s USD/MMBTU amount=%s USD"
                  c.kind (printQty c.qtyMMBtu) (printRate c.rate) (printMoney c.amount))
          else
            printfn "   cost: (sin costos)")

      // Agregados útiles (ejemplo: costo total de transporte)
      let totalTransportUSD : Money =
        transitions
        |> List.collect (fun t -> t.costs)
        |> List.filter   (fun c -> c.kind = CostKind.Transport)
        |> List.sumBy    (fun c -> c.amount)

      let finalState = (List.last transitions).state
      printfn "TOTAL transporte = %s USD" (printMoney totalTransportUSD)
      printfn "Estado final: qty=%s MMBtu loc=%s contract=%s"
        (printQty finalState.qtyMMBtu) finalState.location finalState.contract

// Ejecutar el escenario
escenarioSupplyManyMasTransport_1 ()




let private moneyStr (m: Money) = (decimal m).ToString("0.#####")
let private rateStr  (r: RateGas) = (decimal r).ToString("0.#####")
let private qtyStr   (q: Energy) = (decimal q).ToString("0.#####")

let escenario_Supply_Transport_Trade () =
  // Base
  let gasDay     = DateOnly(2025, 10, 22)
  let deliveryPt = "Waha"
  let buyer      = "INDUSTRIA_X"

  // Dos TCs
  let tc1 : TransactionConfirmation =
    { tcId        = "TC-001"; gasDay = gasDay; deliveryPt = deliveryPt
      seller      = "SUPPLIER_A"; buyer = buyer
      qtyMMBtu    = 8000.0m<MMBTU>; price = 3.10m<USD/MMBTU>
      contractRef = "C-A-2025"; meta = Map.empty }

  let tc2 : TransactionConfirmation =
    { tcId        = "TC-002"; gasDay = gasDay; deliveryPt = deliveryPt
      seller      = "SUPPLIER_B"; buyer = buyer
      qtyMMBtu    = 5000.0m<MMBTU>; price = 3.35m<USD/MMBTU>
      contractRef = "C-B-2025"; meta = Map.empty }

  let legs : SupplierLeg list = [ { tc = tc1 }; { tc = tc2 } ]

  // Estado inicial
  let st0 : State =
    { qtyMMBtu = 0.0m<MMBTU>
      owner    = buyer
      contract = "INIT"
      location = deliveryPt
      gasDay   = gasDay
      meta     = Map.empty }

  // TransportParams (nueva firma)
  let pTrans : TransportParams =
    { entry       = deliveryPt
      exit        = "CityGate_X"
      shipper     = "SHIPPER_Y"
      fuelPct     = 0.02m                 // 2% fuel
      usageRate   = 0.08m<USD/MMBTU>
      reservation = 0.50m<USD/MMBTU> }    // ej.: fijo

  // Trade con adder 0.5 USD/MMBTU
  let pTrade : TradeParams =
    { side         = TradeSide.Sell
      seller       = "MARKET_X"
      buyer        = "MARKET_Y"
      qtyMMBtu     = 2000.0m<MMBTU>
      adder        = 0.50m<USD/MMBTU>
      contractRef  = "MARKET_Z"
      meta         = Map.empty }

  // Composición Kleisli (último estado)
  let pipeline : Operation =
        supplyMany legs
    >=> transport pTrans
    >=> trade pTrade

  match pipeline st0 with
  | Error e ->
      printfn "ERROR pipeline: %A" e
  | Ok tFinal ->
      printfn "OK pipeline. Ultimo estado: qty=%s MMBtu loc=%s contract=%s"
        (qtyStr tFinal.state.qtyMMBtu) tFinal.state.location tFinal.state.contract

  // Si querés también la traza completa (balances/costos):
  let ops : Operation list = [ supplyMany legs; transport pTrans; trade pTrade ]
  match runAll ops st0 with
  | Error e ->
      printfn "ERROR runAll: %A" e
  | Ok transitions ->
      printfn "Transiciones: %d" (List.length transitions)
      transitions |> List.iteri (fun i t ->
        let s = t.state
        let opName = t.notes |> Map.tryFind "op" |> Option.map string |> Option.defaultValue ""
        printfn "#%d op=%s qty=%s MMBtu loc=%s contract=%s"
          (i+1) opName (qtyStr s.qtyMMBtu) s.location s.contract

        if List.isEmpty t.costs then printfn "   cost: (sin costos)"
        else
          t.costs |> List.iter (fun c ->
            printfn "   cost kind=%A qty=%s rate=%s USD/MMBTU amount=%s USD"
              c.kind (qtyStr c.qtyMMBtu) (rateStr c.rate) (moneyStr c.amount)))

      let totalTransportUSD : Money =
        transitions
        |> List.collect (fun t -> t.costs)
        |> List.filter   (fun c -> c.kind = CostKind.Transport)
        |> List.sumBy    (fun c -> c.amount)

      let totalFeesUSD : Money =
        transitions
        |> List.collect (fun t -> t.costs)
        |> List.filter   (fun c -> c.kind = CostKind.Fee)
        |> List.sumBy    (fun c -> c.amount)

      let sF = (List.last transitions).state
      printfn "TOTAL Transport = %s USD | TOTAL Fees(Trade) = %s USD"
        (moneyStr totalTransportUSD) (moneyStr totalFeesUSD)
      printfn "Estado final: qty=%s MMBtu loc=%s contract=%s"
        (qtyStr sF.qtyMMBtu) sF.location sF.contract

// Ejecutar
escenario_Supply_Transport_Trade ()