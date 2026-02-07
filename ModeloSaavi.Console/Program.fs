open System
open Tipos
open Helpers
open Escenario
open Tipos
open ProjectOperations
open ErrorLog.Logging
open ErrorLog.RunStages


// Hola

let rec loop () =
    // --- tu función a ejecutar ---
    do
        printfn "Ejecutando..."
        let modo = "CUR"
        let central = "EBC"
        let gasDay = DateOnly(2026,2,3)

        let conn =  DbContext.connectionString

        init "logs"
  
        let flowMasterId = 1

        let result =
            withRunContext flowMasterId  gasDay (fun () ->
            logRunStarted()

            // Acá llamás tu función real
            match runFlowAndPersistDB  flowMasterId gasDay st0 with
            | Ok (runId, finalState, transitions) ->
                logRunOk (Some runId) transitions.Length
                Ok runId
            | Error e ->
                logRunFailed e
                Error e
        )

    // --- esperar comando ---
        let rec ask () =
            printf "Re-ejecutar? (S/N): "
            let key = Console.ReadKey(intercept = true).Key
            Console.WriteLine()

            match key with
            | ConsoleKey.S ->
                loop ()
            | ConsoleKey.N ->
                printfn "Fin."
            | _ ->
                printfn "Tecla inválida. Use S o N."
                ask ()

        ask ()

// Entry point
[<EntryPoint>]
let main _argv =
    loop ()
    0



