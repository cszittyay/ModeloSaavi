module Tipos

open System
open Unidades


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

type Temporalidad = DayAhead | Intraday

// 4) trade: compra/venta directa entre contrapartes (signo por rol)
type TradeSide = | Buy | Sell


type RateGas = decimal<USD/MMBTU>

type TransactionConfirmation = {
  tcId        : string
  gasDay      : DateOnly
  tradingHub  : Location       // Hub que sirve para fijar el precio de referencia o índice
  temporalidad: Temporalidad 
  deliveryPt  : Location      // Punto en el que el Suministrador (Productor) entrega el gas
  seller      : Party
  buyer       : Party
  qEnergia    : Energy        // volumen confirmado
  price       : RateGas       // $/MMBtu
  adder       : decimal<USD/MMBTU>
  contractRef : Contract
  meta        : Map<string,obj>
}



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

// Para armar compras múltiples (una por supplier)
type SupplierLeg = {
  tc : TransactionConfirmation
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
// 1) SUPPLY / SUMINISTRO (cambio de dueño, sin mover físico)
// ========================================================

type SupplyParams =
  { seller      : Party
    buyer       : Party
    priceFix    : decimal<USD/MMBTU>   // precio del gas ($/MMBtu) o monto fijo (ver meta)
    contractRef : Contract }




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


type SleeveParams =
  { provider    : Party
    seller      : Party
    buyer       : Party
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