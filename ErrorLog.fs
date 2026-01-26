module ErrorLog
open System
open Serilog
open Serilog.Events
open Serilog.Context
open Tipos
open Escenario

module Logging =

         // Datos comunes del día y hub
    let gasDay     = DateOnly(2026, 1, 20)
    let entryPt = Location "AguaDulce" //  Location "Ramones" // Location "Ehremberg"
    let buyer      = "EAVIII"  // Datos comunes del día y hub

    // Estado inicial
    let st0 : State =
        {   energy = 0.0m<MMBTU>
            owner    = buyer
            ownerId  = 1001
            transactionId =  0
            location = entryPt
            locationId = 501
            gasDay   = gasDay
            meta     = Map.empty }


    
    
    let init (logDir: string) =
        let path = System.IO.Path.Combine(logDir, "flow-.log")

        Log.Logger <-
        LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                path = path,
                rollingInterval = RollingInterval.Day,
                retainedFileCountLimit = 30,
                shared = true,
                flushToDiskInterval = TimeSpan.FromSeconds(1.0))
            .CreateLogger()

    let withRunContext flowMasterId (gasDay: DateOnly) (f: unit -> 'T) : 'T =
        //use _a = LogContext.PushProperty("runKey", runKey)
        use _a = LogContext.PushProperty("FlowMasterId", flowMasterId)
        use _d = LogContext.PushProperty("gasDay", gasDay.ToString("yyyy-MM-dd"))
        f()

    let logStageStart (stage: Stage) =
        Log.Information("Stage start {stage}", stage.ToString())

    let logStageOk (stage: Stage) =
        Log.Information("Stage ok {stage}", stage.ToString())

    let logStageError (stage: Stage) (err: obj) =
        // err puede ser DomainError o Exception
        Log.Error("Stage error {stage} err={err}", stage.ToString(), err)

    let logRunStarted () =
        Log.Information("Run started")

    let logRunOk (runId: int option) (trCount: int) =
        Log.Information("Run ok runId={runId} transitions={trCount}", (runId |> Option.defaultValue 0), trCount)

    let logRunFailed (err: obj) =
        Log.Error("Run failed err={err}", err)



module RunStages =
    open Logging

    let stage (st: Stage) (f: unit -> Result<'a, DomainError>) : Result<'a, DomainError> =
        try
        logStageStart st
        let r = f()
        match r with
        | Ok _ -> logStageOk st
        | Error e -> logStageError st e
        r
        with ex ->
        logStageError st ex
        Error (DomainError.Other $"[{st}] {ex.Message}")


module FlowApiModule =
    open System.Threading.Tasks
    open Logging
    open RunStages

    /// DTO de request (opcional, pero ordena)
    [<CLIMutable>]
    type RunFlowRequest =
      { GasDay       : DateOnly
        FlowMasterId : int }




    /// Excepción de dominio para que C# la capture fácil
    type FlowRunException(message: string, ?inner: exn) =
      inherit Exception(message, defaultArg inner null)

    type FlowApi() =
          /// Devuelve runId; si falla lanza FlowRunException
          static member RunFlowAndPersistAsync(req: RunFlowRequest) : Task<int> =
            task {
              try
                let st0 = st0

                let r =
                  withRunContext req.FlowMasterId req.GasDay (fun () ->
                    logRunStarted()

                    match runFlowAndPersistDB req.FlowMasterId  req.GasDay st0 with
                    | Ok (runId, _finalState, transitions) ->
                        logRunOk (Some runId) transitions.Length
                        Ok runId
                    | Error e ->
                        logRunFailed e
                        Error e
                  )

                match r with
                | Ok runId ->
                    return runId
                | Error e ->
                    let msg = $"Flow failed: {e}"
                    return! Task.FromException<int>(FlowRunException(msg))

              with ex ->
                // si ya es FlowRunException, propagala; si no, envolvé
                match ex with
                | :? FlowRunException ->
                    return! Task.FromException<int>(ex)
                | _ ->
                    return! Task.FromException<int>(FlowRunException("Unexpected error running flow", ex))
            }
