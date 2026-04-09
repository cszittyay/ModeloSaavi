# ModeloSaavi

F# .NET 8.0 library for natural gas flow modeling, composition, and persistence.

## Build & Run

```bash
dotnet build
dotnet restore
```

The console entry point (`Program.fs`) is excluded from the project compile by default — it is used for ad-hoc testing only.

## Architecture

The library models a **physical and commercial gas chain** using Railway-Oriented Programming.

### Core abstraction

```fsharp
type Operation = State -> Result<Transition, DomainError>
```

Operations are composed via **Kleisli** (`Kleisli.fs`) to build a pipeline: Supply → Transport → Trade/Sell/Sleeve → Consume.

### Key types (`Tipos.fs`)

| Type | Purpose |
|------|---------|
| `State` | Current gas position (energy, owner, location, gasDay) |
| `Transition` | Result of one operation: new State + costs + notes |
| `Block` | Discriminated union of atomic operations (Supply, Transport, Trade, Sleeve, Sell, Consume) |
| `FlowDef` | `Linear` or `Join` (merge multiple paths) |
| `FlowStep` | One step within a flow path |
| `DomainError` | All domain-level failures |

### Units of measure

`MMBTU`, `GJ`, `USD`, `MXN` — always use typed `decimal<MMBTU>` / `decimal<USD/MMBTU>`.

### Module load order (fsproj)

Files must be listed in dependency order in `ModeloSaavi.fsproj`. Adding a new module: place it after all its dependencies.

```
Tipos → DbContext → Persistence → TiposDTO → Mapping → ResutlRows →
LoadFromSQL → DefinedOperations → Helpers → ProjectOperations →
Repository → Kleisli → FlowBuilderDB → Escenario → ErrorLog
```

## Key dependencies

| Package | Purpose |
|---------|---------|
| `FsToolkit.ErrorHandling` | `result {}` CE, `Result.map`, `Result.bind` |
| `SQLProvider` | Type-safe SQL access via `ctx` (DbContext) |
| `Microsoft.Data.SqlClient` | Raw ADO.NET transactions in `Repository.Tx` |
| `Serilog` | Structured logging; initialized with `ErrorLog.Logging.init "logs"` |

## Patterns to follow

- **Error handling**: always `Result<'T, DomainError>`, never exceptions from domain logic.
- **DB access**: use `ctx` (SQLProvider) for reads; use `Repository.Tx.withTransaction` for writes.
- **Flow execution**: `FlowBuilderDB` builds `FlowDef` from DB; `Escenario` / `ProjectOperations` executes it.
- **Persistence**: `Repository.DetailRepo` persists each operation result row after a successful run.
