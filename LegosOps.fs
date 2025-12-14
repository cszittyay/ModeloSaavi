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
