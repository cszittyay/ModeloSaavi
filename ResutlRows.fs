module ResultRows
open System
open Tipos



type TransportResultRow = {
  runId     : int
  gasDay    : DateOnly
  modo      : string
  central   : string
  path      : string
  order     : int
  ref       : string option

  provider  : Party
  pipeline  : Pipeline
  entry     : Location
  exit      : Location
  shipper   : Party
  fuelMode  : FuelMode
  fuelPct   : decimal

  qtyIn     : Energy
  qtyOut    : Energy
  fuelQty   : Energy

  usageRate   : EnergyPrice
  reservation : EnergyPrice
  acaRate     : EnergyPrice
}


type SleeveResultRow = {
  runId     : int
  gasDay    : DateOnly
  modo      : string
  central   : string
  path      : string
  order     : int
  ref       : string option

  provider    : Party
  seller      : Party
  buyer       : Party
  location    : Location
  sleeveSide  : SleeveSide

  qty         : Energy
  price       : EnergyPrice
  index       : int
  adder       : EnergyPrice
  contractRef : Contract
}


type ConsumeResultRow = {
  runId     : int
  gasDay    : DateOnly
  modo      : string
  central   : string
  path      : string
  order     : int
  ref       : string option

  provider      : Party
  meterLocation : Location
  qtyConsume    : Energy
  measured      : Energy
}

type SupplyResultRow = {
  runId:int; 
  gasDay:DateOnly; 
  modo:string; 
  central:string; 
  path:string; 
  order:int; 
  ref:string option
  legNo:int
  tcId:string; 
  tradingHub:TradingHub; 
  temporalidad:Temporalidad; 
  deliveryPt:Location
  seller:string; 
  buyer:string; 
  qty:Energy; 
  index:EnergyPrice; 
  adder:EnergyPrice; 
  price:EnergyPrice
  contractRef:string
}


type TradeResultRow = {
  runId     : int
  gasDay    : DateOnly
  modo      : string
  central   : string
  path      : string
  order     : int
  ref       : string option

  side        : TradeSide
  seller      : Party
  buyer       : Party
  location    : Location
  adder       : EnergyPrice
  contractRef : Contract
}

type SellResultRow = {
  runId     : int
  gasDay    : DateOnly
  modo      : string
  central   : string
  path      : string
  order     : int
  ref       : string option

  location    : Location
  seller      : Party
  buyer       : Party
  qty         : Energy
  price       : EnergyPrice
  adder       : EnergyPrice
  contractRef : Contract
}



type ProjectedRows = {
  supplies   : SupplyResultRow list
  trades     : TradeResultRow list
  sells      : SellResultRow list
  transports : TransportResultRow list
  sleeves    : SleeveResultRow list
  consumes   : ConsumeResultRow list
}
