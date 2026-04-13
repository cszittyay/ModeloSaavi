module FlowBuilderDB

open System
open System.IO
open System.Collections.Generic
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
    let dFlowDetailByMasterPath =  flowDetailsByMasterPath().Value
    match dFlowDetailByMasterPath.TryFind (idFlowMaster, path) with
    | None -> Seq.empty
    | Some s -> s

let private getFlowDetailsByTipo (idFlowMaster: int) (path: string) (tipoOpDesc: string) =
    match tipoOperacionByDesc().Value.TryFind tipoOpDesc with
    | None -> []
    | Some idTipo ->
        getFlowDetails idFlowMaster path
        |> Seq.filter (fun fd -> fd.IdTipoOperacion = idTipo)
        |> Seq.toList

let toErrorDto (e: DomainError) : FlowRunErrorDto =
  match e with
  | MissingFlowMaster id ->
      { Code = "FLOW_MASTER_NOT_FOUND"
        Message = $"No existe FlowMaster id={id}."
        Details = Some $"Path:S/D " }

  | MissingTradeForFlowDetail (fdId, fmId, path) ->
      { Code = "TRADE_NOT_FOUND_FOR_FLOW_DETAIL"
        Message = $"Falta Trade para FlowDetail id={fdId} (FlowMaster={fmId}, path={path})."
        Details = Some "S/D" }
 
// =====================================================================================
// Para un FlowMaster y un path: sólo un idFlowDetail de tipo "Supply" (asumido)
let buildSupplysDB
    (diaGas: DateOnly)
    (idFlowMaster: int)
    (path: string)
    : Result<Map<flowId, SupplyParams list>, DomainError> =

    let dTransGas = transaccionesGasById().Value
    

    match Map.tryFind idFlowMaster dFlowMaster with
    | None -> Error (MissingFlowMaster idFlowMaster)
    | Some flowMaster ->
        let fmNombre = flowMaster.Nombre.Value
        match getFlowDetailsByTipo flowMaster.IdFlowMaster path "Supply" |> List.tryFind (fun fd -> fd.Path = path) with
        | None -> Error (MissingSupplyFlowDetail (fmNombre, diaGas, path))
        | Some fdSupply ->
            let idFlowDetail = fdSupply.IdFlowDetail
            let compraGas = loadCompraGas diaGas idFlowDetail
            if compraGas.Length = 0 then Error (MissingSupplyFlowDetail (fmNombre, diaGas, path))
            else
            compraGas
            |> List.fold (fun acc cg ->
                acc
                |> Result.bind (fun m ->
                    let transact = dTransGas.[cg.idTransaccion]
                    let contrato = dCont.[transact.idContrato]

                    let sp : SupplyParams =
                        { 
                        gasDay         = diaGas
                        transactionId  = transact.id
                        buyerId        = transact.idBuyer
                        sellerId       = transact.idSeller
                        flowDetailId   = idFlowDetail
                        buyer          = transact.buyer
                        seller         = transact.seller
                        temporalidad   = parseTemporalidad cg.temporalidad
                        deliveryPt     = transact.puntoEntrega
                        deliveryPtId   = cg.idPuntoEntrega
                        qEnergia       = cg.nominado * 1.0m<MMBTU>
                        index          = cg.idIndicePrecio |> Option.defaultValue 0
                        adder          = (cg.adder  |> Option.defaultValue 0.0m) * 1.0m<USD/MMBTU>
                        price          = (cg.precio |> Option.defaultValue 0.0m) * 1.0m<USD/MMBTU>
                        contractRef    = contrato.codigo
                        meta           = Map.empty }

                    // acumular lista por FlowId
                    let prev = Map.tryFind idFlowDetail m |> Option.defaultValue []
                    Ok (Map.add idFlowDetail (sp :: prev) m)
                )
            ) (Ok Map.empty)
            // opcional: si querés preservar el orden original de compraGas
            |> Result.map (Map.map (fun _ lst -> List.rev lst))


