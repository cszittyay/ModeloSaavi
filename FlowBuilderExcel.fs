module FlowBuilderExcel

open System
open System.IO
open FsToolkit.ErrorHandling
open FSharp.Interop.Excel
open Tipos
open DefinedOperations
open LegosOps




[<Literal>]
let excelPath = @"C:\Users\cszit\source\repos\f#\Saavi\ComposeModel\ModeloSaavi\EscenarioSample.xlsx"

type FlowSheet         = ExcelFile<excelPath, "Flow", HasHeaders=true >
type SupplySheet       = ExcelFile<excelPath, "Supply", HasHeaders=true >
type SupplyTradeSheet  = ExcelFile<excelPath, "SupplyTrade", HasHeaders=true >
type TradeSheet        = ExcelFile<excelPath, "Trade", HasHeaders=true >
type TransportSheet    = ExcelFile<excelPath, "Transport", HasHeaders=true >
type SleeveSheet       = ExcelFile<excelPath, "Sleeve", HasHeaders=true >
type SellSheet         = ExcelFile<excelPath, "Sell", HasHeaders=true >
type ConsumeSheet      = ExcelFile<excelPath, "Consume", HasHeaders=true >



let getPrice  (index: decimal<USD/MMBTU>) 
              (adder: decimal<USD/MMBTU>) 
              (formula: string )
               : decimal<USD/MMBTU> =
                                        match formula with
                                        | "" -> index + adder
                                        | _     -> 0m<USD/MMBTU> // Aquí puedes implementar la lógica para evaluar la fórmula si es necesario

let loadSheets (path:string) =
    let flow             = new FlowSheet(path)
    let supply           = new SupplySheet(path)
    let supplyTrade      = new SupplyTradeSheet(path)
    let trade            = new TradeSheet(path)
    let transport        = new TransportSheet(path)
    let sleeve           = new SleeveSheet(path)
    let sell             = new SellSheet(path)
    let consume          = new ConsumeSheet(path)
    flow, supply, supplyTrade, trade, transport, sleeve, sell, consume

let parseGasDay (s:string) : GasDay =
    DateOnly.Parse s

let parseTradingHub = function
    | "Mainline" -> TradingHub.Mainline
    | "Waha"     -> TradingHub.Waha
    | "HSC"      -> TradingHub.HSC
    | "AguaDulce"-> TradingHub.AguaDulce
    | x          -> failwithf "TradingHub desconocido: %s" x

let parseTemporalidad = function
    | "DayAhead" -> Temporalidad.DayAhead
    | "Intraday" -> Temporalidad.Intraday
    | "Monthly"  -> Temporalidad.Monthly
    | x          -> failwithf "Temporalidad desconocida: %s" x



let buildSupplies modo central path (sheet: SupplySheet)  : Map<string,  SupplyParams seq> =
        sheet.Data
                |> Seq.filter (fun row -> row.Modo = modo && row.Path = path && row.Central = central && row.Name <> null && row.Name <> "")
                |> Seq.map (fun row ->
                    let index = decimal row.Index * 1.0m<USD/MMBTU>
                    let xadder = decimal row.Adder * 1.0m<USD/MMBTU>
                    let formula  = string row.Formula  

                    let sp : SupplyParams =
                      { tcId        = row.TcId
                        gasDay      = DateOnly.FromDateTime(row.GasDay)
                        tradingHub  = parseTradingHub row.TradingHub
                        temporalidad= parseTemporalidad row.Temporalidad
                        deliveryPt  = row.DeliveryPt
                        seller      = row.Seller
                        buyer       = row.Buyer
                        qEnergia    = decimal row.QEnergiaMMBTU * 1.0m<MMBTU>
                        index       = index                                        
                        adder       = xadder
                        price       = getPrice index xadder formula
                        contractRef = row.ContractRef
                        meta        = Map.empty }
                    row.Name, sp)
                |> Seq.groupBy fst 
                |> Seq.map (fun (name, rows) -> name, rows |> Seq.map snd)
                |> Map.ofSeq



let buildTrades modo central path (sheet: TradeSheet) : Map<string, TradeParams> =
    sheet.Data
    |> Seq.filter (fun row -> row.Modo = modo && row.Path = path && row.Central = central && row.Name <> null && row.Name <> "")
    |> Seq.map (fun row ->
        let tp : TradeParams =
          { side        = if row.Side = "Sell" then TradeSide.Sell else TradeSide.Buy
            location    = row.Location
            seller      = row.Seller
            buyer       = row.Buyer
            
            adder       = decimal row.Adder * 1.0m<USD/MMBTU>
            contractRef = row.ContractRef
            meta        = Map.empty }
        row.Name, tp)
    |> Map.ofSeq


