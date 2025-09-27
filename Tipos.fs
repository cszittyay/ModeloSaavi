module Tipos

open System
open Unidades

// ===== Tipos base =====
type Energy = float<mmbtu>   // MMBtu (puedes cambiar por GJ y agregar conversión)
type Money  = decimal // USD (o MXN si prefieres)

// Identificadores “de negocio”
type Party    = string
type Location = string
type Contract = string

// Estado físico/contractual del gas en un punto de la cadena
type GasState =
  { qtyMMBtu  : Energy  // cantidad física de gas
    owner     : Party
    location  : Location
    ts        : DateTime option
    contract  : Contract option } // p.ej. NAESB/TC, transporte, etc.

// Línea de costo/factura que produce cada operación
type CostItem =
  { kind     : string          // "GAS", "TRANSPORTE-USO", "TRANSPORTE-RESERVA", "FEE-TRADE", "PENALIZACIÓN", etc.
    qtyMMBtu : Energy   // sobre qué energía se cobra
    rate     : Money option    // tarifa o adder
    amount   : Money           // importe = qty * rate, o directo
    currency : Currency           // "USD" / "MXN"
    meta     : Map<string,obj> }

// Resultado de una operación
type OpResult =
  { state   : GasState
    costs   : CostItem list
    notes   : Map<string,obj> }  // detalles (fuel, desbalance, shipper, etc.)

// Operación = función pura de estado -> resultado (o error)
type Operation = GasState -> Result<OpResult,string>
// ========================================================
// 1) SUPPLY / SUMINISTRO (cambio de dueño, sin mover físico)
// ========================================================

type SupplyParams =
  { seller      : Party
    buyer       : Party
    priceFix    : Money option    // precio del gas ($/MMBtu) o monto fijo (ver meta)
    contractRef : Contract option }




// ========================================================
// 2) TRANSPORTE (mueve físico, no cambia dueño)
// ========================================================
type TransportParams =
  { entry       : Location
    exit        : Location
    shipper     : Party
    fuelPct     : float
    usageRate   : Money          // $/MMBtu sobre salida
    reservation : Money option   // monto fijo (ej. diario o mensual), fuera del qty
  }


// ========================================================
// 3) TRADE / COMERCIALIZACIÓN (cambia dueño, no cambia qty)
// ========================================================
type TradeParams =
  { seller      : Party
    buyer       : Party
    adder       : Money        // $/MMBtu (fee/adder)
    currency    : string
    contractRef : Contract option }

// ========================================================
// 4) CONSUMO (sale del sistema; calcula desbalance vs medido)
// ========================================================
type ConsumeParams =
  { meterLocation : Location
    measured      : float<mmbtu>
    currency      : Currency
    penaltyRate   : Money option
    tolerancePct  : float }


// Estado "observable" de un storage (pasado/retornado por quien compone)
type StorageState =
  { loc            : Location
    inv            : float<mmbtu>        // inventario actual
    invMax         : float<mmbtu>        // capacidad máxima
    injMax         : float<mmbtu>        // tasa máxima de inyección (por periodo)
    wdrMax         : float<mmbtu>        // tasa máxima de retiro (por periodo)
    injEfficiency  : float               // 0.0..1.0
    wdrEfficiency  : float               // 0.0..1.0
    usageRateInj   : Money option        // $/MMBtu inyectado efectivo
    usageRateWdr   : Money option        // $/MMBtu retirado
    demandCharge   : Money option        // fijo por periodo (si aplica)
    carryAPY       : decimal option      // costo financiero anual por inventario (%)
  }

type InjectParams =
  { storage   : StorageState
    qtyIn     : float<mmbtu> }

type WithdrawParams =
  { storage   : StorageState
    qtyOut    : float<mmbtu> }     // qty deseada a extraer del storage hacia el pipeline

type CarryParams =
  { storage   : StorageState
    days      : int }                       // días del periodo a prorratear
