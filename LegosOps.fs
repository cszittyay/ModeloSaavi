module LegosOps


open Tipos
open Unidades
open DefinedOperations
open DefinedOperations.Supply
open DefinedOperations.Consume
open DefinedOperations.Transport    
open DefinedOperations.Trade
open DefinedOperations.Sleeve
open DefinedOperations.Sell

/// Bloques atómicos de la cadena física/comercial
type Block =
  | Consume           of ConsumeParams
  | Sell              of SellParams
  | Supply            of SupplyParams
  | SupplyMany        of SupplyParams list
  | Transport         of TransportParams
  | Trade             of TradeParams
  | Sleeve            of SleeveParams
  
/// Compilar una lista de bloques a Operation list
let compile (blocks: Block list) : Operation list =
  blocks
  |> List.map (function
      | Supply sp              -> supply sp
      | SupplyMany  sps        -> supplyMany  sps
      | Sell sp                -> sell sp
      | Transport p            -> transport p
      | Trade p                -> trade p
      | Sleeve p               -> sleeve p  
      | Consume p              -> consume p)
