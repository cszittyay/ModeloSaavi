module Tipos

open System



[<CLIMutable>]
type FlowRunErrorDto =
  { Code: string
    Message: string
    Details: string option }

[<CLIMutable>]
type RunFlowOutcome =
  { Ok: bool
    RunId: int option
    Error: FlowRunErrorDto option }


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
type IdVentaGas = int    // PK de la tabla VentasGas

type DomainError =
  | QuantityNonPositive of where:string
  | MissingContract of id:string
  | CapacityExceeded of what:string
  | InvalidUnits of detail:string
  | MissingTradeForFlowDetail of flowMaster: string * flowDetailId:int * path:string
  | MissingTransportFlowDetail of flowMaster: string * flowDetailId:int * path:string
  | MissingFlowMaster of idFlowMaster:int
  | MissingConsumoForFlowDetail of flowMaster: string * gasDay:DateOnly * path:string
  | MissingSellFlowDetail of flowDetailId:int * gasDay:DateOnly * path:string
  | MissingSupplyFlowDetail of flowMaster: string * gasDay:DateOnly * path:string
  | MissingSleeveFlowDetail of flowMaster: string * flowDetailId:int * path:string
  | MissingFlowType of operationType: string
  | MissingFlowDetail of flowMaster:string
  | Other of string
  


type CostKind = Gas | Transport | Storage | Tax | Fee | Sleeve |Sell |  Nulo

type Temporalidad = DayAhead | Intraday | Monthly

type TradingHub = Mainline | Waha | Permian | SanJuan | SoCal | HSC | AguaDulce

// 4) trade: compra/venta directa entre contraparflowtes (signo por rol)
type TradeSide = | Buy | Sell

type SleeveSide = |Export | Import

type RateGas = EnergyPrice

type GasDay = DateOnly

type FlowMasterId = int

type FlowDetailId = int

type TransactionId = int

type EntidadLegalId = int

type LocationId = int

type RutaId = int

type VentaGasId = int

type ContratoId = int   


type SupplyParams =
  { transactionId: TransactionId
    buyerId    : EntidadLegalId
    sellerId   : EntidadLegalId
    flowDetailId : FlowDetailId
    buyer         : Party
    seller        : Party
    gasDay       : GasDay
    temporalidad : Temporalidad
    deliveryPt  : Location
    deliveryPtId : LocationId
    qEnergia     : decimal<MMBTU>
    index        : int 
    adder        : EnergyPrice
    price        : EnergyPrice
    contractRef : Contract
    meta         : Map<string,obj> }


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
  ownerId  : EntidadLegalId
  transactionId : TransactionId
  location : Location 
  locationId : LocationId
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
  { transactionId : TransactionId
    flowDetailId : FlowDetailId
    provider    : Party
    providerId  : EntidadLegalId
    pipeline    : Pipeline                  // Gasoducto
    entry       : Location
    exit        : Location
    routeId     : RutaId
    shipper     : Party
    shipperId   : EntidadLegalId
    fuelMode    : FuelMode  
    fuelPct     : decimal
    CMD         : Energy
    usageRate   : EnergyPrice
    meta        : Map<string,obj>
  }


// ========================================================
// 3) TRADE / COMERCIALIZACIÓN (cambia dueño, no cambia qEnergia)
// ========================================================
type TradeParams =
  { side        : TradeSide
    transactionId: TransactionId
    flowDetailId : FlowDetailId
    sellerId      : EntidadLegalId
    seller        : Party
    buyer         : Party
    buyerId       : EntidadLegalId
    locationId    : LocationId
    location      : Location
    adder       : EnergyPrice
    price       : EnergyPrice
    meta        : Map<string,obj> }


// es una operación de venta intercalada en el flujo
type SellParams =
  { idVentaGas  : IdVentaGas
    flowDetailId : FlowDetailId
    transactionId : TransactionId
    location    : Location
    locationId  : LocationId
    gasDay      : GasDay
    seller      : Party
    sellerId    : EntidadLegalId
    buyer       : Party
    buyerId     : EntidadLegalId
    qty         : Energy
    price       : EnergyPrice
    adder       : EnergyPrice        // $/MMBtu (fee/adder)
    contractRef : Contract
    meta        : Map<string,obj> }

type SleeveParams =
  { provider    : Party
    transactionId : TransactionId
    flowDetailId : FlowDetailId
    seller      : Party
    buyer       : Party
    location    : Location
    locationId  : LocationId
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
    gasDay        : GasDay
    flowDetailId  : FlowDetailId
    location      : Location
    locationId    : LocationId
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
    flowMasterId : FlowMasterId  // es el identificador del flow master
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
    | "Day Ahead" -> Temporalidad.DayAhead
    | "Intraday" -> Temporalidad.Intraday
    | "Monthly"  -> Temporalidad.Monthly
    | x          -> failwithf "Temporalidad desconocida: %s" x
