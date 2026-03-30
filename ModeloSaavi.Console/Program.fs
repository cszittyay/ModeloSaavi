open System
open Tipos
open Helpers
open Escenario
open Tipos
open ProjectOperations
open ErrorLog.Logging
open ErrorLog.RunStages
open DbContext
open ModeloSaavi.Infrastructure
open Gnx.Persistence

// Hola




let rec loop () =
    // --- tu función a ejecutar ---
    do
        printfn "Ejecutando..."
        let modo = "CUR"
        let central = "EBC"
        let gasDay = DateOnly(2026,2,11)
        let ctxFactory () = DbContext.createCtxWithConnectionString connectionString
        let lc = LoadContext.create ctxFactory gasDay
        

        init "logs"
  
        let flowMasterId = 1

        let result =
            withRunContext flowMasterId  gasDay (fun () ->
            logRunStarted()

            // Acá llamás tu función real
            match runFlowAndPersistDB lc flowMasterId gasDay st0 with
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

[<EntryPoint>]
let main _argv =
    loop ()
    0



