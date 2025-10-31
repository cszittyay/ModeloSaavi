module Helpers

open System
open System.Data
open Tipos
open Unidades


// === Helpers sugeridos
module Money =
  let inline amount (qty: Energy) (rate: RateGas) : Money =    qty * rate 


module DomainError =

  let msg = function
    | QuantityNonPositive w -> $"Cantidad no positiva en {w}"
    | MissingContract id    -> $"Falta contrato {id}"
    | CapacityExceeded w    -> $"Capacidad excedida: {w}"
    | InvalidUnits d        -> $"Unidades inválidas: {d}"
    | Other s               -> s


module DomainErrorHelpers =
    open Tipos  // donde está definido type DomainError

    let describe (err: DomainError) : string =
        match err with
        | QuantityNonPositive where_ ->
            sprintf "[QuantityNonPositive] %s → cantidad <= 0" where_
        | MissingContract id ->
            sprintf "[MissingContract] Faltante: %s" id
        | CapacityExceeded what ->
            sprintf "[CapacityExceeded] Capacidad excedida en %s" what
        | InvalidUnits detail ->
            sprintf "[InvalidUnits] %s" detail
        | Other msg ->
            sprintf "[Other] %s" msg



module Display =
    let moneyStr (m: Money) = (decimal m).ToString("0.##")
    let rateStr  (r: RateGas) = (decimal r).ToString("0.##")
    let qtyStr   (q: Energy) = (decimal q).ToString("0.##")

module Domain =
  let inline amount (qty: Energy) (rate: RateGas) : Money =   qty * rate

  let weightedAvgRate (legs: SupplierLeg list) : RateGas =
    let qty = legs |> List.sumBy (fun l -> l.tc.qtyMMBtu)
    if qty = 0.0m<MMBTU> then 0.0m<USD/MMBTU>
    else
      let amt = legs |> List.sumBy (fun l -> amount l.tc.qtyMMBtu l.tc.price)
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

  /// Reglas mínimas para consolidar: mismo buyer, mismo gasDay y mismo deliveryPt.
  let legsConsolidados (legs: SupplierLeg list) =
    match legs with
    | [] -> Error EmptyLegs
    | x::xs ->
      let b = x.tc.buyer
      let d = x.tc.gasDay
      let p = x.tc.deliveryPt
      let okBuyer    = xs |> List.forall (fun l -> l.tc.buyer     = b)
      let okGasDay   = xs |> List.forall (fun l -> l.tc.gasDay    = d)
      let okDelivPt  = xs |> List.forall (fun l -> l.tc.deliveryPt = p)
      let okQty      = x::xs |> List.forall (fun l -> l.tc.qtyMMBtu > 0.0m<MMBTU>)
      if not okBuyer   then Error (BuyerMismatch (b, xs |> List.tryPick (fun l -> Some l.tc.buyer) |> Option.defaultValue "<desconocido>"))
      elif not okGasDay then Error GasDayMismatch
      elif not okDelivPt then Error DeliveryPtMismatch
      elif not okQty    then Error (NonPositiveQty (x.tc.seller))
      else Ok (b,d,p)



module Meta =
  /// Devuelve Some t si la clave existe y el tipo coincide; si no, None
    let tryGet<'T> (k: string) (m: Map<string, obj>) : 'T option =
        match Map.tryFind k m with
        | Some v ->
            match v with
            | :? 'T as t -> Some t
            | _ -> None
        | None -> None

    let set (k: string) (v: obj) (m: Map<string, obj>) =
        Map.add k v m


/// Chequea un balance contra una tolerancia
    let check (eps: Energy) (b: DailyBalance) : bool =
        let lhs = b.buy + b.withdraw
        let rhs = b.sell + b.inject + b.consume
        abs(lhs - rhs) > eps     // true si hay desbalance


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
               let qty = t.state.qtyMMBtu
               let op  = tryGet<string> "op" t.notes
               applyOp op qty acc
             ) zero)