let buildSleeves modo central path (sheet: SleeveSheet) : Map<string, SleeveParams> =
    sheet.Data
    |> Seq.filter (fun row -> row.Modo = modo && row.Path = path && row.Central = central &&   row.Name <> null && row.Name <> "")
    |> Seq.map (fun row ->

        let index = decimal row.Index * 1.0m<USD/MMBTU>
        let xadder = decimal row.Adder * 1.0m<USD/MMBTU>
        let formula  = string row.Formula  

        let sl : SleeveParams =
            {   provider    = row.Provider
                seller      = row.Seller
                buyer       = row.Buyer
                location    = row.Location
                sleeveSide  = if row.SleevSide = "Export" then SleeveSide.Export else SleeveSide.Import
                index       = index                                        
                adder       = xadder
                price       = getPrice index xadder formula
                contractRef = row.ContractRef
                meta        = Map.empty }
        row.Name, sl)
    |> Map.ofSeq



let buildTransports modo central path (sheet: TransportSheet) : Map<string, TransportParams> =
    sheet.Data
    |> Seq.filter (fun row -> row.Modo = modo && row.Path = path && row.Central = central && row.Name <> null && row.Name <> "")
    |> Seq.map (fun row ->
        let tp : TransportParams =
          { provider   = row.Provider
            pipeline   = row.Pipeline
            entry      = row.Entry
            exit       = row.Exit
            acaRate    = decimal row.AcaRate * 1.0m<USD/MMBTU>
            shipper    = row.Shipper
            fuelMode   = if row.FuelMode = "RxBase" then FuelMode.RxBase else FuelMode.ExBase
            fuelPct    = decimal row.FuelPct
            usageRate  = decimal row.UsageRateUSDMMBTU * 1.0m<USD/MMBTU>
            reservation= decimal row.ReservationUSDMMBTU * 1.0m<USD/MMBTU> 
            meta       = Map.empty }
        row.Name, tp)
    |> Map.ofSeq


let buildConsumes modo central path (sheet: ConsumeSheet) : Map<string, ConsumeParams> =
    let cd = sheet.Data |> Seq.filter (fun row -> row.Modo = modo && row.Path = path && row.Central = central && row.Name <> null && row.Name <> "") |> Seq.toList
    cd |> Seq.map (fun row ->
        let cp : ConsumeParams =
          { provider      = row.Provider
            meterLocation = row.Location
            measured      = decimal row.MeasuredMMBTU * 1.0m<MMBTU>
            tolerancePct  = decimal row.TolerancePct
            penaltyRate   = decimal row.PenaltyUSDMMBTU * 1.0m<USD/MMBTU> }
        row.Name, cp)
    |> Map.ofSeq


let buildSells cliente diaGas  (sheet: SellSheet) : Map<string, SellParams> =
    sheet.Data
    |> Seq.filter (fun row -> row.Client = cliente && DateOnly.FromDateTime(row.GasDay) = diaGas )
    |> Seq.map (fun row ->
        let sp: SellParams =
          {
            location    = row.Location
            gasDay      = DateOnly.FromDateTime(row.GasDay)
            seller      = row.Seller
            buyer       = row.Buyer 
            qty         = decimal row.QtyMMBTU * 1.0m<MMBTU>
            price       = decimal row.Price * 1.0m<USD/MMBTU>
            adder       = decimal row.Adder  * 1.0m<USD/MMBTU>      
            contractRef = row.ContractRef
            meta        = Map.empty
          }
        row.Name, sp)
    |> Map.ofSeq



let buildFlowSteps (pathExcel:string) modo central path diaGas: FlowStep list =
    let flowSheet, supplySheet, supplyTrade, tradeSheet, transportSheet, sbyteSheet,  sellSheet, consumeSheet = loadSheets pathExcel

    let cliente = "ClienteX"

    let supplies   = buildSupplies modo central path supplySheet
    let trades     = buildTrades modo central path tradeSheet
    let transports = buildTransports modo central path transportSheet
    let consumes   = buildConsumes modo central path consumeSheet
    let sleeves     = buildSleeves modo central path sbyteSheet
    let sells       = buildSells cliente diaGas sellSheet

    flowSheet.Data
    |> Seq.filter (fun row -> row.Modo = modo && row.Path = path && row.Central = central && row.Kind <> null )
    |> Seq.sortBy (fun row -> row.Order)
    |> Seq.map (fun row ->

        let flowId =
          { modo    = row.Modo
            central = row.Central
            path    = row.Path
          }

        // construir la Operation que ya tenías
        let block : Block =
            match row.Kind with
      
            | "Supply" ->
                let sp = supplies |> Map.find row.Ref |> Seq.toList
                SupplyMany sp
               
            | "Sell" ->
                let sp = sells |> Map.find row.Ref
                Sell sp
        

            | "Trade" -> 
                let tp = trades |> Map.find row.Ref
                Trade tp
            
            | "Transport" ->
                let tp = transports |> Map.find row.Ref
                Transport tp

            | "Sleeve" ->
                let sl = sleeves |> Map.find row.Ref
                Sleeve sl

            | "Consume" ->
                let cp = consumes |> Map.find row.Ref
                Consume cp

            | other ->
                failwithf "Kind '%s' no soportado en hoja Flow" other

        let joinKey =
                  // depende del tipo generado por ExcelProvider; ajustá nombre exacto
                  let jk = row.JoinKey
                  if isNull (box jk) || String.IsNullOrWhiteSpace (string jk)
                  then None
                  else Some (string jk)
      
        { flowId  = flowId
          order   = int row.Order
          block   = block
          joinKey = joinKey })
    |> Seq.toList


