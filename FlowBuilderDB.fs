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


// =====================================================================================
// Caches (lazy) - evitamos pegarle a la DB al cargar el módulo y permitimos reuso.
// =====================================================================================
type flowId = int

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

let private flowMasterById =
    lazy (ctx.Fm.FlowMaster |> Seq.map (fun fm -> fm.IdFlowMaster, fm) |> Map.ofSeq)


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


let private rutaById =
    lazy (ctx.Dbo.Ruta |> Seq.map (fun r -> r.IdRuta, r) |> Map.ofSeq)

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
let dRuta = rutaById.Value

// =====================================================================================
// Helpers
// =====================================================================================

let private tryFindFlowMaster (codigoFM: string) (path: string) =
    ctx.Fm.FlowMaster |> Seq.tryFind (fun fm -> fm.Nombre = codigoFM)

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



// Para un FlowMaster y un path: sólo un idFlowDetail de tipo "Supply" (asumido)
let buildSupplysDB diaGas (idFlowMaster: int) path : Map<flowId, SupplyParams list> = 
    let dTradingHub = ctx.Platts.IndicePrecio |> Seq.map(fun th -> th.IdIndicePrecio, th.Nemonico) |> Map.ofSeq

    let flowMaster = flowMasterById.Value.[idFlowMaster]
    let flowDetail = getFlowDetailsByTipo flowMaster.IdFlowMaster path "Supply" |> List.find(fun fd -> fd.Path = path)
    let compraGas = loadCompraGas diaGas flowDetail.IdFlowDetail
  
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
                deliveryPt  = dPto.[cg.idPuntoEntrega]
                seller      = dEnt.[contrato.idContraparte].Nombre
                buyer       = dEnt.[contrato.idParte].Nombre
                qEnergia    = cg.nominado * 1.0m<MMBTU>
                index       = 1.0m * 0.m<USD/MMBTU> // Placeholder, replace with actual index retrieval                                        
                adder       = 1.0m * 0.m<USD/MMBTU>
                price       = (cg.precio |> Option.defaultValue 0.0m) * 1.m<USD/MMBTU> 
                contractRef = contrato.codigo
                meta        = Map.empty }
        flowDetail.IdFlowDetail, sp
        )
        |> List.groupBy fst 
        |> List.map (fun (flId, rows) -> flId, rows |> List.map snd)
        |> Map.ofList


let buildTradesDB idFlowMaster path  : Map<flowId , TradeParams> =
    let flowMaster = flowMasterById.Value.[idFlowMaster]
    let tradeGas = getFlowDetailsByTipo flowMaster.IdFlowMaster path "Trade"

    tradeGas |> List.map(fun  fd ->
        let trade = tradeByFlowDetailId.Value.[fd.IdFlowDetail]
        let tp : TradeParams =
                {
                  side = if trade.Side = "Sell" then TradeSide.Sell else TradeSide.Buy
                  buyer = dEnt.[trade.IdBuyer].Nombre 
                  seller = dEnt.[trade.IdSeller].Nombre
                  location = dPto.[trade.IdPunto]
                  adder   = (trade.Adder |> Option.defaultValue 0.0m) * 1.m<USD/MMBTU>
                  contractRef = trade.Codigo
                  meta = Map.empty
                 }
        fd.IdFlowDetail, tp
        )
        |> Map.ofList


let buildSleevesDB idFlowMaster path : Map<flowId, SleeveParams> =
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
            Some (fd.IdFlowDetail, sp)
    )
    |> Map.ofList


let buildTransportsDB idFlowMaster path : Map<flowId, TransportParams> =
    let detalles = getFlowDetailsByTipo idFlowMaster path "Transport"

    let dTransport = ctx.Fm.Transport |> Seq.map (fun t -> t.IdFlowDetail, t) |> Map.ofSeq
    
    detalles
    |> List.choose (fun fd ->
        match dTransport.TryFind fd.IdFlowDetail with
        | None -> None
        | Some tr ->
            let ruta = dRuta.[tr.IdRuta]
            let cto = dCont.[ContratoId ruta.IdContrato]
            let tp : TransportParams =
                { provider    = dEnt.[cto.idContraparte].Nombre
                  pipeline    = "Gasoducto"
                  entry       = dPto.[ruta.IdPuntoRecepcion]
                  exit        = dPto.[ruta.IdPuntoEntrega]
                  acaRate     = tr.AcaRate  * 1.0m<USD/MMBTU>
                  shipper     = dEnt.[cto.idContraparte].Nombre
                  fuelMode    = if tr.FuelMode = "RxBase" then FuelMode.RxBase else FuelMode.ExBase
                  fuelPct     = ruta.Fuel
                  usageRate   = ruta.CargoUso * 1.0m<USD/MMBTU>
                  reservation = ruta.CargoReserva * 1.0m<USD/MMBTU>
                  meta        = Map.empty }
            Some (fd.IdFlowDetail, tp)
    )
    |> Map.ofList