let buildTradesDB idFlowMaster path : Result<Map<flowId, TradeParams>, DomainError> =
    match Map.tryFind idFlowMaster dFlowMaster with
    | None -> Error (MissingFlowMaster idFlowMaster)
    | Some flowMaster ->
        let tradeGas = getFlowDetailsByTipo flowMaster.IdFlowMaster path "Trade"
        let dTransGas = transaccionesGasById().Value
        let dTradeByFlowDetail = tradeByFlowDetailId().Value
        tradeGas
        |> List.fold (fun acc fd ->
            acc
            
            |> Result.bind (fun m ->
                match Map.tryFind fd.IdFlowDetail dTradeByFlowDetail with
                | None ->  Error (MissingTradeForFlowDetail (flowMaster.Nombre.Value, fd.IdFlowDetail,  path))
                | Some trade ->

                    let transact = dTransGas.[trade.IdTransaccionGas]

                    let tp : TradeParams =
                        { side = if trade.Side = "Sell" then TradeSide.Sell else TradeSide.Buy
                          transactionId = trade.IdTransaccionGas
                          flowDetailId = fd.IdFlowDetail
                          buyer = transact.buyer
                          buyerId = transact.idBuyer
                          sellerId = transact.idSeller
                          seller = transact.seller
                          adder = (transact.adder |> Option.defaultValue 0.0m) * 1.0m<USD/MMBTU>
                          price = 0.0m<USD/MMBTU> // TODO
                          locationId = transact.idPuntoEntrega
                          location = transact.puntoEntrega
                          meta = Map.empty }

                    Ok (Map.add fd.IdFlowDetail tp m)
            )
        ) (Ok Map.empty)


let buildSleevesDB idFlowMaster path : Result<Map<flowId, SleeveParams>, DomainError> =

    // Si idFlowMaster acá es el IdFlowMaster “real”, ok.
    // Si en otros módulos usás flowMasterById, mantené consistencia.
    let dTransGas = transaccionesGasById().Value
    match Map.tryFind idFlowMaster dFlowMaster with
    | None ->  Error (MissingFlowMaster idFlowMaster)
    | Some flowMaster ->

        let detalles = getFlowDetailsByTipo flowMaster.IdFlowMaster path "Sleeve"
        let dSleeve =
            ctx.Fm.Sleeve
            |> Seq.map (fun s -> s.IdFlowDetail, s)
            |> Map.ofSeq

        detalles
        |> List.fold (fun acc fd ->
            acc
            |> Result.bind (fun m ->

                match Map.tryFind fd.IdFlowDetail dSleeve with
                | None ->  Error (MissingSleeveFlowDetail (flowMaster.Nombre.Value, fd.IdFlowDetail,  path))
                | Some sl ->
                    let transact = dTransGas.[sl.IdTransaccionGas]

                    let sp : SleeveParams =
                            { provider      = transact.buyer
                              transactionId = sl.IdTransaccionGas
                              flowDetailId  = fd.IdFlowDetail
                              locationId    = transact.idPuntoEntrega
                              seller        = transact.seller
                              buyer         = transact.buyer
                              location      = transact.puntoEntrega
                              sleeveSide    = if sl.Side = "Export" then SleeveSide.Export else SleeveSide.Import
                              index         = 0 // TODO
                              adder         = (transact.adder |> Option.defaultValue 0.0m) * 1.0m<USD/MMBTU>
                              contractRef   = transact.contratRef
                              meta          = Map.empty }

                    Ok (Map.add fd.IdFlowDetail sp m)
            )
        ) (Ok Map.empty)

        
let tryGetPoolFromCtx (ctx: SharedTransportContext) : TryGetCapacityPool =
  fun tfId ->
    match Map.tryFind tfId ctx.Pools with
    | Some pool -> Ok pool
    | None -> Error (Other $"No existe pool para TF={tfId}")

