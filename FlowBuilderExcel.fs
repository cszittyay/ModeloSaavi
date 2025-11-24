module FlowBuilderExcel

open System
open System.IO
open FSharp.Interop.Excel
open Tipos
open LegosOps

type FlowSheet         = ExcelFile< @"EscenarioSample.xlsx", "Flow", HasHeaders=true >
type SupplySheet       = ExcelFile< @"EscenarioSample.xlsx", "Supply", HasHeaders=true >
type SupplyTradeSheet  = ExcelFile< @"EscenarioSample.xlsx", "SupplyTrade", HasHeaders=true >
type TradeSheet        = ExcelFile< @"EscenarioSample.xlsx", "Trade", HasHeaders=true >
type TransportSheet    = ExcelFile< @"EscenarioSample.xlsx", "Transport", HasHeaders=true >
type SleeveSheet       = ExcelFile< @"EscenarioSample.xlsx", "Sleeve", HasHeaders=true >
type ConsumeSheet      = ExcelFile< @"EscenarioSample.xlsx", "Consume", HasHeaders=true  >



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
    | x          -> failwithf "TradingHub desconocido: %s" x

let parseTemporalidad = function
    | "DayAhead" -> Temporalidad.DayAhead
    | "Intraday" -> Temporalidad.Intraday
    | "Monthly"  -> Temporalidad.Monthly
    | x          -> failwithf "Temporalidad desconocida: %s" x


let buildSupplies (sheet: SupplySheet) : Map<string, SupplyParams> =
    sheet.Data
    |> Seq.filter (fun row -> row.Name <> null && row.Name <> "")
    |> Seq.map (fun row ->
        let sp : SupplyParams =
          { tcId        = row.TcId
            gasDay      = DateOnly.FromDateTime(row.GasDay)
            tradingHub  = parseTradingHub row.TradingHub
            temporalidad= parseTemporalidad row.Temporalidad
            deliveryPt  = row.DeliveryPt
            seller      = row.Seller
            buyer       = row.Buyer
            qEnergia    = decimal row.QEnergiaMMBTU * 1.0m<MMBTU>
            price       = decimal row.PriceUSDMMBTU * 1.0m<USD/MMBTU>
            adder       = decimal row.AdderUSDMMBTU * 1.0m<USD/MMBTU>
            contractRef = row.ContractRef
            meta        = Map.empty }
        row.Name, sp)
    |> Map.ofSeq


let buildTrades (sheet: TradeSheet) : Map<string, TradeParams> =
    sheet.Data
    |> Seq.filter (fun row -> row.Name <> null && row.Name <> "")
    |> Seq.map (fun row ->
        let tp : TradeParams =
          { side        = if row.Side = "Sell" then TradeSide.Sell else TradeSide.Buy
            seller      = row.Seller
            buyer       = row.Buyer
            adder       = decimal row.AdderUSDMMBTU * 1.0m<USD/MMBTU>
            contractRef = row.ContractRef
            meta        = Map.empty }
        row.Name, tp)
    |> Map.ofSeq


let buildSleeves (sheet: SleeveSheet) : Map<string, SleeveParams> =
    sheet.Data
    |> Seq.filter (fun row -> row.Name <> null && row.Name <> "")
    |> Seq.map (fun row ->
        let sl : SleeveParams =
            {   provider    = row.Provider
                seller      = row.Seller
                buyer       = row.Buyer
                sleeveSide  = if row.SleevSide = "Exporte" then SleeveSide.Export else SleeveSide.Import
                adder       = decimal row.AdderUSDMMBTU * 1.0m<USD/MMBTU>
                contractRef = row.ContractRef
                meta        = Map.empty }
        row.Name, sl)
    |> Map.ofSeq



let buildTransports (sheet: TransportSheet) : Map<string, TransportParams> =
    sheet.Data
    |> Seq.filter (fun row -> row.Name <> null && row.Name <> "")
    |> Seq.map (fun row ->
        let tp : TransportParams =
          { provider   = row.Provider
            entry      = row.Entry
            exit       = row.Exit
            shipper    = row.Shipper
            fuelPct    = decimal row.FuelPct
            usageRate  = decimal row.UsageRateUSDMMBTU * 1.0m<USD/MMBTU>
            reservation= decimal row.ReservationUSDMMBTU * 1.0m<USD/MMBTU> }
        row.Name, tp)
    |> Map.ofSeq


let buildConsumes (sheet: ConsumeSheet) : Map<string, ConsumeParams> =
    sheet.Data
    |> Seq.filter (fun row -> row.Name <> null && row.Name <> "")
    |> Seq.map (fun row ->
        let cp : ConsumeParams =
          { provider      = row.Provider
            meterLocation = row.MeterLocation
            measured      = decimal row.MeasuredMMBTU * 1.0m<MMBTU>
            tolerancePct  = decimal row.TolerancePct
            penaltyRate   = decimal row.PenaltyUSDMMBTU * 1.0m<USD/MMBTU> }
        row.Name, cp)
    |> Map.ofSeq




let supplyTradeFromRow (sheet: SupplyTradeSheet) : MultiSupplyTradeParams  =
     let multiSupplyTrade =
         sheet.Data
         |> Seq.filter (fun row -> row.SupContractRef <> null && row.SupContractRef <> "")
         |> Seq.map (fun row ->
                let sp : SupplyParams =
                                        {
                                            tcId        = row.Name
                                            gasDay      = DateOnly.FromDateTime (row.GasDay)
                                            tradingHub  = parseTradingHub row.TradingHub
                                            temporalidad= parseTemporalidad row.Temporalidad
                                            deliveryPt  = row.DeliveryPt
                                            seller      = row.TradeSeller       // usamos TradeSeller como seller físico
                                            buyer       = row.TradeBuyer        // usamos TradeBuyer como buyer físico
                                            qEnergia    = decimal row.QtyMMBTU * 1.0m<MMBTU>
                                            price       = decimal row.PriceUSDMMBTU * 1.0m<USD/MMBTU>
                                            adder       = decimal row.AdderUSDMMBTU * 1.0m<USD/MMBTU>
                                            contractRef = row.SupContractRef
                                            meta        = Map.empty
                                        }
      
                let tp : TradeParams =
                                    {
                                        side        = if row.TradeSide = "Sell" then TradeSide.Sell else TradeSide.Buy
                                        seller      = row.TradeSeller
                                        buyer       = row.TradeBuyer
                                        adder       = decimal row.AdderUSDMMBTU * 1.0m<USD/MMBTU>
                                        contractRef = row.TradeContractRef
                                        meta        = Map.empty
                                    }
                {supply = sp; trade = tp}
               )
     { legs = multiSupplyTrade |> Seq.toList}

let buildBlocksFromExcel (path:string) : Block list =
    let flowSheet, supplySheet, supplyTrade, tradeSheet, transportSheet, sbyteSheet, consumeSheet =
        loadSheets path

    let supplies   = buildSupplies supplySheet
    let trades     = buildTrades tradeSheet
    let transports = buildTransports transportSheet
    let consumes   = buildConsumes consumeSheet
    let supplyTrade = supplyTradeFromRow supplyTrade
    let sleeves     = buildSleeves sbyteSheet

    flowSheet.Data
    |> Seq.filter (fun row -> row.Ref <> null && row.Ref <> "")
    |> Seq.sortBy (fun row -> row.Order)
    |> Seq.map (fun row ->
        match row.Kind with
        | "Supply" ->
            let sp = supplies |> Map.find row.Ref
            Supply sp

        | "MultiSupplyTrade" ->
            let spt = supplyTrade 
            MultiSupplyTrade spt
        
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
