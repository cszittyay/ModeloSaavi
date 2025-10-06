using System;
using System.Collections.Generic;

namespace ModeloSaavi.Domain;

public record GasState(
    decimal QtyMmbtu,
    string Owner,
    string Location,
    DateTime Timestamp,
    string Contract);

public record CostItem(
    string Kind,
    decimal QtyMmbtu,
    decimal RatePerMmbtu,
    decimal Amount,
    IReadOnlyDictionary<string, object?> Meta);

public record OpResult(
    GasState State,
    IReadOnlyList<CostItem> Costs,
    IReadOnlyDictionary<string, object?> Notes);

public delegate Result<OpResult> Operation(GasState state);

public readonly struct Result<T>
{
    public bool IsOk { get; }
    public T? Value { get; }
    public string? Error { get; }
    private Result(T value){ IsOk = true; Value = value; Error = null; }
    private Result(string error){ IsOk = false; Value = default; Error = error; }
    public static Result<T> Ok(T v) => new(v);
    public static Result<T> Fail(string e) => new(e);
}
