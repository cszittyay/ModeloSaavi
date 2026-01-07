module FlowBuilderDB

open System
open System.IO
open FsToolkit.ErrorHandling
open Tipos
open DefinedOperations
open Helpers
open DbContext
open Gnx.Persistence.SQL_Data
open Gnx.Domain

//let dEntidadLegal = ctx.Dbo.EntidadLegal |> Seq.map(fun e -> e.IdEntidadLegal, e) |> Map.ofSeq
//let dContrato = loadContratos() |> List.map (fun c -> c.id, c) |> Map.ofList
//let dPunto = ctx.Dbo.Punto |> Seq.map(fun p -> p.IdPunto, p.Codigo) |> Map.ofSeq
  
//let transactions = loadTransacciones() |> List.map (fun t -> t.id, t) |> Map.ofList
//let dFlowMaster = ctx.Fm.FlowMaster |> Seq.map(fun fm -> fm.Nombre, fm) |> Map.ofSeq
//let dTipoOperacion = ctx.Fm.TipoOperacion 
//                     |> Seq.map(fun top -> top.Descripcion, top.IdTipoOperacion) 
//                     |> Seq.toList
//                     |> Map.ofList

//// Obtener los FlowDetail y mapearlos por (IdFlowMaster, Path)
//let dFlowDetail = ctx.Fm.FlowDetail 
//                  |> Seq.groupBy(fun fd -> (fd.IdFlowMaster, fd.Path))
//                  |> Seq.map(fun (k, v) -> k, v )
//                  |> Map.ofSeq

//let dTrades = ctx.Fm.Trade |> Seq.map(fun t -> t.IdFlowDetail, t) |> Map.ofSeq

//let getFlowDetail codigoFlowMaster path tipoOper  =
//    match dFlowDetail.TryFind (codigoFlowMaster, path) with
//    | Some details -> details |> Seq.toList |> List.filter(fun fd -> fd.IdTipoOperacion = dTipoOperacion.[tipoOper])
//    | None -> List.empty


/// *****************************************************************************************************

// =====================================================================================
// Caches (lazy) - evitamos pegarle a la DB al cargar el módulo y permitimos reuso.
// =====================================================================================

let private entidadLegalById =
    lazy (ctx.Dbo.EntidadLegal |> Seq.map (fun e -> e.IdEntidadLegal, e) |> Map.ofSeq)

let private puntoCodigoById =
    lazy (ctx.Dbo.Punto |> Seq.map (fun p -> p.IdPunto, p.Codigo) |> Map.ofSeq)

let  contratosById =
    lazy (loadContratos() |> List.map (fun c -> c.id, c) |> Map.ofList)

let private transaccionesById =
    lazy (loadTransacciones() |> List.map (fun t -> t.id, t) |> Map.ofList)



let private flowMasterByNombre =
    lazy (ctx.Fm.FlowMaster |> Seq.map (fun fm -> fm.Nombre, fm) |> Map.ofSeq)

let private tipoOperacionByDesc =
    lazy (
        ctx.Fm.TipoOperacion
        |> Seq.map (fun top -> top.Descripcion, top.IdTipoOperacion)
        |> Seq.toList
        |> Map.ofList
    )

let private tipoOperacionById =
    lazy (
        ctx.Fm.TipoOperacion
        |> Seq.map (fun top -> top.IdTipoOperacion, top.Descripcion)
        |> Seq.toList
        |> Map.ofList
    )

// (IdFlowMaster, Path) -> seq FlowDetail
let private flowDetailsByMasterPath =
    lazy (
        ctx.Fm.FlowDetail
        |> Seq.groupBy (fun fd -> (fd.IdFlowMaster, fd.Path))
        |> Seq.map (fun (k, v) -> k, v)
        |> Map.ofSeq
    )

// Operaciones “master data” por IdFlowDetail (las transaccionales suelen requerir diaGas)
let private tradeByFlowDetailId =
    lazy (ctx.Fm.Trade |> Seq.map (fun t -> t.IdFlowDetail, t) |> Map.ofSeq)

// NOTE: estas tablas pueden o no existir en tu schema `Fm`. Si existen, descomentá.
let private sleeveByFlowDetailId =
    lazy (ctx.Fm.Sleeve |> Seq.map (fun s -> s.IdFlowDetail, s) |> Map.ofSeq)
//
let private transportByFlowDetailId =
    lazy (ctx.Fm.Transport |> Seq.map (fun t -> t.IdFlowDetail, t) |> Map.ofSeq)
//
//let private consumeByFlowDetailId =
//    lazy (ctx.Fm.Consume |> Seq.map (fun c -> c.IdFlowDetail, c) |> Map.ofSeq)
//
//let private sellByFlowDetailId =
//    lazy (ctx.Fm. |> Seq.map (fun s -> s.IdFlowDetail, s) |> Map.ofSeq)

let private tradingHubNemonicoById =
    lazy (ctx.Platts.IndicePrecio |> Seq.map (fun th -> th.IdIndicePrecio, th.Nemonico) |> Map.ofSeq)


let dEnt = entidadLegalById.Value
let dPto = puntoCodigoById.Value
let dCont = contratosById.Value
let dTrans = transaccionesById.Value
// =====================================================================================
// Helpers
// =====================================================================================

let private tryFindFlowMaster (modo: string) (central: string) =
    // Heurística: en algunos modelos el FlowMaster.Nombre = central, en otros = $"{modo}_{central}" etc.
    let candidates =
        [ central
          $"{modo}_{central}"
          $"{modo}-{central}"
          $"{modo}{central}"
          $"{central}_{modo}"
          $"{central}-{modo}" ]
    let fmMap = flowMasterByNombre.Value
    candidates |> List.tryPick fmMap.TryFind

