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




type CostKind = Gas | Transport | Storage | Tax | Fee

type RateGas = decimal<USD/MMBTU>

type TransactionConfirmation = {
  tcId        : string
  gasDay      : System.DateOnly
  deliveryPt  : Location      // hub/punto de entrega (p.ej. Waha, HSC, etc.)
  seller      : Party
  buyer       : Party
  qtyMMBtu    : Energy        // volumen confirmado
  price       : RateGas       // $/MMBtu
  contractRef : Contract
  meta        : Map<string,obj>
}


// 4) trade: compra/venta directa entre contrapartes (signo por rol)
type TradeSide = | Buy | Sell

// Estado físico/contractual del gas en un punto de la cadena
type GasState =
  { qtyMMBtu  : Energy  // cantidad física de gas
    owner     : Party
    location  : Location
    gasDay    : DateOnly
    contract  : Contract } // p.ej. NAESB/TC, transporte, etc.


// =====================
// Costos tipados
// =====================

type CostLine = {
  kind     : CostKind
  qtyMMBtu : Energy
  rate     : RateGas           // $/MMBtu cuando aplica
  amount   : Money             // rate * qty
  meta     : Map<string,obj>   // detalles (seller, tcId, etc.)
}

// =====================
// Estado entre operaciones
// =====================
type State = {
  qtyMMBtu : Energy
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
  costs : CostLine list
  notes : Map<string,obj>       // fuel, desbalance, shipper, etc.
}




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
  { entry       : Location
    exit        : Location
    shipper     : Party
    fuelPct     : decimal
    usageRate   : decimal<USD/MMBTU>       // $/MMBtu sobre salida
    reservation : decimal<USD/MMBTU>           // monto fijo (ej. diario o mensual), fuera del qtyMMBtu
  }


// ========================================================
// 3) TRADE / COMERCIALIZACIÓN (cambia dueño, no cambia qtyMMBtu)
// ========================================================
type TradeParams =
  { seller      : Party
    buyer       : Party
    adder       : decimal<USD/MMBTU>        // $/MMBtu (fee/adder)
    contractRef : Contract}

// ========================================================
// 4) CONSUMO (sale del sistema; calcula desbalance vs medido)
// ========================================================
type ConsumeParams =
  { meterLocation : Location
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