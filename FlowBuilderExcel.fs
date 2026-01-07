module FlowBuilderExcel

open System
open System.IO
open FsToolkit.ErrorHandling
open FSharp.Interop.Excel
open Tipos
open Helpers.FlowBuilderUtils
open Helpers



[<Literal>]
let excelPath = @"EscenarioSample.xlsx"

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
                index       = 1
                adder       = xadder
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
          { gasDay       = DateOnly(2026, 1, 1)
            provider      = row.Provider
            meterLocation = row.Location
            measured      = decimal row.MeasuredMMBTU * 1.0m<MMBTU>
          }
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
          joinKey = joinKey
          ref     = row.Ref
        })
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


 




// ********************************************************************************************************
// Estado inicial
let st0 : State =
    {   energy = 0.0m<MMBTU>
        owner    = "N/A"
        contract = "INIT"
        location = "E104"
        gasDay   = DateOnly(2025,12,19)
        meta     = Map.empty }


let runAllModoCentral xlsPath modo central diaGas =
    let flowSteps = getFlowSteps xlsPath modo central diaGas
    let fd =  buildFlowDef flowSteps
    let ze = 0.0m<MMBTU>
    match fd with
    | Ok fs -> runFlow fs st0 ze (+) runSteps
    | Error e -> Error e
    

let printStatus (st:State , (ts:Transition list)) = 
        
        ts |> List.iter (fun t -> printfn "%A" t.notes)
            //let op =
            //    match t.notes.TryFind "op" with
            //    | Some v -> v
            //    | None -> "<sin-op>"

            //let costos = t.costs |> List.sumBy (fun c -> c.amount)
            //printfn $"Op: {op} Owner: {t.state.owner}\tLocation: {t.state.location}\tEnergy: {Display.qtyStr t.state.energy}MBTU\tCostos: {Display.moneyStr costos}USD)" 
            


let showTransitions  (r: Result<(State * Transition list), DomainError>) =
    match r with
    | Ok (st, ts) -> printStatus (st, ts)
    | Error e -> printfn "%A" e
       


