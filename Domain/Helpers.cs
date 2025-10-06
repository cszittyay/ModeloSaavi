using System;
using System.Collections.Generic;
using ModeloSaavi.Domain;

namespace ModeloSaavi.Domain;

public static class Helpers
{
    public static OpResult Merge(OpResult a, OpResult b) => new(
        b.State,
        new List<CostItem>(a.Costs).Concat(b.Costs).ToList(),
        MergeMaps(a.Notes, b.Notes)
    );

    private static IReadOnlyDictionary<string, object?> MergeMaps(IReadOnlyDictionary<string, object?> m1, IReadOnlyDictionary<string, object?> m2)
    {
        var d = new Dictionary<string, object?>(m1);
        foreach (var kv in m2) d[kv.Key] = kv.Value;
        return d;
    }

    public static Result<OpResult> Run(IEnumerable<Operation> ops, GasState init)
    {
        var acc = Result<OpResult>.Ok(new OpResult(init, new List<CostItem>(), new Dictionary<string, object?>()));
        foreach (var op in ops)
        {
            if (!acc.IsOk) return acc;
            var next = op(acc.Value!.State);
            if (!next.IsOk) return Result<OpResult>.Fail(next.Error!);
            acc = Result<OpResult>.Ok(Merge(acc.Value!, next.Value!));
        }
        return acc;
    }
}
