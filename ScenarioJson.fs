 


module ScenarioJson

open System
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.Json.Serialization
open FSharp.SystemTextJson

open Unidades
open Tipos
open Helpers
open Supply
open Transport
open Trade
open Consume
open Kleisli

// =====================
// Helpers de parseo
// =====================

let inline mMMBTU (x: decimal) : decimal<MMBTU> = LanguagePrimitives.DecimalWithMeasure<MMBTU> x
let inline mUSD_MMBTU (x: decimal) : decimal<USD/MMBTU> = LanguagePrimitives.DecimalWithMeasure<USD/MMBTU> x

let tryParseDateOnly (s: string) =
    match DateOnly.TryParse(s) with
    | true, d -> Ok d
    | _ -> Error (sprintf "Fecha inválida: '%s' (DateOnly esperado, formato ISO yyyy-MM-dd)" s)

/// Ajustá estos parsers a tus DUs reales en Tipos.fs
let parseTradingHub (s: string) : Result<Location,string> =
    match s.Trim().ToLowerInvariant() with
    | "mainline" -> Ok Mainline
    | "waha"     -> Ok WAHA
    | "hsc"    -> Ok HSC
    // agrega los que correspondan...
    | other      -> Error (sprintf "TradingHub desconocido: '%s'" other)

let parseCicle (s: string) : Result<Cycle,string> =
    match s.Trim().ToLowerInvariant() with
    | "dayahead" -> Ok Cycle.DayAhead
    | "intraday" -> Ok Cycle.Intraday
    | "monthahead" -> Ok Cycle.MonthAhead
    // agrega los que correspondan...
    | other        -> Error (sprintf "Cicle desconocido: '%s'" other)

let tryGet (jo: JsonObject) (name: string) =
    match jo.TryGetPropertyValue(name) with
    | true, v -> Ok v
    | _ -> Error (sprintf "Falta propiedad '%s'" name)

let expectString (jn: JsonNode) =
    match jn.GetValueKind() with
    | JsonValueKind.String -> Ok (jn.GetValue<string>())
    | _ -> Error (sprintf "Se esperaba string, llegó %A" (jn.GetValueKind()))

let expectDecimal (jn: JsonNode) =
    match jn.GetValueKind() with
    | JsonValueKind.Number ->
        match Decimal.TryParse(jn.ToJsonString()) with
        | true, v -> Ok v
        | _ -> Ok (jn.GetValue<decimal>()) // fallback
    | JsonValueKind.String ->
        let s = jn.GetValue<string>()
        match Decimal.TryParse(s.Replace(",", "."), Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture) with
        | true, v -> Ok v
        | _ -> Error (sprintf "Decimal inválido: '%s'" s)
    | _ -> Error (sprintf "Se esperaba número/decimal, llegó %A" (jn.GetValueKind()))

let tryString (jo: JsonObject) (name: string) =
    ResultCE.result {
        let! n = tryGet jo name
        return! expectString n
    }

let tryDecimal (jo: JsonObject) (name: string) =
    ResultCE.result {
        let! n = tryGet jo name
        return! expectDecimal n
    }

let tryObj (jn: JsonNode) =
    match jn with
    | :? JsonObject as o -> Ok o
    | _ -> Error "Se esperaba un objeto JSON"

let tryArray (jn: JsonNode) =
    match jn with
    | :? JsonArray as a -> Ok a
    | _ -> Error "Se esperaba un array JSON"

// =====================
// DTOs (formato JSON)
// =====================

type TcDto =
  { tcId        : string
    gasDay      : string
    tradingHub  : string
    cycle       : string
    deliveryPt  : string
    seller      : string
    buyer       : string
    qtyMMBtu    : decimal
    price       : decimal
    adder       : decimal
    contractRef : string
    meta        : Map<string,obj>  }

type TransportParamsDto =
  { provider    : string
    entry       : string
    exit        : string
    shipper     : string
    fuelPct     : decimal
    usageRate   : decimal
    reservation : decimal
    meta        : Map<string,obj> option }


//type TradeParams =
//  { side        : TradeSide
//    seller      : Party
//    buyer       : Party
//    adder       : decimal<USD/MMBTU>        // $/MMBtu (fee/adder)
//    contractRef : Contract
//    meta        : Map<string,obj> }

