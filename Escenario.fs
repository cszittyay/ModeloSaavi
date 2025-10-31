module Escenario

open System
open Unidades           // Energy, RateGas, Money (decimal<...>)
open Tipos              // State, Transition, DomainError, SupplierLeg, TransactionConfirmation, CostKind, etc.
open Supply             // supplyMany : SupplierLeg list -> Operation
open Transport          // transport  : RateGas -> string -> string -> string -> Operation
open Consume            // consume    : decimal<MMBTU> -> decimal<USD/MMBTU> -> Operation
open Trade              // trade      : TradeParams -> Operation
open Sleeve             // sleeve     : TradeParams -> Operation
open Kleisli            // runAll     : Operation list -> Plan (State -> Result<Transition list, _>)
open Helpers


/// Helpers de impresión (opcionales)
let private printMoney (m: Money) = (decimal m).ToString("0.#####")
let private printRate (r: RateGas) = (decimal r).ToString("0.#####")
let private printQty  (q: Energy) = (decimal q).ToString("0.#####")

// Los trading hubs

let Mainline = Location "El Paso S Mainline FDt Com"
let Permian = Location "El Paso Permian FDt Com"
let SanJuan = Location "El Paso SanJuan FDt Com"
let SoCal = Location "SoCal Gas CG FDt Com"
let HSC = Location "Houston ShipChl FDt Com"
let WAHA = Location "Waha FDt Com"


  // Datos comunes del día y hub
let gasDay     = DateOnly(2025, 10, 22)
let deliveryPt = Location "Elhemberg"
let entryPt    = Location "Elhemberg"
let buyer      = "INDUSTRIA_X"  // Datos comunes del día y hub

// Estado inicial
let st0 : State =
    {   qtyMMBtu = 0.0m<MMBTU>
        owner    = buyer
        contract = "INIT"
        location = "deliveryPt"
        gasDay   = gasDay
        meta     = Map.empty }

// Ejecuta una secuencia de Operations y muestra el resultado
let run ops st0 =
  let setCol c = Console.ForegroundColor <- c
  match runAll ops st0 with
  | Error e ->
      printfn "ERROR runAll: %A" e
  | Ok transitions ->
      setCol ConsoleColor.Green
      printfn "Transiciones: %d" (List.length transitions)

      transitions |> List.iteri (fun i t ->
        let s = t.state
        let opName = t.notes |> Map.tryFind "op" |> Option.map string |> Option.defaultValue ""
        setCol ConsoleColor.White
        printfn "\n#%d op=%s qty=%s MMBtu loc=%s contract=%s"
          (i+1) opName (Display.qtyStr s.qtyMMBtu) s.location s.contract

        if List.isEmpty t.costs then printfn "   cost: (sin costos)"
        else
          t.costs |> List.iter (fun c ->
            setCol ConsoleColor.Yellow
            printfn "   cost kind=%A qty=%s rate=%s USD/MMBTU amount=%s USD"
              c.kind (Display.qtyStr c.qtyMMBtu) (Display.rateStr c.rate) (Display.moneyStr c.amount))
        setCol ConsoleColor.Green
        printfn "Notas: %A\n" t.notes
        )
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

      let totalGasUSD : Money =
        transitions
        |> List.collect (fun t -> t.costs)
        |> List.filter   (fun c -> c.kind = CostKind.Gas)
        |> List.sumBy    (fun c -> c.amount)


      let sF = (List.last transitions).state
      setCol ConsoleColor.White
      printfn "\nTotal Gas = %s USD | TOTAL Transport = %s USD | TOTAL Fees(Trade) = %s USD"
            (Display.moneyStr totalGasUSD) (Display.moneyStr totalTransportUSD) (Display.moneyStr totalFeesUSD)

      printfn "\nEstado final: qty=%s MMBtu loc=%s contract=%s"
        (Display.qtyStr sF.qtyMMBtu) sF.location sF.contract




/// Construye un escenario: compra consolidada (2 suppliers) + transporte único
let escenarioSupplyManyMasTransport_1 () =


  // TC 1
  let tc1 : TransactionConfirmation =
    { tcId        = "TC-001"
      gasDay      = gasDay
      cicle = DayAhead
      tradingHub  = Mainline
      deliveryPt  = deliveryPt
      seller      = "SUPPLIER_A"
      buyer       = buyer
      qtyMMBtu    = 8000.0m<MMBTU>
      price       = 3.10m<USD/MMBTU>
      adder       = 0.05m<USD/MMBTU>
      contractRef = "C-A-2025"
      meta        = Map.empty }

  // TC 2
  let tc2 : TransactionConfirmation =
    { tcId        = "TC-002"
      gasDay      = gasDay
      tradingHub  = Mainline
      cicle       = Intraday
      deliveryPt  = deliveryPt
      seller      = "SUPPLIER_B"
      buyer       = buyer
      qtyMMBtu    = 5000.0m<MMBTU>
      price       = 3.35m<USD/MMBTU>
      adder       = 0.029m<USD/MMBTU>
      contractRef = "C-B-2025"
      meta        = Map.empty }

  let legs : SupplierLeg list = [ { tc = tc1 }; { tc = tc2 } ]

  // Estado inicial
  let st0 : State =
    { qtyMMBtu = 0.0m<MMBTU>
      owner    = buyer
      contract = "INIT"
      location = entryPt
      gasDay   = gasDay
      meta     = Map.empty }

  // Pipeline: supplyMany -> transport
  let rate : RateGas = 0.08m<USD/MMBTU>


  let tp = { entry = entryPt 
             exit = deliveryPt 
             shipper = buyer 
             fuelPct = 0.01m 
             usageRate = rate 
             reservation = 0.2m<USD/MMBTU> }
  
  let tr : Operation = transport tp

  let ops : Operation list =
    [ supplyMany legs
      transport tp]


  run ops st0
  
