module ErrorLog

open System
open System.Threading.Tasks
open Serilog
open Serilog.Events
open Serilog.Context
open Tipos
open Escenario

module Logging =

    let gasDay  = DateOnly(2026, 1, 20)
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
        use _a = LogContext.PushProperty("FlowMasterId", flowMasterId)
        use _d = LogContext.PushProperty("gasDay", gasDay.ToString("yyyy-MM-dd"))
        f()

    let logStageStart (stage: Stage) =
        Log.Information("Stage start {stage}", stage.ToString())

    let logStageOk (stage: Stage) =
        Log.Information("Stage ok {stage}", stage.ToString())

    let logStageError (stage: Stage) (err: obj) =
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
    open Logging
    open RunStages

    [<CLIMutable>]
    type RunFlowRequest =
      { GasDay       : DateOnly
        FlowMasterId : int }

    type FlowRunException(message: string, ?inner: exn) =
      inherit Exception(message, defaultArg inner null)

    type FlowApi() =
      static member RunFlowAndPersistSafeAsync(req: RunFlowRequest) : Task<RunFlowOutcome> =
        task {
          let initialState = Logging.st0

          let mkErr code msg details =
            { Ok = false
              RunId = None
              Error = Some { Code = code; Message = msg; Details = details } }

          try
            let r =
              match Escenario.initSharedTransportContext req.GasDay with
              | Error e -> Error e
              | Ok sharedTransportCtx ->
                  let tryGetPool = Escenario.mkTryGetPool sharedTransportCtx

                  Logging.withRunContext req.FlowMasterId req.GasDay (fun () ->
                    Logging.logRunStarted()

                    match Escenario.runFlowAndPersistDB tryGetPool req.FlowMasterId req.GasDay initialState with
                    | Ok (runId, _finalState, transitions) ->
                        Logging.logRunOk (Some runId) transitions.Length
                        Ok runId
                    | Error e ->
                        Logging.logRunFailed e
                        Error e
                  )

            match r with
            | Ok runId ->
                return
                  { Ok = true
                    RunId = Some runId
                    Error = None }

            | Error e ->
                return mkErr "FlowFailed" $"Flow failed: {e}" None

          with ex ->
            return mkErr "Unexpected" "Unexpected error running flow" (Some ex.Message)
        }
