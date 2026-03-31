open System
open Tipos
open Helpers
open Escenario
open Tipos
open ProjectOperations
open ErrorLog.Logging
open ErrorLog.RunStages


open System

[<EntryPoint>]
let main argv =


    let gasDay  = DateOnly(2026, 3, 26)
    let entryPt = Location "AguaDulce"
    let buyer   = "EAVIII"

    let st0 : State =
        { energy        = 0.0m<MMBTU>
          owner         = buyer
          ownerId       = 1001
          transactionId = 0
          location      = entryPt
          locationId    = 501
          gasDay        = gasDay
          meta          = Map.empty }



    // Orden explícito de ejecución
    let flowMasterIds = [ 12 ]

    // Estado inicial por flow
    let initialByFlow : Map<int, State> =
        [
            12, st0
        ]
        |> Map.ofList

    match Escenario.runFlowsAndPersistDB flowMasterIds gasDay initialByFlow with
    | Ok results ->
        printfn ""
        printfn "Corrida batch OK"
        printfn "================"

        for r in results do
            printfn $"FlowMasterId : {r.FlowMasterId}"
            printfn $"RunId        : {r.RunId}"
            printfn $"Transitions  : {r.Transitions.Length}"
            printfn $"Final energy : {r.FinalState.energy}"
            printfn ""

        0

    | Error e ->
        printfn ""
        printfn $"Error en corrida batch: {e}"
        1