/// Lee la SheetFlow desde el archivo Excel y construye los paths del Flow
/// agrupados por `FlowId`.
///
/// Responsabilidad:
/// - Filtra los registros por `modo` y `central`.
/// - Materializa los `FlowStep` correspondientes al `diaGas`.
/// - Agrupa los steps por `FlowId`.
/// - Ordena los steps de cada path por el campo `order`.
///
/// Alcance:
/// - Esta función solo construye la representación estructural del Flow
///   (`Map<FlowId, FlowStep list>`).
/// - NO interpreta topología (Linear / Join).
/// - NO clasifica roles (Contributor / Final).
/// - NO ejecuta operaciones ni valida invariantes del Flow.
///
/// Garantías:
/// - Cada lista de `FlowStep` está ordenada crecientemente por `order`.
/// - Los `FlowId` devueltos representan paths independientes dentro del Flow.
///
/// Errores:
/// - Cualquier error de lectura, parseo o datos inconsistentes del Excel
///   debe manejarse en este nivel o propagarse explícitamente.
///
/// El resultado de esta función es la entrada directa de `buildFlowDef`,
/// que se encarga de detectar la topología y validar invariantes.
let getFlowSteps
    (pathExcel : string)
    (modo      : string)
    (central   : string)
    (diaGas    : DateOnly)
    : Map<FlowId, FlowStep list> =


    let flowSheet = new FlowSheet(pathExcel)

    let paths =
        flowSheet.Data
        |> Seq.filter (fun r ->
            String.Equals(r.Modo, modo, StringComparison.OrdinalIgnoreCase) &&
            String.Equals(r.Central, central, StringComparison.OrdinalIgnoreCase))
        |> Seq.map (fun r -> r.Path)
        |> Seq.distinct
        |> Seq.toList

    paths
    |> List.map (fun path ->
        let fid = { modo = modo; central = central; path = path }
        fid, buildFlowSteps pathExcel modo central path diaGas)
    |> Map.ofList


 
let opOfBlock (b: Block) : Operation =
    match b with
    | Supply sp       -> Supply.supply sp
    | SupplyMany sps  -> Supply.supplyMany sps
    | Sell sp         -> Sell.sell sp
    | Transport p     -> Transport.transport p
    | Trade p         -> Trade.trade p
    | Sleeve p        -> Sleeve.sleeve p  
    | Consume p       -> Consume.consume p



let runSteps (steps: FlowStep list) (initial: State)
    : Result<State * Transition list, DomainError> =

    (Ok (initial, []), steps)
    ||> List.fold (fun acc step ->
        acc
        |> Result.bind (fun (st, ts) ->
            let op = opOfBlock step.block
            op st
            |> Result.map (fun tr ->
                tr.state, ts @ [tr] )))


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
    steps |> List.tryLast |> Option.bind (fun s -> s.joinKey) = Some jk

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




// ********************************************************************************************************
// Estado inicial
let st0 : State =
    {   energy = 0.0m<MMBTU>
        owner    = "N/A"
        contract = "INIT"
        location = "E104"
        gasDay   = DateOnly(2025,12,1)
        meta     = Map.empty }


let runAllModoCentral xlsPath modo central diaGas =
    let flowSteps = getFlowSteps xlsPath modo central diaGas
    let fd =  buildFlowDef flowSteps
    let ze = 0.0m<MMBTU>
    match fd with
    | Ok fs -> runFlow fs st0 ze (+) runSteps
    | Error e -> Error e
    

let printStatus (st:State , (ts:Transition list)) = 
        printfn "%A" st
        
        ts |> List.iter (fun t -> 
            let op =
                match t.notes.TryFind "op" with
                | Some v -> v
                | None -> "<sin-op>"

            printfn $"Op: {op} Owner: {t.state.owner} Location: {t.state.location} Energy: {t.state.energy})" |> ignore)

let showTransitions  (r: Result<(State * Transition list), DomainError>) =
    match r with
    | Ok (st, ts) -> printStatus (st, ts)
    | Error e -> printfn "%A" e
       