let buildTransportsDB idFlowMaster path : Result<Map<flowId, TransportParams>, DomainError> =

  match Map.tryFind idFlowMaster dFlowMaster with
  | None -> Error (MissingFlowMaster idFlowMaster)
  | Some flowMaster ->

      let detalles   = getFlowDetailsByTipo flowMaster.IdFlowMaster path "Transport"
      let transpFlow = ctx.Fm.Transport |> Seq.map (fun t -> t.IdFlowDetail, t) |> Map.ofSeq
      let dRuta      = rutaById()
      let dTransTte  = transaccionesTransporteById().Value

      detalles
      |> List.fold (fun acc fd ->
          acc
          |> Result.bind (fun m ->

              match Map.tryFind fd.IdFlowDetail transpFlow with
              | None ->
                  Error (MissingTransportFlowDetail (flowMaster.Nombre.Value, fd.IdFlowDetail, path))

              | Some tteFlow ->

                  // trTF : TransaccionTransporte option
                  let trTF =
                    tteFlow.IdTransaccionTf
                    |> Option.map (fun id -> dTransTte.[id])

                  // trTI : TransaccionTransporte option
                  let trTI =
                    tteFlow.IdTransaccionTi
                    |> Option.map (fun id -> dTransTte.[id])


                // helper (si querés local)
                  let pickTransaccion trTF trTI =
                    match trTF, trTI with
                    | Some tf, _     -> Ok tf
                    | None, Some ti  -> Ok ti
                    | None, None     -> Error (MissingTransportTransaction (flowMaster.Nombre.Value, fd.IdFlowDetail, path)) // <-- OJO: fd no existe acá

      
                  // Elegir una transacción “base” para ruta/contrato, o error si faltan ambas
                  let pickTransaccion trTF trTI =
                    match trTF, trTI with
                    | Some tf, _     -> Ok tf
                    | None, Some ti  -> Ok ti
                    | None, None     -> Error (MissingTransportTransaction (flowMaster.Nombre.Value, fd.IdFlowDetail, path))

                  pickTransaccion trTF trTI
                  |> Result.bind (fun trTte ->

                      let ruta  = dRuta.[trTte.idRuta]
                      let cto   = dCont.[trTte.idContrato]

                      let contraparte = dEnt.[cto.idContraparte]
                      let entryPto    = dPto.[ruta.IdPuntoRecepcion]
                      let exitPto     = dPto.[ruta.IdPuntoEntrega]

                      let tp : TransportParams =
                        { provider      = contraparte.Nombre
                          transactionTF = trTF |> Option.map (fun x -> x.id)
                          transactionTI = trTI |> Option.map (fun x -> x.id)
                          flowDetailId  = fd.IdFlowDetail
                          providerId    = cto.idParte
                          pipeline      = if ruta.IdGasoducto.IsSome then dGasoducto.[ruta.IdGasoducto.Value].Nombre else "S/D"
                          shipperId     = cto.idContraparte
                          routeId       = ruta.IdRuta
                          entry         = entryPto
                          exit          = exitPto
                          shipper       = contraparte.Nombre
                          fuelMode      = if trTte.fuelMode = "RxBase" then FuelMode.RxBase else FuelMode.ExBase
                          fuelPct       = ruta.Fuel
                          CDC           = trTte.cmd
                          usageRate     = trTte.usageRate
                          meta          = Map.empty }

                      Ok (Map.add fd.IdFlowDetail tp m)
                  )
          )
      ) (Ok Map.empty)





