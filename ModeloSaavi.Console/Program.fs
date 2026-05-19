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


    

    let gasDay = DateOnly(2026, 3, 1)
    // CTM = 5
    // LR = 1
    // Bajio (EAVIII) = 2
    // ECHI = 4 
    // ESLP = 3


    let idPlanta = 5
    [11..11] |> List.iter (fun i ->

                match  FlowRunRepo.runFlowsAndPersistDBByPlanta idPlanta (gasDay.AddDays(i)) with
                | Ok results ->
                    printfn ""
                    printfn "Corrida batch OK"
                    printfn "================"

                    for r in results do
                        //printfn $"RunId        : {r.RunId}"
                        printfn ""

                    

                | Error e ->
                    printfn ""
                    printfn $"Error en corrida batch: {e}"
                    
            )
    0