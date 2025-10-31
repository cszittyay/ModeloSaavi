module Kleisli
open Tipos


/// Kleisli composition: encadena Operations
let (>=>) (f: Operation) (g: Operation) : Operation =
  fun s0 ->
    match f s0 with
    | Error e -> Error e
    | Ok t1   -> g t1.state

/// Convierte una Operation en un "plan" que acumula transiciones
type Plan = State -> Result<Transition list, DomainError>

let planOf (op: Operation) : Plan =
  fun s -> op s |> Result.map List.singleton

/// Encadena un Plan con una Operation, acumulando la nueva transición al final
let (>>+) (p: Plan) (op: Operation) : Plan =
  fun s0 ->
    match p s0 with
    | Error e -> Error e
    | Ok ts ->
      let st = (List.last ts).state
      match op st with
      | Error e -> Error e
      | Ok t    -> Ok (ts @ [t])

/// Ejecuta una secuencia de Operations y devuelve la lista de transiciones
let runAll (ops: Operation list) : Plan =
  fun s0 ->
    let folder acc op =
      match acc with
      | Error e -> Error e
      | Ok []   ->
        op s0 |> Result.map (fun t -> [t])
      | Ok ts   ->
        let st = (List.last ts).state
        op st  |> Result.map (fun t -> ts @ [t])
    List.fold folder (Ok []) ops


