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
open Gnx.Persistence


// =====================================================================================
// Caches (lazy) - evitamos pegarle a la DB al cargar el módulo y permitimos reuso.
// =====================================================================================


type flowId = int



// =====================================================================================
// Helpers
// =====================================================================================

let private tryFindFlowMaster flowMasterId =
    ctx.Fm.FlowMaster |> Seq.tryFind (fun fm -> fm.IdFlowMaster = flowMasterId)

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
    let flowDetail = getFlowDetailsByTipo flowMaster.IdFlowMaster path "Supply" |> List.tryFind(fun fd -> fd.Path = path)
    if flowDetail.IsNone then Map.empty

    else
    let idFlowDetail = flowDetail.Value.IdFlowDetail
    let compraGas = loadCompraGas diaGas idFlowDetail  
    compraGas|> List.map(fun cg ->
        
        let nominado = cg.nominado 
        let confirmado = None
        let transact = dTransGas.[cg.idTransaccion]
        let contrato = dCont.[transact.idContrato]// .[transact.idContrato]

        let sp : SupplyParams =
                { 
                gasDay      = diaGas
                transactionId = transact.id
                buyerId    = transact.idBuyer
                sellerId   = transact.idSeller
                buyer      = transact.buyer
                seller     = transact.seller
                temporalidad  = parseTemporalidad cg.temporalidad
                deliveryPt = transact.puntoEntrega
                deliveryPtId  = cg.idPuntoEntrega
                qEnergia     = cg.nominado * 1.0m<MMBTU>
                index        = (cg.idIndicePrecio |> Option.defaultValue 0) 
                adder        = (cg.adder |> Option.defaultValue 0.0m) * 1.m<USD/MMBTU>
                price        = (cg.precio |> Option.defaultValue 0.0m) * 1.m<USD/MMBTU>
                contractRef = contrato.codigo
                meta         = Map.empty }
        idFlowDetail, sp
        )
        |> List.groupBy fst 
        |> List.map (fun (flId, rows) -> flId, rows |> List.map snd)
        |> Map.ofList


