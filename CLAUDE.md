# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# ModeloSaavi

F# .NET 10.0 library for natural gas flow modeling, composition, and persistence.

## Build & Run

```bash
dotnet restore
dotnet build
```

The ad-hoc console runner lives in `ModeloSaavi.Console/` (separate `.fsproj`). To run it:

```bash
cd ModeloSaavi.Console
dotnet run
```

There are no automated tests in this project. `Program.fs` (root) and the files under `NominacionTte/` are not compiled — they are dead code.

## Architecture

The library models a **physical and commercial gas chain** using Railway-Oriented Programming (ROP).

### Core abstraction

```fsharp
type Operation = State -> Result<Transition, DomainError>
```

Operations are composed via **Kleisli** (`Kleisli.fs`) operators (`>=>` and `>>+`), though the actual flow execution goes through `runSteps` in `Helpers.FlowBuilderUtils`, which folds over a `FlowStep list` accumulating `Transition list`. `Kleisli` is available for manual composition outside the DB-driven flow.

### Key types (`Tipos.fs`)

| Type | Purpose |
|------|---------|
| `State` | Current gas position (energy, owner, location, gasDay) |
| `Transition` | Result of one operation: new State + costs + notes |
| `Block` | DU of atomic operations: Supply, SupplyMany, Transport, Trade, Sleeve, Sell, SellMany, Consume |
| `FlowDef` | `Linear`, `Join`, or `MultiJoin` (chain of joins) — topology of a flow |
| `FlowStep` | One step within a flow path, carries `Block + joinKey + ref` |
| `DomainError` | All domain-level failures |

### Flow topologies (`FlowDef`)

- **Linear**: single path, steps executed sequentially.
- **Join**: multiple `Contributor` paths merge at one `joinKey`; optional `Final` path continues after.
- **MultiJoin**: chain of `JoinStage`s — each stage has contributors + optional bridge from the previous stage.

`buildFlowDef` (in `Helpers.FlowBuilderUtils`) infers topology from the `joinKey` fields on `FlowStep`s. `runFlow` dispatches to `runJoinNPaths` or `runMultiJoin` accordingly.

### Units of measure

`MMBTU`, `GJ`, `USD`, `MXN` — always use typed `decimal<MMBTU>` / `decimal<USD/MMBTU>`.

### Module load order (fsproj)

Files must be listed in dependency order in `ModeloSaavi.fsproj`. Adding a new module: place it after all its dependencies.

```
Tipos → DbContext → Persistence → TiposDTO → Mapping → ResutlRows →
LoadFromSQL → DefinedOperations → Helpers → ProjectOperations →
Repository → Kleisli → FlowBuilderDB → Escenario → ErrorLog
```

## DB access layer

### Connection string

`DbContext.fs` holds a `[<Literal>]` connection string pointing to the UAT SQL Server (AWS RDS). To switch databases at runtime use:

```fsharp
DbContext.DbFactory.SetGlobalContext(myConnString)   // mutates the global ctx
// or per-call:
let ctx2 = DbContext.DbFactory.GetContext(myConnString)
```

### SQLProvider `ctx`

`ctx` (type `sqlGnx.dataContext`) exposes schema-typed tables via `ctx.Dbo.*`, `ctx.Fm.*`, `ctx.Platts.*`. Use it for reads. For writes, always go through `Repository.Tx.withTransaction`.

### Pre-loaded lookup maps (`Gnx.Persistence.SQL_Data`)

`LoadFromSQL.fs` (namespace `Gnx.Persistence`, module `SQL_Data`) has two distinct patterns:

- **Module-level values** — loaded eagerly once at module initialization: `dFlowMaster`, `dEnt`, `dPto`, `dCont`, `dGasoducto`, `dCliente`, `dTipoOperacionById`, `dTipoOperacionByDesc`. These are true singletons.
- **Functions returning `lazy`** — e.g. `transaccionesGasById()`, `transaccionesTransporteById()`, `flowDetailsByMasterPath()`, `tradeByFlowDetailId()`. Each call returns a **new** `Lazy<T>` instance; calling `.Value` queries the DB for that instance. Assign the result to a local `let` binding and call `.Value` once per logical scope to avoid redundant queries.
- **Functions returning `Map` directly** — `rutaById()`, `loadCompraGas`, `loadConsumo`, `ventasByFlowDetailId` query the DB on every call; they have no lazy wrapper.

### Transport capacity pool (`SharedTransportContext`)

`CapacityPool` tracks remaining CDC (firm capacity) per `TransactionId` across an entire batch run. It is built once per `gasDay` via `buildCapacityPoolsFromTransactions` and injected into every `Transport.transport` operation as `TryGetCapacityPool`. When a `TFandTI` transport is present, the pool is consumed first by the Firm (TF) leg; overflow goes to the Interruptible (TI) leg.

## Execution entry points

All public entry points are in `Escenario.FlowRunRepo`:

| Function | Description |
|---|---|
| `runFlowAndPersistDB` | Run + persist a single `FlowMaster` for one `gasDay` |
| `runFlowsAndPersistDB` | Run a list of `FlowMaster` IDs (shares transport pool) |
| `runFlowsAndPersistDBByPlanta` | Resolve IDs by `idPlanta`, then call above |
| `runFlowBatchIdsByPlanta` | Same as above; returns `int list` of `RunId`s |

Execution order: `getFlowStepsDB` → `buildFlowDef` → `runFlow` → `withTransaction (insertRunId + projectRows + persistAll)`.

`getFlowStepsDB` discovers all distinct `Path` values for a `FlowMaster` and calls `buildFlowStepsDb` per path. `buildFlowStepsDb` does the per-path DB resolution (supplies, trades, transports, sleeves, sells, consumes) and assembles `FlowStep list`.

`buildInitialFlowState` in `Escenario.fs` hardcodes the starting `State` (owner `"EAX"`, ownerId `1001`, location `"AguaDulce"`, locationId `501`). Adjust if running against a different entity or hub.

Missing Supply or Consume data causes a `[SKIP]` (not an error) when batching by planta.

### Console `idPlanta` reference values

| Planta | idPlanta |
|---|---|
| LR | 1 |
| Bajio (EAVIII) | 2 |
| ECHI | 4 |
| CTM | 5 |

## Key dependencies

| Package | Purpose |
|---------|---------|
| `FsToolkit.ErrorHandling` | `result {}` CE, `Result.map`, `Result.bind` |
| `SQLProvider` | Type-safe SQL access via `ctx` (DbContext) |
| `Microsoft.Data.SqlClient` | Raw ADO.NET transactions in `Repository.Tx` |
| `Serilog` | Structured logging; initialized with `ErrorLog.Logging.init "logs"` |

## Patterns to follow

- **Error handling**: always `Result<'T, DomainError>`, never exceptions from domain logic.
- **DB reads**: use module-level maps from `SQL_Data` or lazy lookups; avoid ad-hoc `ctx` queries inside builders.
- **DB writes**: use `Repository.Tx.withTransaction`.
- **Flow execution**: `FlowBuilderDB` builds `FlowDef` from DB; `Helpers.FlowBuilderUtils.runFlow` executes it.
- **Transition notes**: use `Helpers.Meta.require<'T>` / `Meta.tryGet<'T>` to read typed values from `Transition.notes`.
- **Persistence**: `ProjectOperations.projectRows` converts `Transition list` → `ProjectedRows`; `Repository.DetailRepo.persistAll` writes them in the same transaction as `FlowRun`.
