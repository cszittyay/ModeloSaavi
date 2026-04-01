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


    

    let gasDay = DateOnly(2026, 3, 26)
    let idPlanta = 1

    match  runFlowsAndPersistDBByPlanta idPlanta gasDay with
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
