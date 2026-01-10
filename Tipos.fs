module Tipos

open System


// ===== Unidades (Units of Measure) =====
[<Measure>] type MMBTU
[<Measure>] type GJ
[<Measure>] type USD
[<Measure>] type MXN

// ===== Tipos base =====
type Energy = decimal<MMBTU>      // MMBtu (puedes cambiar por GJ y agregar conversión)
type Money  = decimal<USD>      // USD (o MXN si prefieres)


type Stage = Build | Execute | Project | Persist

// ===== Tipos derivados =====
type EnergyPrice = decimal<USD/MMBTU>   // $/MMBtu

// Identificadores “de negocio”
type Party    = string
type Location = string
type Contract = string
type Pipeline = string
type Formula  = string


type DomainError =
  | QuantityNonPositive of where:string
  | MissingContract of id:string
  | CapacityExceeded of what:string
  | InvalidUnits of detail:string
  | Other of string


type CostKind = Gas | Transport | Storage | Tax | Fee | Sleeve |Sell |  Nulo

type Temporalidad = DayAhead | Intraday | Monthly

type TradingHub = Mainline | Waha | Permian | SanJuan | SoCal | HSC | AguaDulce

// 4) trade: compra/venta directa entre contraparflowtes (signo por rol)
type TradeSide = | Buy | Sell

type SleeveSide = |Export | Import

type RateGas = EnergyPrice

type GasDay = DateOnly





type SupplyParams =
  { tcId        : string
    gasDay      : GasDay
    tradingHub  : TradingHub
    temporalidad: Temporalidad
    deliveryPt  : Location
    seller      : string
    buyer       : string
    qEnergia    : decimal<MMBTU>
    index       : EnergyPrice
    adder       : EnergyPrice
    price       : EnergyPrice
    contractRef : string
    meta        : Map<string,obj> }


// =====================
// Costos tipados
// =====================

type ItemCost = {
  kind     : CostKind
  provider : Party             // quien factura (cuando aplica)
  qEnergia : Energy
  rate     : EnergyPrice           // $/MMBtu cuando aplica
  amount   : Money             // rate * qty
  meta     : Map<string,obj>   // detalles (seller, tcId, etc.)
}

// =====================
// Estado entre operaciones
// =====================
type State = {
  gasDay   : DateOnly
  energy   : Energy
  owner    : Party
  contract : Contract
  location : Location 
 
  meta     : Map<string,obj>
}

// =====================
// Resultado de una operación
// =====================
type Transition = {
  state : State
  costs : ItemCost list
  notes : Map<string,obj>       // fuel, desbalance, shipper, etc.
}

// Es una función que toma State y devuelve Result<Transition, Error>
// es lo que permite encadenar operaciones (composició de funciones)
type Operation = State -> Result<Transition, DomainError>






// ========================================================
// 2) TRANSPORTE (mueve físico, no cambia dueño)
// ========================================================

// Modo de cálculo de fuel  
// RxBase: se calcula sobre la qEnergia recibida, 
// ExBase: se calcula sobre la qEnergia entregada
type FuelMode = |RxBase | ExBase



type TransportParams =
  { provider    : Party
    pipeline    : Pipeline                  // Gasoducto
    entry       : Location
    exit        : Location
    shipper     : Party
    fuelMode    : FuelMode  
    fuelPct     : decimal
    usageRate   : EnergyPrice       // $/MMBtu sobre salida
    reservation : EnergyPrice       // monto fijo (ej. diario o mensual), fuera del qEnergia
    acaRate     : EnergyPrice       // $/MMBtu es un Adder que se cobra en USA
    meta        : Map<string,obj>
  }


// ========================================================
// 3) TRADE / COMERCIALIZACIÓN (cambia dueño, no cambia qEnergia)
// ========================================================
type TradeParams =
  { side        : TradeSide
    seller      : Party
    buyer       : Party
    location    : Location
    adder       : EnergyPrice        // $/MMBtu (fee/adder)
    contractRef : Contract
    meta        : Map<string,obj> }


// es una operación de venta intercalada en el flujo
type SellParams =
  { location    : Location
    gasDay      : GasDay
    seller      : Party
    buyer       : Party
    qty         : Energy
    price       : EnergyPrice
    adder       : EnergyPrice        // $/MMBtu (fee/adder)
    contractRef : Contract
    meta        : Map<string,obj> }

type SleeveParams =
  { provider    : Party
    seller      : Party
    buyer       : Party
    location    : Location
    sleeveSide  : SleeveSide
    index       : int
    adder       : EnergyPrice
    contractRef : Contract
    meta        : Map<string,obj> }

// ========================================================
// 4) CONSUMO (sale del sistema; calcula desbalance vs medido)
// ========================================================
type ConsumeParams =
  { 
    gasDay      : GasDay
    provider      : Party
    meterLocation : Location
    measured      : decimal<MMBTU>
   }






/// Bloques atómicos de la cadena física/comercial
type Block =
  | Consume           of ConsumeParams
   | Supply            of SupplyParams
  | SupplyMany        of SupplyParams list
  | Transport         of TransportParams
  | Trade             of TradeParams
  | Sleeve            of SleeveParams
  | Sell              of SellParams
  | SellMany          of SellParams list

type PathRole =
  | Contributor
  | Final


type FlowId = {
    modo   : string
    central: string
    path   : string  // es un identificador de la ruta que define un flow
}



type FlowStep = {
    flowId  : FlowId
    order   : int
    block   : Block
    joinKey : string option
    ref     : string
}





type FlowPath = {
  id    : FlowId
  role  : PathRole
  steps : FlowStep list
}

// son los dos tipo de operación: Lineal o 
type FlowDef =
  | Linear of flowId: FlowId * steps: FlowStep list
  | Join of joinKey: string * paths: Map<FlowId, FlowPath>



type DailyBalance = {
  fecha   : DateOnly
  hub     : string
  buy     : Energy
  sell    : Energy
  inject  : Energy
  withdraw: Energy
  consume : Energy
}


module Unidades =

    // conversión: 1 MMBtu ≈ 1.055056 GJ
    [<Literal>]
    let gj_per_mmbtu = 1.055056m

    let inline toGJ (e: decimal<MMBTU>) : decimal<GJ> =   (decimal e) * gj_per_mmbtu |> LanguagePrimitives.DecimalWithMeasure<GJ>

    let inline toMMBtu (e: decimal<GJ>) : decimal<MMBTU> =  (decimal e) / gj_per_mmbtu |> LanguagePrimitives.DecimalWithMeasure<MMBTU>




// Mainline | Waha | Permian | SanJuan | SoCal | HSC | AguaDulce
let parseTradingHub = function
    | "Permian"         -> TradingHub.Permian
    | "Waha FDt Com"    -> TradingHub.Waha
    | "HSC"             -> TradingHub.HSC
    | "San Juan"        -> TradingHub.SanJuan
    | "SML"             -> TradingHub.AguaDulce
    | "SoCal Gas CG FDt Com" -> TradingHub.SoCal
    | x                 -> failwithf "TradingHub desconocido: %s" x

let parseTemporalidad = function
    | "DayAhead" -> Temporalidad.DayAhead
    | "Intraday" -> Temporalidad.Intraday
    | "Monthly"  -> Temporalidad.Monthly
    | x          -> failwithf "Temporalidad desconocida: %s" x
