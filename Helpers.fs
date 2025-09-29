module Helpers

open System
open System.Data
open Tipos

// Helpers
let inline d (x: decimal) : decimal = decimal x
//let round2 (m: Money) = Math.Round(m, 2)

// merge utils
let mergeMaps (m1: Map<string,obj>) (m2: Map<string,obj>) : Map<string,obj> =
    Map.fold (fun acc k v -> acc.Add(k,v)) m1 m2


let merge (r1: OpResult) (r2: OpResult) : OpResult =
  { state = r2.state
    costs = r1.costs @ r2.costs
    notes = mergeMaps r1.notes r2.notes }

let (>>>) (op1: Operation) (op2: Operation) : Operation =
  fun st ->
    match op1 st with
    | Error e -> Error e
    | Ok r1 ->
      match op2 r1.state with
      | Error e -> Error e
      | Ok r2 -> Ok (merge r1 r2)

let run (ops: Operation list) (init: GasState) =
  ops |> List.fold (fun acc op ->
          match acc with
          | Error _ as e -> e
          | Ok r ->
            match op r.state with
            | Error e -> Error e
            | Ok r2 -> Ok (merge r r2))
        (Ok { state=init; costs=[]; notes=Map.empty })


