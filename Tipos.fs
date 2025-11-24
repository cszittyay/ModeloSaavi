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

// Identificadores “de negocio”
type Party    = string
type Location = string
type Contract = string


type DomainError =
  | QuantityNonPositive of where:string
  | MissingContract of id:string
  | CapacityExceeded of what:string
  | InvalidUnits of detail:string
  | Other of string




type CostKind = Gas | Transport | Storage | Tax | Fee | Sleeve | Nulo

type Temporalidad = DayAhead | Intraday | Monthly

type TradingHub = Mainline | Waha | Permian | SanJuan | SoCal

// 4) trade: compra/venta directa entre contrapartes (signo por rol)
type TradeSide = | Buy | Sell


type SleeveSide = |Export | Import


type RateGas = decimal<USD/MMBTU>

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
    price       : decimal<USD/MMBTU>
    adder       : decimal<USD/MMBTU>
    contractRef : string
    meta        : Map<string,obj> }


// =====================
// Costos tipados
// =====================

type ItemCost = {
  kind     : CostKind
  provider : Party             // quien factura (cuando aplica)
  qEnergia : Energy
  rate     : RateGas           // $/MMBtu cuando aplica
  amount   : Money             // rate * qty
  meta     : Map<string,obj>   // detalles (seller, tcId, etc.)
}

// =====================
// Estado entre operaciones
// =====================
type State = {
  energy   : Energy
  owner    : Party
  contract : Contract
  location : Location 
  gasDay   : DateOnly
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
type TransportParams =
  { provider    : Party
    entry       : Location
    exit        : Location
    shipper     : Party
    fuelPct     : decimal
    usageRate   : decimal<USD/MMBTU>       // $/MMBtu sobre salida
    reservation : decimal<USD/MMBTU>           // monto fijo (ej. diario o mensual), fuera del qEnergia
  }


// ========================================================
// 3) TRADE / COMERCIALIZACIÓN (cambia dueño, no cambia qEnergia)
// ========================================================
type TradeParams =
  { side        : TradeSide
    seller      : Party
    buyer       : Party
    adder       : decimal<USD/MMBTU>        // $/MMBtu (fee/adder)
    contractRef : Contract
    meta        : Map<string,obj> }


// para el caso de multiple trade legs dentro de un supply
type SupplyTradeParams =
  { supply : SupplyParams
    trade  : TradeParams }


type MultiSupplyTradeParams =
  { legs       : SupplyTradeParams list
  }




type SleeveParams =
  { provider    : Party
    seller      : Party
    buyer       : Party
    sleeveSide  : SleeveSide
    adder       : decimal<USD/MMBTU>        // $/MMBtu (fee/adder)
    contractRef : Contract
    meta        : Map<string,obj> }

// ========================================================
// 4) CONSUMO (sale del sistema; calcula desbalance vs medido)
// ========================================================
type ConsumeParams =
  { provider      : Party
    meterLocation : Location
    measured      : decimal<MMBTU>
    penaltyRate   : decimal<USD/MMBTU>
    tolerancePct  : decimal }



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
