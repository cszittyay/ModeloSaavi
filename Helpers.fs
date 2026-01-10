module Helpers

open System
open System.Data
open Tipos
open Unidades


// === Helpers sugeridos
module Money =
  let inline amount (qty: Energy) (rate: EnergyPrice) : Money =    qty * rate 

module Display =
  let moneyStr (m: Money) = (decimal m).ToString("0.##")
  let rateStr  (r: EnergyPrice) = (decimal r).ToString("0.###")
  let qtyStr   (q: Energy) = (decimal q).ToString("0.###")

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


  let require<'T> (k:string) (m:Map<string,obj>) : Result<'T, DomainError> =
    match tryGet<'T> k m with
    | Some v -> Ok v
    | None -> Error (DomainError.Other $"Missing or invalid notes['{k}']")

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


let do2dt (dateOnly:DateOnly) = dateOnly.ToDateTime(TimeOnly.MinValue)
 



module FlowBuilderUtils =
    open DefinedOperations

    let opOfBlock (b: Block) : Operation =
        match b with
        | Supply sp       -> Supply.supply sp
        | SupplyMany sps  -> Supply.supplyMany sps
        | Sell sp         -> Sell.sell sp
        | Transport p     -> Transport.transport p
        | Trade p         -> Trade.trade p
        | Sleeve p        -> Sleeve.sleeve p  
        | Consume p       -> Consume.consume p
        | SellMany sp     -> Sell.sellMany sp
    
    let withStepMeta (step: FlowStep) (tr: Transition) =
      let fid = step.flowId
      let opName =
        match step.block with
        | Supply _      -> "supply"
        | SupplyMany _  -> "supplyMany"
        | Sell _        -> "sell"
        | SellMany _    -> "sellMany"
        | Trade _       -> "trade"
        | Transport _   -> "transport"
        | Sleeve _      -> "sleeve"
        | Consume _     -> "consume"

      let notes =
        tr.notes
        |> Map.add "op"     (box opName)
        |> Map.add "modo"   (box fid.modo)
        |> Map.add "central"(box fid.central)
        |> Map.add "path"   (box fid.path)
        |> Map.add "order"  (box step.order)
        |> Map.add "ref"    (box step.ref)
      { tr with notes = notes }


    /// Ejecuta secuencialmente un path lineal de `FlowStep`, aplicando el `Block` de cada step
    /// sobre un `State` y acumulando las `Transition` producidas.
    ///
    /// Contrato:
    /// - Entrada: lista ordenada de steps (por `order`) + estado inicial.
    /// - Salida: estado final + lista de transiciones (una por step ejecutado) o un DomainError.
    ///
    /// Responsabilidad:
    /// - Motor puro de ejecución lineal (no conoce topología Join/Linear).
    /// - No persiste en DB. No interpreta Excel. No clasifica roles.
    /// - Devuelve suficiente información (`Transition list`) para proyecciones posteriores
    ///   (auditoría, valorización, persistencia en SQL Server, etc).
    ///
    /// Observabilidad:
    /// - Puede imprimir trazas (printfn) durante la ejecución, pero esas trazas no son un “resultado”.
    /// - Para persistencia robusta, se recomienda enriquecer `Transition.notes` con metadatos del step
    ///   (flowId/order/ref/op) y con resultados operativos (qtyIn/qtyOut/fuel, etc) desde cada `op`.
    let runSteps (steps: FlowStep list) (initial: State)
        : Result<State * Transition list, DomainError> =

        // Estado acumulador: Result con (State actual, lista de transiciones ya generadas)
        // Comienza con Ok(initial, []) y va “encadenando” cada step.
        let flowStateList = (Ok (initial, []), steps)

        // (||>) = pipe-backward de 2 argumentos:
        // (state0, steps) ||> List.fold f  ==  List.fold f state0 steps
        flowStateList ||> List.fold (fun acc step ->

            // 1) Cortocircuito de error:
            //    Si acc = Error e, no ejecuta más steps.
            //    Si acc = Ok(st, ts), ejecuta el step actual.
            acc
            |> Result.bind (fun (st, ts) ->

                // 2) Resolver el “operador” (función de negocio) a partir del Block.
                //    op : State -> Result<Transition, DomainError>
                let op = opOfBlock step.block

                // 3) Logging/tracing (no afecta el resultado):
                //    Muestra estado previo + tipo de operación.
                printfn $"Energy:{Display.qtyStr st.energy}MMBTU\tOwner: {st.owner}\tLocation: {st.location}\n\n"

                match step.block with
                | Supply sp       -> printfn $"Supply {sp}"
                | SupplyMany sps  -> printfn $"SupplyMany {sps}"
                | Sell sp         -> printfn $"Sell {sp}"
                | Trade p         -> printfn $"Trade {p}"
                | Transport p     -> printfn $"Transport {p}"
                | Sleeve p        -> printfn $"Sleeve {p}"
                | Consume p       -> printfn $"Consume {p}"
                | SellMany sp     -> printfn $"SellMany {sp}"

                // 4) Ejecutar la operación sobre el estado actual.
                //    Si falla: Error -> se corta el fold.
                //    Si ok: produce Transition (tr) con state nuevo + costos/notas.
                op st |> Result.map (fun tr ->
                                let tr = withStepMeta step tr
                                tr.state, ts @ [tr]
                         )

                
            )
        )



    /// Ejecuta un Flow de topología JOIN a partir de paths ya clasificados
    /// por rol (`Contributor` | `Final`) y produce el estado agregado final
    /// junto con todas las transiciones generadas.
    ///
    /// Responsabilidad:
    /// - Ejecuta cada path Contributor de forma independiente desde su estado inicial.
    /// - Agrega el estado en el punto JOIN identificado por `joinKey`.
    /// - Continúa la ejecución por el path Final (si existe) a partir del estado agregado.
    /// - NO infiere topología ni roles; asume que `paths` ya están correctamente clasificados.
    ///
    /// Semántica del JOIN:
    /// - Solo los paths con rol `Contributor` aportan a la agregación del JOIN.
    /// - La agregación consiste en:
    ///     * suma de `State.energy` mediante `addEnergy` / `zeroEnergy`.
    /// - El path `Final` NO participa de la agregación y se ejecuta
    ///   exclusivamente después del JOIN.
    ///
    /// Invariantes asumidas:
    /// - Existe al menos un path Contributor.
    /// - Existe a lo sumo un path Final.
    /// - Todo Contributor finaliza en un `FlowStep` con `joinKey`.
    /// - El path Final (si existe) comienza en un `FlowStep` con `joinKey`.
    ///
    /// Consideraciones de diseño:
    /// - La suma de energía se parametriza para no asumir estructura algebraica
    ///   concreta del tipo `Energy`.
    /// - La función no crea ni elimina costos: preserva exactamente los `ItemCost`
    ///   producidos por cada operación.
    /// - No introduce transiciones sintéticas del JOIN; las transiciones devueltas
    ///   corresponden únicamente a ejecuciones reales de paths.
    ///
    /// Errores:
    /// - Propaga errores de `runPath`.
    /// - Devuelve `DomainError` ante violaciones de invariantes del JOIN.
    ///
    /// Esta función implementa exclusivamente la semántica operacional del JOIN
    /// y debe ser invocada únicamente por `runFlow`, nunca directamente
    /// desde la capa de parsing de la SheetFlow.

    let runJoinNPaths
        (joinKey       : string)
        (paths         : Map<FlowId, FlowPath>)
        (initialByPath : Map<FlowId, State>)
        (zeroEnergy    : Energy)
        (addEnergy     : Energy -> Energy -> Energy)
        (runPath       : FlowStep list -> State -> Result<State * Transition list, DomainError>)
        : Result<State * Transition list, DomainError> =

        let err msg = Error (DomainError.Other msg)

        let contributors =
            paths
            |> Map.toList
            |> List.choose (fun (_fid, p) -> if p.role = Contributor then Some p else None)

        let finals =
            paths
            |> Map.toList
            |> List.choose (fun (_fid, p) -> if p.role = Final then Some p else None)

        let endsAtJoin (steps: FlowStep list) =
            steps |> List.tryLast |> Option.bind (fun s -> s.joinKey) = Some joinKey

        let startsAtJoin (steps: FlowStep list) =
            steps |> List.tryHead |> Option.bind (fun s -> s.joinKey) = Some joinKey

        let tryInitial (fid: FlowId) =
            match initialByPath |> Map.tryFind fid with
            | Some st -> Ok st
            | None -> err $"Missing initial state for path {fid}"

        let runContributor (p: FlowPath) =
            tryInitial p.id
            |> Result.bind (fun st0 ->
                runPath p.steps st0
                |> Result.map (fun (stEnd, ts) -> (p.id, stEnd, ts))
            )

        // ---- validations ----
        match contributors with
        | [] -> err "runJoinNPaths: must have at least one Contributor path"
        | _ ->
            match finals with
            | _::_::_ -> err "runJoinNPaths: at most one Final path is allowed"
            | _ ->
                match contributors |> List.tryFind (fun p -> not (endsAtJoin p.steps)) with
                | Some p -> err $"Contributor path {p.id} does not END at joinKey='{joinKey}'"
                | None ->
                    match finals |> List.tryHead with
                    | Some pFinal when not (startsAtJoin pFinal.steps) ->
                        err $"Final path {pFinal.id} does not START at joinKey='{joinKey}'"
                    | _ ->

                        // ---- run all contributors ----
                        let rec runAll acc = function
                            | [] -> Ok (List.rev acc)
                            | p::ps ->
                                runContributor p
                                |> Result.bind (fun r -> runAll (r::acc) ps)

                        runAll [] contributors
                        |> Result.bind (fun partials ->

                            // partials: (FlowId * State * Transition list) list
                            let (_fid0, st0, _ts0) = partials |> List.head

                            // 1) sumar energías (solo contributors)
                            let energyTotal =
                                partials
                                |> List.filter (fun (_fid, _st, _ts) -> paths.[_fid].role = Contributor)
                                |> List.fold (fun acc (_fid, st, _ts) -> addEnergy acc st.energy) zeroEnergy

                            // 2) concatenar transitions (y por ende se preservan los costos internos)
                            let tsContrib =
                                partials
                                |> List.collect (fun (_fid, _st, ts) -> ts)

                            // 3) (opcional pero típico) agregar un Transition "JOIN" con costos agregados
                            //    - concatena TODOS los ItemCost producidos por contributors
                            let joinCosts =
                                tsContrib
                                |> List.collect (fun t -> t.costs)

                            // Nota: definimos el estado "agregado" del join
                            let stJoin =
                                { st0 with energy = energyTotal }

                            // Transition sintética de JOIN (si no querés crear una transición extra, eliminá esto)
                            let joinTransition : Transition =
                                { state = stJoin
                                  costs = joinCosts
                                  notes = Map.empty }  // o Meta.set "op" "join" etc.

                            // Si preferís NO duplicar costos (porque ya están dentro de tsContrib),
                            // entonces NO agregues joinTransition y devolvé tsContrib tal cual.
                            //
                            // En la práctica, hay dos estrategias:
                            // A) tsContrib (contiene costos por operación, sin costo "join")
                            // B) tsContrib @ [joinTransition] (incluye resumen)
                            //
                            // Acá elijo A por seguridad (no duplica).
                            let tsUpToJoin = tsContrib

                            // ---- run Final if present ----
                            match finals |> List.tryHead with
                            | None ->
                                Ok (stJoin, tsUpToJoin)

                            | Some pFinal ->
                                runPath pFinal.steps stJoin
                                |> Result.map (fun (stFinal, tsFinal) ->
                                    (stFinal, tsUpToJoin @ tsFinal)
                                )
                        )



    /// Construye la topología del Flow a partir de los paths ya materializados.
    ///
    /// Regla principal:
    /// - Si ningún FlowStep posee joinKey, el Flow es LINEAL.
    /// - Si existe exactamente un joinKey en el conjunto, el Flow es de tipo JOIN.
    /// - Múltiples joinKey distintos en el mismo Flow se consideran inválidos.
    ///
    /// Invariantes que valida:
    /// - Flow LINEAL:
    ///   - Debe existir exactamente un path.
    /// - Flow JOIN:
    ///   - Debe existir al menos un path Contributor (termina en joinKey).
    ///   - Puede existir a lo sumo un path Final (comienza en joinKey).
    ///   - Todo path debe ser clasificable como Contributor o Final respecto al joinKey.
    ///
    /// Esta función NO ejecuta el Flow.
    /// Su responsabilidad es puramente estructural y semántica:
    /// detectar la topología y tiparla explícitamente para el motor de ejecución.
    ///
    /// Errores devueltos:
    /// - Flow sin joinKey y con múltiples paths.
    /// - Flow con múltiples joinKey distintos.
    /// - Paths que no encajan en la topología esperada (ni Contributor ni Final).

    let buildFlowDef (paths: Map<FlowId, FlowStep list>) : Result<FlowDef, DomainError> =
      let err msg = Error (DomainError.Other msg)

      let allJoinKeys =
        paths
        |> Map.toList
        |> List.collect (fun (_fid, steps) -> steps |> List.choose (fun s -> s.joinKey))
        |> List.distinct

      let endsAtJoin (jk: string) (steps: FlowStep list) =
        if steps.Length = 1 then false else steps |> List.tryLast |> Option.bind (fun s -> s.joinKey) = Some jk

      let startsAtJoin (jk: string) (steps: FlowStep list) =
        steps |> List.tryHead |> Option.bind (fun s -> s.joinKey) = Some jk

      match allJoinKeys with
      | [] ->
          match paths |> Map.toList with
          | [ (fid, steps) ] -> Ok (FlowDef.Linear (fid, steps))
          | _ -> err "Flow lineal inválido: hay múltiples paths pero ningún JoinKey."
      | [ jk ] ->
          let flowPaths =
            paths
            |> Map.map (fun fid steps ->
                let role =
                  if endsAtJoin jk steps then Contributor
                  elif startsAtJoin jk steps then Final
                  else
                    // path participa del flow pero no encaja en la topología esperada
                    // (p.ej. tiene el joinKey en el medio, o nunca lo toca)
                    // si querés permitir join en el medio, se puede extender para "split" del path.
                    failwith $"Path {fid} no es Contributor (end) ni Final (start) para joinKey='{jk}'."

                { id = fid; role = role; steps = steps }
            )

          // Validación: a lo sumo un Final
          let finalsCount =
            flowPaths |> Map.toList |> List.sumBy (fun (_,p) -> if p.role = Final then 1 else 0)

          if finalsCount > 1 then
            err $"Flow inválido: hay {finalsCount} paths Final para joinKey='{jk}' (debe haber a lo sumo 1)."
          else
            Ok (FlowDef.Join (jk, flowPaths))

      | _ ->
          err $"Flow inválido: hay múltiples JoinKey en el mismo Flow: {allJoinKeys}."






    /// Ejecuta un Flow previamente tipado (`FlowDef`) y devuelve el estado final
    /// junto con la secuencia completa de transiciones producidas.
    ///
    /// Responsabilidad:
    /// - Actúa como dispatcher semántico entre las distintas topologías de Flow.
    /// - NO interpreta la SheetFlow ni infiere estructura; asume que `flow` ya
    ///   cumple todas las invariantes topológicas validadas por `buildFlowDef`.
    ///
    /// Comportamiento por topología:
    /// - Linear:
    ///   - Ejecuta el único path de forma secuencial usando `runPath`.
    /// - Join:
    ///   - Ejecuta todos los paths Contributor de manera independiente
    ///     (cada uno desde `initial`).
    ///   - Agrega el estado en el punto JOIN:
    ///       * suma la energía usando `addEnergy` / `zeroEnergy`.
    ///   - Continúa la ejecución por el path Final si existe,
    ///     partiendo del estado agregado del JOIN.
    ///   - El path Final NO contribuye a la agregación del JOIN.
    ///
    /// Consideraciones de diseño:
    /// - La suma de energía se parametriza para no asumir estructura algebraica
    ///   concreta del tipo `Energy`.
    /// - Las transiciones devueltas preservan el orden lógico de ejecución
    ///   (contributors → final, si existe).
    /// - La función no introduce transiciones sintéticas salvo que el motor
    ///   aguas abajo lo requiera explícitamente.
    ///
    /// Errores:
    /// - Propaga errores de `runPath`.
    /// - Propaga errores de consistencia detectados durante la ejecución del JOIN.
    ///
    /// Esta función constituye el único punto de entrada al motor de ejecución
    /// del Flow, separando claramente:
    ///   * construcción topológica (buildFlowDef)
    ///   * ejecución operacional (runPath*

    let runFlow
        (flow         : FlowDef)
        (initial      : State)
        (zeroEnergy   : Energy)
        (addEnergy    : Energy -> Energy -> Energy)
        (runPath      : FlowStep list -> State -> Result<State * Transition list, DomainError>)
        : Result<State * Transition list, DomainError> =

      match flow with
      | FlowDef.Linear (_fid, steps) ->   runPath steps initial

      | FlowDef.Join (joinKey, flowPaths) ->
          let initialByPath =
            flowPaths |> Map.map (fun _ _ -> initial)

          runJoinNPaths
            joinKey
            flowPaths
            initialByPath
            zeroEnergy
            addEnergy
            runPath



