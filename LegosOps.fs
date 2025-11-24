module LegosOps


open Tipos
open Unidades
open DefinedOperations
open DefinedOperations.Supply
open DefinedOperations.Consume
open DefinedOperations.Transport    
open DefinedOperations.Trade
open DefinedOperations.Sleeve
open DefinedOperations.SupplyTrade

/// Bloques atómicos de la cadena física/comercial
type Block =
  | Consume           of ConsumeParams
  | Supply            of SupplyParams
  | SupplyMany        of SupplyParams list
  | MultiSupplyTrade  of MultiSupplyTradeParams
  | Transport         of TransportParams
  | Trade             of TradeParams
  | Sleeve            of SleeveParams
  
/// Compilar una lista de bloques a Operation list
let compile (blocks: Block list) : Operation list =
  blocks
  |> List.map (function
      | Supply sp              -> supply sp
      | SupplyMany  tcs        -> supplyMany  tcs
      | MultiSupplyTrade p     -> multiSupplyTrade p
      | Transport p            -> transport p
      | Trade p                -> trade p
      | Sleeve p               -> sleeve p  
      | Consume p              -> consume p)