// ============================================================================================================

let escenario_Supply_Transport_Trade ()=
  // Base
  let gasRxPt = "EHRENBERG"
  let entryPtA005F1 = gasRxPt
  let exitPtA005F1 = "OGILBY"
  let buyer      = "SES"

  // Dos TCs
  let tc1 : TransactionConfirmation =
    { tcId        = "TC-001"; 
      gasDay = gasDay; 
      tradingHub  = Mainline
      cicle = DayAhead;
      deliveryPt = gasRxPt          // Punto en el que el Suministrador (Productor) entrega el gas
      seller      = "JP Morgan"; 
      buyer = buyer
      qtyMMBtu    = 8000.0m<MMBTU>; 
      price = 3.10m<USD/MMBTU>
      adder       = 0.029m<USD/MMBTU>
      contractRef = "C-A-2025"; 
      meta = Map.empty }

  let tc2 : TransactionConfirmation =
    { tcId        = "TC-002"; 
      gasDay = gasDay; 
      cicle = DayAhead;
      tradingHub  = Mainline
      deliveryPt = gasRxPt
      seller      = "Exxon"; 
      buyer = buyer
      qtyMMBtu    = 5000.0m<MMBTU>; 
      price = 3.35m<USD/MMBTU>
      adder       = 0.029m<USD/MMBTU>
      contractRef = "C-B-2025"; 
      meta = Map.empty }

  let legs : SupplierLeg list = [ { tc = tc1 }; { tc = tc2 } ]

  

  // TransportParams 
  // Planta EAX
  // CMD: 135.000<MBTU>
  // Receipt Location:	336406	EHRENBERG REC
  // Delivery Location:	336408	OGILBY DEL

  let pA005F1 : TransportParams =
    { entry       = entryPtA005F1
      exit        = exitPtA005F1
      shipper     = "EAX"
      fuelPct     = 0.007m                 // 0,7% fuel
      usageRate   = 0.08m<USD/MMBTU>
      reservation = 0.50m<USD/MMBTU> }    // ej.: fijo


  // TransportParams 
  // Planta EAX
  // CMD: 135.000<MBTU>
  // Receipt Location:	OGILBY
  // Delivery Location:	336408	
  // Fuel: 0.1751%

  let pM005F1 : TransportParams =
    { entry       = exitPtA005F1
      exit        = "Planta_EAX"
      shipper     = "EAX"
      fuelPct     = 0.001751m                // 0.1751% fuel
      usageRate   = 0.08m<USD/MMBTU>
      reservation = 0.50m<USD/MMBTU> }    // ej.: fijo

  // Trade con adder 0.5 USD/MMBTU
  let pTradeSES : TradeParams =
    { side         = TradeSide.Sell
      seller       = "Suppliers USA"
      buyer        = "SES"
      adder        = 0.50m<USD/MMBTU>
      contractRef  = "MARKET_Z"
      meta         = Map.empty }

  let pTradeSE : TradeParams =
    { side         = TradeSide.Sell
      seller       = "SE"
      buyer        = "EAX"
      adder        = 0.20m<USD/MMBTU>
      contractRef  = "MARKET_Z"
      meta         = Map.empty }

  // parametros de Consumo (planta)
  let pConsume = 
    { meterLocation       = "Planta_EAX"
      measured           = 13000.0m<MMBTU>
      tolerancePct       = 5.0m
      penaltyRate        = 0.10m<USD/MMBTU>
      }
    // ej.: fijo


  // Composición Kleisli (último estado)
  //let pipeline : Operation =  supplyMany legs >=> trade pTrade  >=> transport pA005F1   >=> trade pTrade  >=> transport pM005F1   >=> consume pConsume

  //match pipeline st0 with
  //| Error e ->
  //    printfn "ERROR pipeline: %A" e
  //| Ok tFinal ->
  //    printfn "OK pipeline. Ultimo estado: qty=%s MMBtu loc=%s contract=%s"
  //      (Display.qtyStr tFinal.state.qtyMMBtu) tFinal.state.location tFinal.state.contract

  // Si querés también la traza completa (balances/costos):
  let ops : Operation list = [ supplyMany legs ;trade pTradeSES ;transport pA005F1  ;trade pTradeSE ;transport pM005F1  ;consume pConsume]
  run ops st0




