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



let getFlowSteps (pathExcel: string) (modo: string) (central: string) (diaGas: DateOnly)
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


let splitAtJoin (joinKey: string) (steps: FlowStep list)
    : FlowStep list * FlowStep option * FlowStep list =
    let idxOpt =
        steps
        |> List.tryFindIndex (fun s -> s.joinKey = Some joinKey)

    match idxOpt with
    | None ->
        steps, None, []
    | Some idx ->
        let before = steps |> List.take idx
        let join   = steps.[idx]
        let after  = steps |> List.skip (idx + 1)
        before, Some join, after


let runUntilJoin
    (joinKey : string)
    (steps   : FlowStep list)
    (initial : State)
    : Result<State * Transition list * FlowStep option * FlowStep list, DomainError> =

    result {

        // Separar steps en before + joinStep + after
        let before, joinOpt, after = splitAtJoin joinKey steps
        
        // Ejecutar steps previos al join
        let! (stBefore, tsBefore) =
            runSteps before initial

        // Si no hay join, devolver el resultado hasta aquí
        match joinOpt with
        | None ->
            return stBefore, tsBefore, None, []
        | Some joinStep ->
            // Ejecutar también el step del join
            let opJoin = opOfBlock joinStep.block
            let! trJoin = opJoin stBefore
            let stJoin  = trJoin.state
            let tsAll   = tsBefore @ [trJoin]

            return stJoin, tsAll, Some joinStep, after
    }








let findPreJoinPaths (joinKey:string) (paths: Map<FlowId, FlowStep list>) =
    paths
    |> Map.toList
    |> List.choose (fun (fid, steps) ->
        if steps |> List.exists (fun s -> s.joinKey = Some joinKey) then
            Some (fid, steps)
        else None)

let findPostJoinPath (joinKey:string) (paths: Map<FlowId, FlowStep list>) =
    paths
    |> Map.toList
    |> List.tryFind (fun (_fid, steps) ->
        match steps with
        | first :: _ -> first.joinKey = Some joinKey
        | [] -> false)




let runJoinNPaths
    (joinKey : string)
    (paths   : Map<FlowId, FlowStep list>)
    (initialByPath : Map<FlowId, State>)
    : Result<State * Transition list, DomainError> =

    result {
        // 1) identificar todos los paths que llegan al joinKey
        let prePaths = findPreJoinPaths joinKey paths
        if prePaths.IsEmpty then
            return! Error (Other $"No hay paths que lleguen a JoinKey={joinKey}")

        // 2) correr cada path hasta el join
        let! partials =
            prePaths
            |> List.traverseResultM (fun (fid, steps) ->
                let initial =
                    initialByPath
                    |> Map.tryFind fid
                    |> Option.defaultWith (fun () ->
                        failwith $"Falta initial state para {fid}")

                result {
                    let! (stJoin, ts, joinOpt, _post) = runUntilJoin joinKey steps initial
                    match joinOpt with
                    | None -> return! Error (Other $"JoinKey={joinKey} no encontrado en {fid.path}")
                    | Some _ -> return (fid, stJoin, ts)
                })

        // 3) sumar energías
        let energyTotal =
            partials |> List.sumBy (fun (_fid, st, _ts) -> st.energy)

        let (fid0, st0, _) = partials.Head
        let stJoin = { st0 with energy = energyTotal }

        // 4) encontrar path post-join (recomendado: primer step con JoinKey)
        match findPostJoinPath joinKey paths with
        | None ->
            // Alternativa: si no existe post-join path explícito, sólo devolvemos el estado join
            // (pero para tu caso Path3 debería tener JoinKey para continuar)
            return! Error (Other $"No existe path post-join que arranque con JoinKey={joinKey}. Sugerido: poner JoinKey en el primer step del Path3.")
        | Some (_postFid, postSteps) ->

            // 5) correr la cola común
            let! (stFinal, tsPost) = runSteps postSteps stJoin

            // 6) unir transiciones
            let tsPreAll = partials |> List.collect (fun (_,_,ts) -> ts)
            return (stFinal, tsPreAll @ tsPost)
    }

    // Estado inicial
let st0 : State =
    {   energy = 0.0m<MMBTU>
        owner    = "N/A"
        contract = "INIT"
        location = "E104"
        gasDay   = DateOnly(2025, 10, 22)
        meta     = Map.empty }

// Generación de estados iniciales para cada path
let genInitialByPaths (s0:State) (flowIds: FlowId seq) = flowIds |> Seq.map(fun x -> x, s0) |> Map.ofSeq


// genrera el flujo completo
let genFlujoCompleto pathExcel (joinKey:string) modo central diaGas  =
     let flows = getFlowSteps pathExcel modo central diaGas
     let paths = flows.Keys 
     let initialByPath = genInitialByPaths st0 paths
     let result = runJoinNPaths joinKey flows initialByPath 
     result 