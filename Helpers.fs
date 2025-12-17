module Helpers

open System
open System.Data
open Tipos
open Unidades


// === Helpers sugeridos
module Money =
  let inline amount (qty: Energy) (rate: EnergyPrice) : Money =    qty * rate 

module DomainError =
  let msg = function
    | QuantityNonPositive w -> $"Cantidad no positiva en {w}"
    | MissingContract id    -> $"Falta contrato {id}"
    | CapacityExceeded w    -> $"Capacidad excedida: {w}"
    | InvalidUnits d        -> $"Unidades inválidas: {d}"
    | Other s               -> s

module DomainErrorHelpers =
  open Tipos
  let describe (err: DomainError) : string =
    match err with
    | QuantityNonPositive where_ -> sprintf "[QuantityNonPositive] %s → cantidad <= 0" where_
    | MissingContract id -> sprintf "[MissingContract] Faltante: %s" id
    | CapacityExceeded what -> sprintf "[CapacityExceeded] Capacidad excedida en %s" what
    | InvalidUnits detail -> sprintf "[InvalidUnits] %s" detail
    | Other msg -> sprintf "[Other] %s" msg

module Display =
  let moneyStr (m: Money) = (decimal m).ToString("0.##")
  let rateStr  (r: EnergyPrice) = (decimal r).ToString("0.###")
  let qtyStr   (q: Energy) = (decimal q).ToString("0.###")

module Domain =
  let inline amount (qty: Energy) (rate: EnergyPrice) : Money =   qty * rate
  let weightedAvgRate (sps: SupplyParams list)  : EnergyPrice =
    let qty = sps |> List.sumBy (fun sp -> sp.qEnergia)
    if qty = 0.0m<MMBTU> then 0.0m<USD/MMBTU>
    else
      let amt = sps |> List.sumBy (fun sp -> amount sp.qEnergia sp.price)
      (amt / qty)

module Validate =
  type Err =
    | EmptyLegs
    | BuyerMismatch of expected:Party * found:Party
    | GasDayMismatch
    | DeliveryPtMismatch
    | NonPositiveQty of Party
  
  let toString = function
    | EmptyLegs -> "No hay suppliers/legs para consolidar."
    | BuyerMismatch (e,f) -> $"Buyer inconsistente. Esperado={e} encontrado={f}"
    | GasDayMismatch -> "GasDay inconsistente entre legs."
    | DeliveryPtMismatch -> "DeliveryPoint inconsistente entre legs."
    | NonPositiveQty p -> $"Cantidad no positiva en leg del seller={p}"
  
  let legsConsolidados (sps: SupplyParams list) =
    match sps with
    | [] -> Error EmptyLegs
    | x::xs ->
      let b = x.buyer
      let d = x.gasDay
      let p = x.deliveryPt
      let okBuyer    = xs |> List.forall (fun sp -> sp.buyer     = b)
      let okGasDay   = xs |> List.forall (fun sp -> sp.gasDay    = d)
      let okDelivPt  = xs |> List.forall (fun sp -> sp.deliveryPt = p)
      if not okBuyer   then Error (BuyerMismatch (b, xs |> List.tryPick (fun sp -> Some sp.buyer) |> Option.defaultValue "<desconocido>"))
      elif not okGasDay then Error GasDayMismatch
      elif not okDelivPt then Error DeliveryPtMismatch
      else Ok (b,d,p)

module Meta =
  /// Devuelve Some t si la clave existe y el tipo coincide; si no, None (también soporta almacenar Option<'T>)
  let tryGet<'T> (k: string) (m: Map<string,obj>) : 'T option =
    match Map.tryFind k m with
    | Some v ->
      match v with
      | :? 'T as t -> Some t
      | :? ('T option) as opt -> opt
      | _ -> None
    | None -> None

  let set (k: string) (v: obj) (m: Map<string,obj>) = Map.add k v m

/// Chequea un balance contra una tolerancia
let check (eps: Energy) (b: DailyBalance) : bool =
  let lhs = b.buy + b.withdraw
  let rhs = b.sell + b.inject + b.consume
  abs(lhs - rhs) > eps

// Normaliza y aplica la operación al balance
let private applyOp (opOpt: string option) (qty: decimal<MMBTU>) (b: DailyBalance) =
  match opOpt with
  | Some "supply" | Some "supplyMany" | Some "buy" -> { b with buy      = b.buy      + qty }
  | Some "sell"                                     -> { b with sell     = b.sell     + qty }
  | Some "inject"                                   -> { b with inject   = b.inject   + qty }
  | Some "withdraw"                                 -> { b with withdraw = b.withdraw + qty }
  | Some "consume"                                  -> { b with consume  = b.consume  + qty }
  | _                                               -> b

/// Construye la lista de balances desde transiciones
let fromTransitions (ts: Transition list) : DailyBalance list =
  ts
  |> List.groupBy (fun t -> t.state.gasDay, t.state.location)
  |> List.map (fun ((d, h), group) ->
    let zero : DailyBalance =
      { fecha = d
        hub = h
        buy = 0.0m<MMBTU>; sell = 0.0m<MMBTU>
        inject = 0.0m<MMBTU>; withdraw = 0.0m<MMBTU>
        consume = 0.0m<MMBTU> }
    group
    |> List.fold (fun (acc: DailyBalance) (t: Transition) ->
         let qty = t.state.energy
         let op  = Meta.tryGet<string> "op" t.notes
         applyOp op qty acc
       ) zero)



 