type TradeParamsDto =
  { side         : string              // "Buy" | "Sell"
    seller       : string
    byer        : string
    adder        : decimal
    contractRef  : string option
    meta         : Map<string,obj> }

type ConsumeParamsDto =
  { meterLocation : string
    measured      : decimal
    tolerancePct  : decimal
    penaltyRate   : decimal
    meta          : Map<string,obj> }

type StepSpec =
  | SupplyMany of TcDto list
  | Transport  of TransportParamsDto
  | Trade      of TradeParamsDto
  | Consume    of ConsumeParamsDto

// =====================
// Map DTO -> Domain
// =====================

//type TransactionConfirmation = {
//  tcId        : string
//  gasDay      : DateOnly
//  tradingHub  : Location       // Hub que sirve para fijar el precio de referencia o índice
//  cycle       : Cycle 
//  deliveryPt  : Location      // Punto en el que el Suministrador (Productor) entrega el gas
//  seller      : Party
//  buyer       : Party
//  qtyMMBtu    : Energy        // volumen confirmado
//  price       : RateGas       // $/MMBtu
//  adder       : decimal<USD/MMBTU>
//  contractRef : Contract
//  meta        : Map<string,obj>
//}



let tcFromDto (d: TcDto) : Result<TransactionConfirmation, string> =
    // Use Result.bind for chaining operations
    tryParseDateOnly d.gasDay
    |> Result.bind (fun gd ->
        parseTradingHub d.tradingHub
        |> Result.bind (fun hub ->
            parseCicle d.cycle
            |> Result.map (fun ci ->
                {
                    tcId = d.tcId
                    gasDay = gd
                    tradingHub = hub
                    cycle = ci
                    deliveryPt = d.deliveryPt
                    seller = d.seller
                    buyer = d.buyer
                    qtyMMBtu = mMMBTU d.qtyMMBtu
                    price = mUSD_MMBTU d.price
                    adder = mUSD_MMBTU d.adder
                    contractRef = d.contractRef
                    meta = d.meta
                }: TransactionConfirmation
            )
        )
    )
    

let transportFromDto (d: TransportParamsDto) : TransportParams =
    { provider    = d.provider
      entry       = d.entry
      exit        = d.exit
      shipper     = d.shipper
      fuelPct     = d.fuelPct
      usageRate   = mUSD_MMBTU d.usageRate
      reservation = mUSD_MMBTU d.reservation }


//type TradeParams =
//  { side        : TradeSide
//    seller      : Party
//    buyer       : Party
//    adder       : decimal<USD/MMBTU>        // $/MMBtu (fee/adder)
//    contractRef : Contract
//    meta        : Map<string,obj> }


let tradeFromDto (d: TradeParamsDto) : Result<TradeParams, string> =
    // Parse the trade side
    let sideResult =
        match d.side.Trim().ToLowerInvariant() with
        | "buy" -> Ok TradeSide.Buy
        | "sell" -> Ok TradeSide.Sell
        | x -> Error (sprintf "Trade.side desconocido: '%s'" x)

    // Construct the TradeParams record
    sideResult
    |> Result.map (fun side ->
        {
            side = side
            seller = d.seller
            buyer = d.byer
            adder = mUSD_MMBTU d.adder
            contractRef = d.contractRef
            meta = d.meta
        })

let consumeFromDto (d: ConsumeParamsDto) : ConsumeParams =
    { meterLocation = d.meterLocation
      measured      = mMMBTU d.measured
      tolerancePct  = d.tolerancePct
      penaltyRate   = mUSD_MMBTU d.penaltyRate }

// =====================
// Decode StepSpec desde JSON
// =====================

let options =
    let o = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
    o.Converters.Add(JsonFSharpConverter(unionEncoding = JsonUnionEncoding.AdjacentTag))
    o