// En el caso de Consume, sólo debería haber uno por FlowMaster, independiente del path.
let buildConsumeDB
    (diaGas: DateOnly)
    (idFlowMaster: int)
    (path: string)
    : Result<Map<flowId, ConsumeParams>, DomainError> =

    // 1) Resolver IdTipoOperacion "Consume" sin indexador
    match Map.tryFind "Consume" dTipoOperacionByDesc with
    | None -> Error (MissingFlowType "Consume")   // <- agregá este caso o mapealo a uno existente
    | Some idTipoConsume ->

        let fmNombre = dFlowMaster.[idFlowMaster].Nombre.Value
        let idCliente = dFlowMaster.[idFlowMaster].IdCliente


        match ctx.Fm.FlowDetail |> Seq.tryFind (fun fd -> fd.IdFlowMaster = idFlowMaster && fd.IdTipoOperacion = idTipoConsume) with
        | None -> Error (MissingConsumoForFlowDetail (fmNombre, diaGas, path))  // <- o el error que uses para "no existe Consume"

        | Some fdConsumo ->

            // 3) Cargar consumo (puede venir vacío)
            match loadConsumo diaGas idCliente |> Seq.tryHead with
            | None -> Error (MissingConsumoForFlowDetail (fmNombre, diaGas, path))
            | Some (idPunto, consumo) ->


                    let consumeParams : ConsumeParams =
                        { gasDay      = diaGas
                          flowDetailId = fdConsumo.IdFlowDetail
                          location     = dPto.[idPunto]
                          locationId   = idPunto
                          measured     = (consumo |> Option.defaultValue 0.0m) * 1.0m<MMBTU> }

                    Ok (Map.ofList [ (fdConsumo.IdFlowDetail, consumeParams) ])


let buildSellsDB
    (diaGas: DateOnly)
    (idFlowMaster: int)
    (path: string)
    : Result<Map<flowId, SellParams list>, DomainError> =

    let flowDetails = getFlowDetailsByTipo idFlowMaster path "Sell"
    let ventas = ventasByFlowDetailId diaGas (flowDetails |> List.map (fun fd -> fd.IdFlowDetail))
    let dTransGas = transaccionesGasById().Value

    // Si no hay sells definidos para ese path, esto puede ser válido:
    if List.isEmpty flowDetails || List.isEmpty ventas then
        Ok Map.empty
    else

        // Map: FlowDetailId -> Referencia
        let fdSellRefById : Map<int, string> =
            flowDetails
            |> Seq.map (fun x -> x.IdFlowDetail, x.Referencia)
            |> Map.ofSeq


        ventas
        |> List.fold (fun acc vr ->
            acc
            |> Result.bind (fun m ->

                // 1) referencia del FlowDetail
                match Map.tryFind vr.IdFlowDetail fdSellRefById with
                | None -> Error (MissingSellFlowDetail (vr.IdFlowDetail, diaGas, path))
                | Some contractRef ->

                    let transact = dTransGas.[vr.IdTransaccion]
                    // 2) transacción gas
                    let sp : SellParams =
                            { idVentaGas   = vr.IdVentaGas
                              location     = transact.puntoEntrega
                              gasDay       = diaGas
                              flowDetailId = vr.IdFlowDetail
                              transactionId = vr.IdTransaccion
                              seller       = transact.seller
                              sellerId     = transact.idSeller
                              buyerId      = transact.idBuyer
                              buyer        = transact.buyer
                              locationId   = transact.idPuntoEntrega
                              qty          = vr.CantidadMmBtu * 1.0m<MMBTU>
                              price        = vr.PrecioUsd * 1.0m<USD/MMBTU>
                              adder        = 0.0m<USD/MMBTU>
                              contractRef  = contractRef
                              meta         = Map.empty }

                        // 3) agrupar por FlowId acumulando lista
                    let prev = Map.tryFind vr.IdFlowDetail m |> Option.defaultValue []
                    Ok (Map.add vr.IdFlowDetail (sp :: prev) m)
            )
        ) (Ok Map.empty)
        |> Result.map (Map.map (fun _ xs -> List.rev xs))

    

// =====================================================================================
// FlowSteps (equivalente a buildFlowSteps / getFlowSteps de Excel)
// =====================================================================================