let buildTradesDB idFlowMaster path  : Map<flowId , TradeParams> =
    let flowMaster = flowMasterById.Value.[idFlowMaster]
    let tradeGas = getFlowDetailsByTipo flowMaster.IdFlowMaster path "Trade"

    tradeGas |> List.map(fun  fd ->
        let trade = tradeByFlowDetailId.Value.[fd.IdFlowDetail]
        let transact = dTransGas.[trade.IdTransaccionGas]
        let tp : TradeParams =
                {
                  side = if trade.Side = "Sell" then TradeSide.Sell else TradeSide.Buy
                  transactionId = trade.IdTransaccionGas
                  flowDetailId = fd.IdFlowDetail
                  buyer      = transact.buyer
                  buyerId    = transact.idBuyer
                  sellerId   = transact.idSeller
                  seller     = transact.seller
                  adder      = (transact.adder |> Option.defaultValue 0.0m) * 1.0m<USD/MMBTU>
                  // TODO: Precio
                  price      = 0.0m<USD/MMBTU> // Placeholder, reemplazar con lógica de precio real
                  locationId = transact.idPuntoEntrega
                  location   = transact.puntoEntrega
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
            let transact = dTransGas.[sl.IdTransaccionGas]
            let sp : SleeveParams =
                { provider    = transact.buyer
                  transactionId = sl.IdTransaccionGas
                  flowDetailId = fd.IdFlowDetail
                  locationId  = transact.idPuntoEntrega
                  seller      = transact.seller
                  buyer       = transact.buyer
                  location    = transact.puntoEntrega
                  sleeveSide  = if sl.Side = "Export" then SleeveSide.Export else SleeveSide.Import
                  // TODO: Indice
                  index       = 0 // transact.idIndicePrecio |> Option.defaultValue 0
                  adder       = (transact.adder |> Option.defaultValue 0)  * 1.0m<USD/MMBTU>
                  contractRef = transact.contratRef
                  meta        = Map.empty }
            Some (fd.IdFlowDetail, sp)
    )
    |> Map.ofList


let buildTransportsDB idFlowMaster path : Map<flowId, TransportParams> =
    let detalles = getFlowDetailsByTipo idFlowMaster path "Transport"

    let transpFlow = ctx.Fm.Transport |> Seq.map (fun t -> t.IdFlowDetail, t) |> Map.ofSeq

    detalles
    |> List.choose (fun fd ->
            let tteFlow = transpFlow.[fd.IdFlowDetail]
            let trTte =  dTransTte.[tteFlow.IdTransaccionTransporte]
            let x = trTte.idRuta
            let ruta = dRuta.[trTte.idRuta]
            let cto = dCont.[trTte.idContrato]
            let tp : TransportParams =
                { provider    = dEnt.[cto.idContraparte].Nombre
                  transactionId = trTte.id
                  flowDetailId = fd.IdFlowDetail
                  providerId  = cto.idParte
                  pipeline    = "Gasoducto"
                  shipperId   = cto.idContraparte
                  routeId     = ruta.IdRuta
                  entry       = dPto.[ruta.IdPuntoRecepcion]
                  exit        = dPto.[ruta.IdPuntoEntrega]
                  shipper     = dEnt.[cto.idContraparte].Nombre
                  fuelMode    = if trTte.fuelMode = "RxBase" then FuelMode.RxBase else FuelMode.ExBase
                  fuelPct     = ruta.Fuel
                  CMD         = trTte.cmd  
                  usageRate   = trTte.usageRate 
                  meta        = Map.empty }
            Some (fd.IdFlowDetail, tp)
    )
    |> Map.ofList




// En el caso de Consume, sólo debería haber uno por FlowMaster, independiente del path.
let buildConsumeDB diaGas idFlowMaster path: Map<flowId, ConsumeParams> =
   let fdConsumo = ctx.Fm.FlowDetail 
                   |> Seq.find (fun fd -> fd.IdFlowMaster = idFlowMaster && tipoOperacionByDesc.Value.[ "Consume"] = fd.IdTipoOperacion)
   
   let idPunto, consumo = loadConsumo diaGas fdConsumo.IdFlowDetail |> Seq.head

   let consumeParams : ConsumeParams =
        { 
          gasDay   = diaGas
          flowDetailId = fdConsumo.IdFlowDetail
          location   = dPto.[idPunto]
          locationId = idPunto
          measured   =(consumo |> Option.defaultValue 0.0m) * 1.0m<MMBTU> // Placeholder, reemplazar con lógica de consumo real
        }

   Map.ofList[ (fdConsumo.IdFlowDetail, consumeParams) ]



let buildSellsDB diaGas idFlowMaster path : Map<flowId, SellParams list> =
     // Las ventas definidas para el path
    let flowDetail = getFlowDetailsByTipo idFlowMaster path "Sell" 
    if flowDetail.IsEmpty then
        Map.empty
    else
    let fdSellMap = flowDetail|> Seq.map(fun x -> x.IdFlowDetail, x.Referencia) |> Map
    let ventasRegistradas = ctx.Dbo.VentaGas |> Seq.filter (fun vg -> vg.DiaGas = do2dt diaGas )   
   
    let mapVentas = ventasRegistradas  |> Seq.map( fun vr ->
            let ref = fdSellMap.[vr.IdFlowDetail]
            let transact = dTransGas.[vr.IdTransaccion]
            let sp : SellParams =
                {   idVentaGas  = vr.IdVentaGas
                    location      = transact.puntoEntrega
                    gasDay      = diaGas
                    flowDetailId = vr.IdFlowDetail
                    transactionId = vr.IdTransaccion
                    seller      = transact.seller
                    sellerId    = transact.idSeller
                    buyerId     = transact.idBuyer
                    buyer       = transact.buyer
                    locationId  = transact.idPuntoEntrega
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

let buildFlowStepsDb (flowMasterId:FlowMasterId) (path: string) (diaGas: DateOnly) : FlowStep list =
    let fm =
        match tryFindFlowMaster flowMasterId  with
        | Some fm -> fm
        | None -> failwithf "No se encontró FlowMaster para el Id='%d'" flowMasterId


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
        let flowId = { flowMasterId = flowMasterId; path = path }

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
    (flowMasterId      : FlowMasterId)
    (diaGas    : DateOnly)
    : Map<FlowId, FlowStep list> =

    let fm = ctx.Fm.FlowMaster |> Seq.find (fun fm -> fm.IdFlowMaster = flowMasterId)
        

    let paths =
        ctx.Fm.FlowDetail
        |> Seq.filter (fun fd -> fd.IdFlowMaster = fm.IdFlowMaster)
        |> Seq.map (fun fd -> fd.Path)
        |> Seq.distinct
        |> Seq.toList

    paths
    |> List.map (fun path ->
        let fid = { flowMasterId = flowMasterId; path = path }
        fid, buildFlowStepsDb flowMasterId path diaGas)
    |> Map.ofList