// En el caso de Consume, sólo debería haber uno por FlowMaster, independiente del path.
let buildConsumeDB diaGas idFlowMaster path: Map<flowId, ConsumeParams> =
   let fdConsumo = ctx.Fm.FlowDetail |> Seq.find (fun fd -> fd.IdFlowMaster = idFlowMaster && tipoOperacionByDesc.Value.[ "Consume"] = fd.IdTipoOperacion)
   let idPunto, consumo = loadConsumo diaGas fdConsumo.IdFlowDetail |> Seq.head

   let consumeParams : ConsumeParams =
        { meterLocation = dPto.[idPunto]
          gasDay   = diaGas
          provider = "Internal"
          measured   =(consumo |> Option.defaultValue 0.0m) * 1.0m<MMBTU> // Placeholder, reemplazar con lógica de consumo real
        }

   Map.ofList[ (fdConsumo.IdFlowDetail, consumeParams) ]



let buildSellsDB diaGas idFlowMaster path : Map<flowId, SellParams list> =
     // Las ventas definidas para el path
    let flowDetail = getFlowDetailsByTipo idFlowMaster path "Sell" 
    let fdSellMap = flowDetail|> Seq.map(fun x -> x.IdFlowDetail, x.Referencia) |> Map
    let ventasRegistradas = ctx.Dbo.VentaGas |> Seq.filter (fun vg -> vg.DiaGas = do2dt diaGas )   
   
    let mapVentas = ventasRegistradas  |> Seq.map( fun vr ->
            let ref = fdSellMap.[vr.IdFlowDetail]
            let sp : SellParams =
                {   location      = dPto.[vr.PuntoEntrega]
                    gasDay      = diaGas
                    seller      = dEnt.[vr.IdVendedor].Nombre
                    buyer       = dEnt.[vr.IdComprador].Nombre
                    qty         = vr.CantidadMmBtu  * 1.0m<MMBTU>
                    price       = vr.PrecioUsd * 1.0m<USD/MMBTU>
                    adder       = 0.0m<USD/MMBTU>
                    contractRef = ref
                    meta        = Map.empty
                }
            (vr.IdFlowDetail, sp))
    mapVentas |> Seq.groupBy fst |> Seq.map (fun (ref, rows) -> ref, rows |> Seq.toList |> List.map snd) |> Map
    

// =====================================================================================
// FlowSteps (equivalente a buildFlowSteps / getFlowSteps de Excel)
// =====================================================================================

let buildFlowStepsDb (codigoFM:string) (path: string) (diaGas: DateOnly) : FlowStep list =
    let fm =
        match tryFindFlowMaster codigoFM path with
        | Some fm -> fm
        | None -> failwithf "No se encontró FlowMaster para modo='%s' central='%s'" codigoFM path


    let supplies   = buildSupplysDB diaGas fm.IdFlowMaster path
    let trades     = buildTradesDB fm.IdFlowMaster path
    let sleeves    = buildSleevesDB fm.IdFlowMaster path
    let transports = buildTransportsDB fm.IdFlowMaster path
    let consumes   = buildConsumeDB diaGas fm.IdFlowMaster path
    let sells      = buildSellsDB diaGas fm.IdFlowMaster path

    // Orden: preferimos fd.Orden si existe; si no, IdFlowDetail (estable).
    let flowDetails =
        getFlowDetails fm.IdFlowMaster path
        |> Seq.toList
        |> List.sortBy (fun fd -> fd.Orden)

    flowDetails
    |> List.map (fun fd ->
        let flowId = { modo = codigoFM; central = path; path = path }

        let tipoDesc =
            tipoOperacionById.Value
            |> Map.tryFind fd.IdTipoOperacion
            |> Option.defaultValue "<unknown>"

        let block : Block =
            match tipoDesc with
            | "Supply" ->
                let sp = supplies |> Map.find fd.IdFlowDetail
                SupplyMany sp
            | "Trade" ->
                let tp = trades |> Map.find fd.IdFlowDetail
                Trade tp
            | "Transport" ->
                let tp = transports |> Map.find fd.IdFlowDetail
                Transport tp
            | "Sleeve" ->
                let sl = sleeves |> Map.find fd.IdFlowDetail
                Sleeve sl
            | "Consume" ->
                let cp = consumes |> Map.find fd.IdFlowDetail
                Consume cp
            | "Sell" ->
                let sp = sells |> Map.find fd.IdFlowDetail
                SellMany sp 
            | other ->
                failwithf "TipoOperacion '%s' no soportado para FlowSteps DB" other

      
        { flowId  = flowId
          order   = fd.Orden
          block   = block
          joinKey = fd.JoinKey
          ref     = fd.Path }
    )

let getFlowStepsDB
    (flowMaster      : string)
    (path   : string)
    (diaGas    : DateOnly)
    : Map<FlowId, FlowStep list> =

    let fm =
        match tryFindFlowMaster flowMaster path with
        | Some fm -> fm
        | None -> failwithf "No se encontró FlowMaster para modo='%s' central='%s'" flowMaster path

    let paths =
        ctx.Fm.FlowDetail
        |> Seq.filter (fun fd -> fd.IdFlowMaster = fm.IdFlowMaster)
        |> Seq.map (fun fd -> fd.Path)
        |> Seq.distinct
        |> Seq.toList

    paths
    |> List.map (fun path ->
        let fid = { modo = flowMaster; central = path; path = path }
        fid, buildFlowStepsDb flowMaster path diaGas)
    |> Map.ofList