let buildFlowStepsDb (flowMasterId: FlowMasterId) (path: string) (diaGas: DateOnly) : Result<FlowStep list, DomainError> =

    let fm =
        match tryFindFlowMaster flowMasterId with
        | Some fm -> fm
        | None -> failwithf "No se encontró FlowMaster para el Id='%d'" flowMasterId

    let tryFindOr (defaultValue: 'a) (fdId: int) (r: Result<Map<int,'a>, 'e>) : Result<'a,'e> =
        r |> Result.map (fun m -> Map.tryFind fdId m |> Option.defaultValue defaultValue)

    let supplies   = buildSupplysDB diaGas fm.IdFlowMaster path
    let trades     = buildTradesDB fm.IdFlowMaster path
    let sleeves    = buildSleevesDB fm.IdFlowMaster path
    let transports = buildTransportsDB fm.IdFlowMaster path
    let consumes   = buildConsumeDB diaGas fm.IdFlowMaster path
    let sells      = buildSellsDB diaGas fm.IdFlowMaster path

    let flowDetails =
        getFlowDetails fm.IdFlowMaster path
        |> Seq.toList
        |> List.sortBy (fun fd -> fd.Orden)

    let buildBlock (tipoDesc: string) (fd: FlowDetail) : Result<Block, DomainError> =
        match tipoDesc with
        | "Supply" ->
            supplies |> Result.bind (fun m ->
                match Map.tryFind fd.IdFlowDetail m with
                | Some sp -> Ok (SupplyMany sp)
                | None    -> Error (MissingSupplyFlowDetail (fm.Nombre.Value, diaGas, path)))

        | "Trade" ->
            trades |> Result.bind (fun m ->
                match Map.tryFind fd.IdFlowDetail m with
                | Some tp -> Ok (Trade tp)
                | None    -> Error (MissingTradeForFlowDetail (fm.Nombre.Value, fd.IdFlowDetail, path)))

        | "Transport" ->
            transports |> Result.bind (fun m ->
                match Map.tryFind fd.IdFlowDetail m with
                | Some tp -> Ok (Transport tp)
                | None    -> Error (MissingTransportFlowDetail (fm.Nombre.Value, fd.IdFlowDetail, path)))

        | "Sleeve" ->
            sleeves |> Result.bind (fun m ->
                match Map.tryFind fd.IdFlowDetail m with
                | Some sl -> Ok (Sleeve sl)
                | None    -> Error (MissingSleeveFlowDetail (fm.Nombre.Value, fd.IdFlowDetail, path)))

        | "Sell" ->
            tryFindOr [] fd.IdFlowDetail sells
            |> Result.map SellMany

        | "Consume" ->
            consumes |> Result.bind (fun m ->
                match Map.tryFind fd.IdFlowDetail m with
                | Some cp -> Ok (Consume cp)
                | None    -> Error (MissingConsumoForFlowDetail (fm.Nombre.Value, diaGas, path)))

        | other ->
            Error (MissingFlowType other)

    flowDetails
    |> List.map (fun fd ->
        let tipoDesc =
            dTipoOperacionById
            |> Map.tryFind fd.IdTipoOperacion
            |> Option.defaultValue "<unknown>"
        buildBlock tipoDesc fd
        |> Result.map (fun block ->
            { flowId  = { flowMasterId = flowMasterId; path = path }
              order   = fd.Orden
              block   = block
              joinKey = fd.JoinKey
              ref     = fd.Path })
    )
    |> List.sequenceResultM

let getFlowStepsDB
    (flowMasterId      : FlowMasterId)
    (diaGas    : DateOnly)
    : Result<Map<FlowId, FlowStep list>, DomainError> =



    match ctx.Fm.FlowMaster |> Seq.tryFind (fun fm -> fm.IdFlowMaster = flowMasterId) with
    | None -> Error (MissingFlowMaster flowMasterId)
    | Some fm ->
        let paths =
            ctx.Fm.FlowDetail
            |> Seq.filter (fun fd -> fd.IdFlowMaster = fm.IdFlowMaster)
            |> Seq.map (fun fd -> fd.Path)
            |> Seq.distinct
            |> Seq.toList

        if paths.Length = 0 then Error (MissingFlowDetail (fm.Nombre.Value))
        else
            paths
            |> List.map (fun path ->
                let fid = { flowMasterId = flowMasterId; path = path }
                buildFlowStepsDb flowMasterId path diaGas
                |> Result.map (fun steps -> fid, steps))
            |> List.sequenceResultM
            |> Result.map Map.ofList





