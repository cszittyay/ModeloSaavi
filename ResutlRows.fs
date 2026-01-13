module ResultRows
open System
open Tipos




type TransportResultRow = {
  runId     : int
  gasDay    : DateOnly
  flowDetailId : FlowDetailId
  transactionId :TransactionId
  pipeline  : Pipeline
  fuelMode  : FuelMode
  routeId   : RouteId
  qtyIn     : Energy
  qtyOut    : Energy
  fuelQty   : Energy
}


type SleeveResultRow = {
  runId     : int
  gasDay    : DateOnly
  flowDetailId : FlowDetailId
  transactionId :TransactionId
  locationId    : LocationId
  sleeveSide  : SleeveSide

  qty         : Energy
  price       : EnergyPrice
  indexPrice  : EnergyPrice
  adder       : EnergyPrice
}


type ConsumeResultRow = {
  runId     : int
  gasDay    : DateOnly
  flowDetailId : FlowDetailId
  providerId   : EntidadLegalId
  locationId   : LocationId
  qtyAsigned    : Energy
}

type SupplyResultRow = {
  runId:int; 
  gasDay:DateOnly; 
  flowDetailId : FlowDetailId
  transactionId :TransactionId
  temporalidad:Temporalidad
  buyBack: bool
  seller:string; 
  qty   :Energy; 
  index :EnergyPrice; 
  adder :EnergyPrice; 
  price :EnergyPrice
}


type TradeResultRow = {
  runId     : int
  gasDay    : DateOnly
  flowDetailId : FlowDetailId
  transactionId :int
  sellerId     : EntidadLegalId
  buyerId      : EntidadLegalId
  locationId   : LocationId
  qty         : Energy
  adder       : EnergyPrice
  price       : EnergyPrice
}

type SellResultRow = {
  runId     : int
  gasDay    : DateOnly
  flowDetailId : FlowDetailId
  ventaGasId  : VentaGasId
  locationId    : LocationId
  sellerId      : EntidadLegalId
  buyerId       : EntidadLegalId
  qty         : Energy
  price       : EnergyPrice
  adder       : EnergyPrice
}



type ProjectedRows = {
  supplies   : SupplyResultRow list
  trades     : TradeResultRow list
  sells      : SellResultRow list
  transports : TransportResultRow list
  sleeves    : SleeveResultRow list
  consumes   : ConsumeResultRow list
}