let private getFlowDetails (idFlowMaster: int) (path: string) =
    match flowDetailsByMasterPath.Value.TryFind (idFlowMaster, path) with
    | None -> Seq.empty
    | Some s -> s

let private getFlowDetailsByTipo (idFlowMaster: int) (path: string) (tipoOpDesc: string) =
    match tipoOperacionByDesc.Value.TryFind tipoOpDesc with
    | None -> []
    | Some idTipo ->
        getFlowDetails idFlowMaster path
        |> Seq.filter (fun fd -> fd.IdTipoOperacion = idTipo)
        |> Seq.toList


/// *****************************************************************************************************
let buildSupplysDB diaGas idFlowDetail : Map<string, SupplyParams list> = 
    let compraGas = loadCompraGas diaGas idFlowDetail
    let dTradingHub = ctx.Platts.IndicePrecio |> Seq.map(fun th -> th.IdIndicePrecio, th.Nemonico) |> Map.ofSeq

    compraGas|> List.map(fun cg ->
        
        let nominado = cg.nominado 
        let confirmado = None
        let transact = dTrans.[cg.idTransaccion]
        let contrato = dCont.[transact.idContrato]// .[transact.idContrato]
        let tradingHubNemo = dTradingHub.[transact.idIndicePrecio |> Option.defaultValue 0]

        let sp : SupplyParams =
                { 
                tcId        = contrato.codigo
                gasDay      = diaGas
                tradingHub  = parseTradingHub tradingHubNemo
                temporalidad= parseTemporalidad cg.temporalidad
                deliveryPt  = puntoCodigoById.Value.[cg.idPuntoEntrega]
                seller      = dEnt.[contrato.idContraparte].Nombre
                buyer       = dEnt.[contrato.idParte].Nombre
                qEnergia    = cg.nominado * 1.0m<MMBTU>
                index       = 1.0m * 0.m<USD/MMBTU> // Placeholder, replace with actual index retrieval                                        
                adder       = 1.0m * 0.m<USD/MMBTU>
                price       = (cg.precio |> Option.defaultValue 0.0m) * 1.m<USD/MMBTU> 
                contractRef = contrato.codigo
                meta        = Map.empty }
        contrato.codigo, sp
        )
        |> List.groupBy fst 
        |> List.map (fun (name, rows) -> name, rows |> List.map snd)
        |> Map.ofList


let buildTradesDB codigoFlowMaster path  : Map<string, TradeParams> =
    let flowMaster = flowMasterByNombre.Value.[codigoFlowMaster]
    let tradeGas = getFlowDetailsByTipo flowMaster.IdFlowMaster path "Trade"
    tradeGas |> List.map(fun  fd ->
        let trade = tradeByFlowDetailId.Value.[fd.IdFlowDetail]
        let tp : TradeParams =
                {
                  side = if trade.Side = "Sell" then TradeSide.Sell else TradeSide.Buy
                  buyer = dEnt.[trade.IdBuyer].Nombre 
                  seller = dEnt.[trade.IdSeller].Nombre
                  location = puntoCodigoById.Value.[trade.IdPunto]
                  adder   = (trade.Adder |> Option.defaultValue 0.0m) * 1.m<USD/MMBTU>
                  contractRef = trade.Codigo
                  meta = Map.empty
                 }
        trade.Codigo, tp
        )
        |> Map.ofList


let buildSleevesDB (idFlowMaster: int) (path: string) : Map<string, SleeveParams> =
    // Requiere tabla Fm.Sleeve (o loader equivalente).
    // Si tu modelo guarda sleeves en otra tabla, reemplazá esta implementación.
    let detalles = getFlowDetailsByTipo idFlowMaster path "Sleeve"
   

    // Intento de acceso “suave”: si no existe la tabla, esto no compila -> en ese caso,
    // implementalo usando un loader (ej. loadSleeves) y eliminá el acceso ctx.Fm.Sleeve.
    let dSleeve = ctx.Fm.Sleeve |> Seq.map (fun s -> s.IdFlowDetail, s) |> Map.ofSeq

    detalles
    |> List.choose (fun fd ->
        match dSleeve.TryFind fd.IdFlowDetail with
        | None -> None
        | Some sl ->
            let sp : SleeveParams =
                { provider    = dEnt.[sl.IdProvider].Nombre
                  seller      = dEnt.[sl.IdSeller].Nombre
                  buyer       = dEnt.[sl.IdBuyer].Nombre
                  location    = dPto.[sl.IdPunto]
                  sleeveSide  = if sl.Side = "Export" then SleeveSide.Export else SleeveSide.Import
                  index       = sl.IdIndicePrecio
                  adder       = (sl.Adder |> Option.defaultValue 0.0m) * 1.0m<USD/MMBTU>
                  contractRef = sl.Codigo
                  meta        = Map.empty }
            Some (sl.Codigo, sp)
    )
    |> Map.ofList

let buildTransportsDB modo central : Map<string, TransportParams> = failwith "Not implemented"

let buildConsumesDB modo central : Map<string, ConsumeParams> = failwith "Not implemented"

let buildSellsDB modo central : Map<string, SellParams> = failwith "Not implemented"


let buildFlowSteps modo central path diaGas : FlowStep list = failwith "Not implemented"

let getFlowSteps
    (modo      : string)
    (central   : string)
    (diaGas    : DateOnly)
    : Map<FlowId, FlowStep list> = failwith "Not implemented"





