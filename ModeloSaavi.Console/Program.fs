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


    

    let gasDay = DateOnly(2026, 4, 2)
    // CTM = 5
    // LR = 1
    let idPlanta = 1

    match  FlowRunRepo.runFlowsAndPersistDBByPlanta idPlanta gasDay with
    | Ok results ->
        printfn ""
        printfn "Corrida batch OK"
        printfn "================"

        for r in results do
            printfn $"RunId        : {r.RunId}"
            printfn ""

        0

    | Error e ->
        printfn ""
        printfn $"Error en corrida batch: {e}"
        1
