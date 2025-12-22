module ErrorLog
open System
open Serilog
open Serilog.Events
open Serilog.Context
open Tipos

module Logging =

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

    let withRunContext (runKey: Guid) (modo: string) (central: string) (gasDay: DateOnly) (f: unit -> 'T) : 'T =
        use _a = LogContext.PushProperty("runKey", runKey)
        use _b = LogContext.PushProperty("modo", modo)
        use _c = LogContext.PushProperty("central", central)
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
