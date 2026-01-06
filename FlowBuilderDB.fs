module FlowBuilderDB

open System
open System.IO
open FsToolkit.ErrorHandling
open Tipos
open DefinedOperations
open Helpers
open DbContext
open Gnx.Persistence.SQL_Data


let transactions = loadTransacciones() |> List.map (fun t -> t.id, t) |> Map.ofList


let buildSupplysDB diaGas idFlowDetail : Map<string, SupplyParams list> = 
    let compraGas = loadCompraGas diaGas idFlowDetail
    let dEntidadLegal = ctx.Dbo.EntidadLegal |> Seq.map(fun e -> e.IdEntidadLegal, e) |> Map.ofSeq
    let dContrato = loadContratos() |> List.map (fun c -> c.id, c) |> Map.ofList
    let dTradingHub = ctx.Platts.IndicePrecio |> Seq.map(fun th -> th.IdIndicePrecio, th.Nemonico) |> Map.ofSeq
    let dPunto = ctx.Dbo.Punto |> Seq.map(fun p -> p.IdPunto, p.Codigo) |> Map.ofSeq

    compraGas|> List.map(fun cg ->
        
        let nominado = cg.nominado 
        let confirmado = None
        let transact = transactions.[cg.idTransaccion]
        let contrato = dContrato.[transact.idContrato]
        let buyer =  dEntidadLegal.[contrato.idContraparte].Nombre |> Option.defaultValue "N/A"
        let seller = dEntidadLegal.[contrato.idParte].Nombre |> Option.defaultValue "N/A"   
        let tradingHubNemo = dTradingHub.[transact.idIndicePrecio |> Option.defaultValue 0]

        let sp : SupplyParams =
                { 
                tcId        = contrato.codigo
                gasDay      = diaGas
                tradingHub  = parseTradingHub tradingHubNemo
                temporalidad= parseTemporalidad cg.temporalidad
                deliveryPt  = dPunto.[cg.idPuntoEntrega]
                seller      = seller
                buyer       = buyer
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


   


        
let buildTransportsDB modo central : Map<string, TransportParams> = failwith "Not implemented"

let buildConsumesDB modo central : Map<string, ConsumeParams> = failwith "Not implemented"

let buildSellsDB modo central : Map<string, SellParams> = failwith "Not implemented"


let buildFlowSteps modo central path diaGas : FlowStep list = failwith "Not implemented"

let getFlowSteps
    (modo      : string)
    (central   : string)
    (diaGas    : DateOnly)
    : Map<FlowId, FlowStep list> = failwith "Not implemented"





