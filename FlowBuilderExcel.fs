module FlowBuilderExcel

open System
open System.IO
open FSharp.Interop.Excel
open Tipos
open LegosOps

[<Literal>]
let excelPath = @"C:\Users\cszit\source\repos\f#\Saavi\ComposeModel\ModeloSaavi\EscenarioSample.xlsx"

type FlowSheet         = ExcelFile<excelPath, "Flow", HasHeaders=true >
type SupplySheet       = ExcelFile<excelPath, "Supply", HasHeaders=true >
type SupplyTradeSheet  = ExcelFile<excelPath, "SupplyTrade", HasHeaders=true >
type TradeSheet        = ExcelFile<excelPath, "Trade", HasHeaders=true >
type TransportSheet    = ExcelFile<excelPath, "Transport", HasHeaders=true >
type SleeveSheet       = ExcelFile<excelPath, "Sleeve", HasHeaders=true >
type ConsumeSheet      = ExcelFile<excelPath, "Consume", HasHeaders=true >



let getPrice  (index: decimal<USD/MMBTU>) 
              (adder: decimal<USD/MMBTU>) 
              (formula: string )
               : decimal<USD/MMBTU> =
                                        match formula with
                                        | "" -> index + adder
                                        | _     -> index 

let loadSheets (path:string) =
    let flow             = new FlowSheet(path)
    let supply           = new SupplySheet(path)
    let supplyTrade      = new SupplyTradeSheet(path)
    let trade            = new TradeSheet(path)
    let transport        = new TransportSheet(path)
    let sleeve           = new SleeveSheet(path)
    let consume          = new ConsumeSheet(path)
    flow, supply, supplyTrade, trade, transport, sleeve, consume

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



let buildSupplies planta central (sheet: SupplySheet)  : SupplyParams list =
    sheet.Data
    |> Seq.filter (fun row -> row.Planta = planta && row.Central = central && row.Name <> null && row.Name <> "")
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
        sp)
        |> Seq.toList



let buildTrades planta central (sheet: TradeSheet) : Map<string, TradeParams> =
    sheet.Data
    |> Seq.filter (fun row -> row.Planta = planta && row.Central = central && row.Name <> null && row.Name <> "")
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


let buildSleeves planta central (sheet: SleeveSheet) : Map<string, SleeveParams> =
    sheet.Data
    |> Seq.filter (fun row -> row.Planta = planta && row.Central = central &&   row.Name <> null && row.Name <> "")
    |> Seq.map (fun row ->

        let index = decimal row.Index * 1.0m<USD/MMBTU>
        let xadder = decimal row.Adder * 1.0m<USD/MMBTU>
        let formula  = string row.Formula  

        let sl : SleeveParams =
            {   provider    = row.Provider
                seller      = row.Seller
                buyer       = row.Buyer
                sleeveSide  = if row.SleevSide = "Exporte" then SleeveSide.Export else SleeveSide.Import
                index       = index                                        
                adder       = xadder
                price       = getPrice index xadder formula
                contractRef = row.ContractRef
                meta        = Map.empty }
        row.Name, sl)
    |> Map.ofSeq



let buildTransports planta central (sheet: TransportSheet) : Map<string, TransportParams> =
    sheet.Data
    |> Seq.filter (fun row -> row.Planta = planta && row.Central = central && row.Name <> null && row.Name <> "")
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


let buildConsumes planta central (sheet: ConsumeSheet) : Map<string, ConsumeParams> =
    sheet.Data
    |> Seq.filter (fun row -> row.Planta = planta && row.Central = central && row.Name <> null && row.Name <> "")
    |> Seq.map (fun row ->
        let cp : ConsumeParams =
          { provider      = row.Provider
            meterLocation = row.Location
            measured      = decimal row.MeasuredMMBTU * 1.0m<MMBTU>
            tolerancePct  = decimal row.TolerancePct
            penaltyRate   = decimal row.PenaltyUSDMMBTU * 1.0m<USD/MMBTU> }
        row.Name, cp)
    |> Map.ofSeq







let buildBlocksFromExcel (path:string) planta central : Block list =
    let flowSheet, supplySheet, supplyTrade, tradeSheet, transportSheet, sbyteSheet, consumeSheet =
        loadSheets path

    let supplies   = buildSupplies planta central supplySheet
    let trades     = buildTrades planta central tradeSheet
    let transports = buildTransports planta central transportSheet
    let consumes   = buildConsumes planta central consumeSheet
    let sleeves     = buildSleeves planta central sbyteSheet

    flowSheet.Data
    |> Seq.filter (fun row -> row.Planta = planta && row.Central = central && row.Kind <> null )
    |> Seq.sortBy (fun row -> row.Order)
    |> Seq.map (fun row ->
        match row.Kind with
      
        | "Supply" ->
            SupplyMany supplies
               
        
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
            failwithf "Kind '%s' no soportado en hoja Flow" other)
    |> Seq.toList
