module LegosOps

module LegoOps

open Tipos
open Supply
open Trade
open Transport
open Consume

/// Bloques atómicos de la cadena física/comercial
type Block =
  | SupplyFromTc      of TransactionConfirmation
  | SupplyMany        of TransactionConfirmation list
  | MultiSupplyTrade  of MultiSupplyTradeParams
  | Transport         of TransportParams
  | Trade             of TradeParams
  | Consume           of ConsumeParams

/// Compilar una lista de bloques a Operation list
let compile (blocks: Block list) : Operation list =
  blocks
  |> List.map (function
      | SupplyFromTc tc        -> supplyFromTc tc
      | SupplyMany  tcs        -> supplyMany  tcs
      | MultiSupplyTrade p     -> multiSupplyTrade p
      | Transport p            -> transport p
      | Trade p                -> trade p
      | Consume p              -> consume p)