/// Decodifica un bloque { "op": "...", ... } a StepSpec
let private decodeStep (jo: JsonObject) : Result<StepSpec,string> =
    result {
        let! opStr = tryString jo "op"
        match opStr.Trim().ToLowerInvariant() with
        | "supplymany" ->
            // legs: TcDto[]
            let! legsNode = tryGet jo "legs"
            let! legsArr  = tryArray legsNode
            let legsDto =
                [ for n in legsArr do
                    let d = JsonSerializer.Deserialize<TcDto>(n, options)
                    yield d ]
            // map DTO->domain para validar enums/fechas
            let legsTC =
                legsDto |> List.map tcFromDto |> List.fold (fun acc x ->
                    match acc, x with
                    | Error e, _      -> Error e
                    | _, Error e      -> Error e
                    | Ok xs, Ok v     -> Ok (v::xs)) (Ok [])
                |> Result.map List.rev
            match legsTC with
            | Error e -> return! Error e
            | Ok _ -> return SupplyMany legsDto

        | "transport" ->
            let! pNode = tryGet jo "p"
            let! pObj  = tryObj pNode
            let pDto   = JsonSerializer.Deserialize<TransportParamsDto>(pObj, options)
            // validaciones mínimas
            if String.IsNullOrWhiteSpace pDto.shipper then
                return! Error "transport.shipper vacío"
            if pDto.fuelPct < 0m || pDto.fuelPct >= 1m then
                return! Error (sprintf "transport.fuelPct=%M fuera de [0,1)" pDto.fuelPct)
            return Transport pDto

        | "trade" ->
            let! pNode = tryGet jo "p"
            let! pObj  = tryObj pNode
            let pDto   = JsonSerializer.Deserialize<TradeParamsDto>(pObj, options)
            // valida side/qty>=0 precio>=0
            if pDto.qtyMMBtu <= 0m then return! Error "trade.qtyMMBtu <= 0"
            if pDto.price   <  0m then return! Error "trade.price < 0"
            return Trade pDto

        | "consume" ->
            let! pNode = tryGet jo "p"
            let! pObj  = tryObj pNode
            let pDto   = JsonSerializer.Deserialize<ConsumeParamsDto>(pObj, options)
            if pDto.measured < 0m then return! Error "consume.measured < 0"
            if pDto.tolerancePct < 0m then return! Error "consume.tolerancePct < 0"
            return Consume pDto

        | other ->
            return! Error (sprintf "op desconocido: '%s'" other)
    }

/// Decodifica un array de operaciones [{op=...}, ...] → Operation[]
let decodeOperationsFromJson (json: string) : Result<Operation array,string> =
    result {
        let root = JsonNode.Parse(json)
        let! arr = tryArray root
        // 1) parse specs
        let specs =
            [ for n in arr do
                match n with
                | :? JsonObject as jo ->
                    match decodeStep jo with
                    | Ok s -> yield Ok s
                    | Error e -> yield Error e
                | _ -> yield Error "Cada ítem debe ser un objeto" ]
            |> List.fold (fun acc x ->
                match acc, x with
                | Error e, _      -> Error e
                | _, Error e      -> Error e
                | Ok xs, Ok v     -> Ok (v::xs)) (Ok [])
            |> Result.map List.rev

        let! specs = specs

        // 2) build Operations
        let build (s: StepSpec) : Result<Operation,string> =
            match s with
            | SupplyMany legsDto ->
                // Map a SupplierLeg list de dominio
                let legsTC =
                    legsDto |> List.map tcFromDto |> List.fold (fun acc x ->
                        match acc, x with
                        | Error e, _  -> Error e
                        | _, Error e  -> Error e
                        | Ok xs, Ok v -> Ok (v::xs)) (Ok [])
                    |> Result.map List.rev
                legsTC
                |> Result.map (fun tcs ->
                    let legs = tcs |> List.map (fun tc -> { tc = tc })
                    Supply.supplyMany legs)

            | Transport pDto ->
                let p = transportFromDto pDto
                Ok (Transport.transport p)

            | Trade pDto ->
                tradeFromDto pDto
                |> Result.map Trade.trade

            | Consume pDto ->
                let p = consumeFromDto pDto
                Ok (Consume.consume p)

        let ops =
            specs
            |> List.map build
            |> List.fold (fun acc x ->
                match acc, x with
                | Error e, _      -> Error e
                | _, Error e      -> Error e
                | Ok xs, Ok v     -> Ok (v::xs)) (Ok [])
            |> Result.map (fun xs -> xs |> List.rev |> List.toArray)

        return! ops
    }

/// Decodifica el formato "scenario" con { operations: [...] }
let decodeScenarioFromJson (json: string) : Result<Operation array,string> =
    result {
        let root = JsonNode.Parse(json) :?> JsonObject
        let! opsNode = tryGet root "operations"
        let arrStr = opsNode.ToJsonString()
        return! decodeOperationsFromJson arrStr
    }