// Operación que incluye Sleeve con Trafigura
let escenario_supply_Transport_Sleeve () =
// Base
  let gasRxPt = "EHRENBERG"
  let entryPtA005F1 = gasRxPt
  let exitPtA005F1 = "OGILBY"
  let buyer      = "SES"
  let contractRef = "Sleeve-Trafigura"

  // Dos TCs
  let tc1 : TransactionConfirmation =
    { tcId        = "TC-001"; 
      gasDay = gasDay; 
      tradingHub  = Mainline
      cicle = DayAhead;
      deliveryPt = gasRxPt          // Punto en el que el Suministrador (Productor) entrega el gas
      seller      = "Koch"; 
      buyer = buyer
      qtyMMBtu    = 5300.0m<MMBTU>; 
      price = 2.95m<USD/MMBTU>
      adder       = 0.029m<USD/MMBTU>
      contractRef = contractRef;
      meta = Map.empty }

  let tc2 : TransactionConfirmation =
    { tcId        = "TC-004"; 
      gasDay = gasDay; 
      tradingHub  = Mainline
      cicle = DayAhead;
      deliveryPt = gasRxPt          // Punto en el que el Suministrador (Productor) entrega el gas
      seller      = "J.Aron";
      buyer = buyer
      qtyMMBtu    = 26.0m<MMBTU>; 
      price = 2.95m<USD/MMBTU>
      adder       = 0.029m<USD/MMBTU>
      contractRef = contractRef;
      meta = Map.empty }



  let tc3 : TransactionConfirmation =
    { tcId        = "TC-002"; 
      gasDay = gasDay; 
      cicle = DayAhead;
      tradingHub  = Mainline
      deliveryPt = gasRxPt
      seller      = "J.Aron"; 
      buyer = buyer
      qtyMMBtu    = 10000.0m<MMBTU>; 
      price = 2.575m<USD/MMBTU>
      adder       = 0.02m<USD/MMBTU>
      contractRef = contractRef; 
      meta = Map.empty }

  let legs : SupplierLeg list = [ { tc = tc1 }; { tc = tc2 }; {tc = tc3} ]

  

  // TransportParams 
  // Planta EAX
  // CMD: 135.000<MBTU>
  // Receipt Location:	336406	EHRENBERG REC
  // Delivery Location:	336408	OGILBY DEL

  let pA005F1 : TransportParams =
    { entry       = entryPtA005F1
      exit        = exitPtA005F1
      shipper     = "EAX"
      fuelPct     = 0.007m                 // 0,7% fuel
      usageRate   = 0.08m<USD/MMBTU>
      reservation = 0.50m<USD/MMBTU> }    // ej.: fijo


  // TransportParams 
  // Planta EAX
  // CMD: 135.000<MBTU>
  // Receipt Location:	OGILBY
  // Delivery Location:	336408	
  // Fuel: 0.1751%

  let pM005F1 : TransportParams =
    { entry       = exitPtA005F1
      exit        = "Planta_La_Estrella"
      shipper     = "EAX"
      fuelPct     = 0.001751m                // 0.1751% fuel
      usageRate   = 0.08m<USD/MMBTU>
      reservation = 0.50m<USD/MMBTU> }    // ej.: fijo


  // Sleeve
  let pSleeve : SleeveParams =
    {
      seller = "SES"
      buyer = "SE"
      adder = 0.05m<USD/MMBTU>
      meta = Map.empty
      contractRef = contractRef
    }
  // Trade con adder 0.5 USD/MMBTU
  let pTradeSES : TradeParams =
    { side         = TradeSide.Sell
      seller       = "Suppliers USA"
      buyer        = "SES"
      adder        = 0.50m<USD/MMBTU>
      contractRef  = "MARKET_Z"
      meta         = Map.empty }

  let pTradeSE : TradeParams =
    { side         = TradeSide.Sell
      seller       = "SE"
      buyer        = "EAX"
      adder        = 0.20m<USD/MMBTU>
      contractRef  = "MARKET_Z"
      meta         = Map.empty }

  // parametros de Consumo (planta)
  let pConsume = 
    { meterLocation       = "Planta_La_Estrella"
      measured           = 15300.0m<MMBTU>
      tolerancePct       = 5.0m
      penaltyRate        = 0.10m<USD/MMBTU>
      }




  // Si querés también la traza completa (balances/costos):
  let ops : Operation list = [ supplyMany legs ;trade pTradeSES ;transport pA005F1 ; sleeve pSleeve ;transport pM005F1; trade pTradeSE  ;consume pConsume]
  run ops st0


