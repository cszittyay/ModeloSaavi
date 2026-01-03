module FlowBuilderDB

open System
open System.IO
open FsToolkit.ErrorHandling
open Tipos
open DefinedOperations
open Helpers
open DbContext




let buildTransportsDB modo central : Map<string, TransportParams> = failwith "Not implemented"

let buildConsumesDB modo central : Map<string, ConsumeParams> = failwith "Not implemented"

let buildSellsDB modo central : Map<string, SellParams> = failwith "Not implemented"


let buildFlowSteps modo central path diaGas : FlowStep list = failwith "Not implemented"

let getFlowSteps
    (modo      : string)
    (central   : string)
    (diaGas    : DateOnly)
    : Map<FlowId, FlowStep list> = failwith "Not implemented"





