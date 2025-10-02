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

// Estado físico/contractual del gas en un punto de la cadena
type GasState =
  { qtyMMBtu  : Energy  // cantidad física de gas
    owner     : Party
    location  : Location
    ts        : DateTime 
    contract  : Contract } // p.ej. NAESB/TC, transporte, etc.

// Línea de costo/factura que produce cada operación
type CostItem =
  { kind     : string               // "GAS", "TRANSPORTE-USO", "TRANSPORTE-RESERVA", "FEE-TRADE", "PENALIZACIÓN", etc.
    qtyMMBtu : Energy               // sobre qué energía se cobra
    rate     : decimal<USD/MMBTU>   // tarifa o adder
    amount   : Money                // importe = qtyMMBtu * rate, o directo
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


// Estado "observable" de un storage (pasado/retornado por quien compone)
type StorageState =
  { location            : Location
    inv            : decimal<MMBTU>        // inventario actual
    invMax         : decimal<MMBTU>        // capacidad máxima
    injMax         : decimal<MMBTU>        // tasa máxima de inyección (por periodo)
    wdrMax         : decimal<MMBTU>        // tasa máxima de retiro (por periodo)
    injEfficiency  : decimal               // 0.0..1.0
    wdrEfficiency  : decimal               // 0.0..1.0
    usageRateInj   : decimal<USD/MMBTU>        // $/MMBtu inyectado efectivo
    usageRateWdr   : decimal<USD/MMBTU>        // $/MMBtu retirado
    demandCharge   : Money option        // fijo por periodo (si aplica)
    carryAPY       : decimal       // costo financiero anual por inventario (%)
  }

type InjectParams =
  { storage   : StorageState
    qtyIn     : decimal<MMBTU> }

type WithdrawParams =
  { storage   : StorageState
    qtyOut    : decimal<MMBTU> }     // qtyMMBtu deseada a extraer del storage hacia el pipeline

type CarryParams =
  { storage   : StorageState
    days      : int }                       // días del periodo a prorratear
