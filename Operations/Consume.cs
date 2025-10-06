using System;
using System.Collections.Generic;
using ModeloSaavi.Domain;

namespace ModeloSaavi.Operations;

public sealed record ConsumeParams(string MeterLocation, decimal Measured, decimal PenaltyRate, decimal TolerancePct);

public static class Consume
{
    public static Operation Create(ConsumeParams p) => state =>
    {
        if (!string.Equals(state.Location, p.MeterLocation, StringComparison.Ordinal))
            return Result<OpResult>.Fail($"Punto de Consumo: estado en {state.Location}, se esperaba {p.MeterLocation}");

        var outQ = state.QtyMmbtu;
        var dmb = outQ - p.Measured;
        var tol = Math.Abs(outQ) * (p.TolerancePct / 100m);

        CostItem? penalty = null;
        if (Math.Abs(dmb) > tol && p.PenaltyRate > 0)
        {
            var penalQty = Math.Abs(dmb) - tol;
            var amount = penalQty * p.PenaltyRate;
            penalty = new CostItem("PENALTY-IMBALANCE", penalQty, p.PenaltyRate, amount,
                new Dictionary<string, object?>
                {
                    ["desbalance"] = dmb,
                    ["tolerancia"] = tol
                });
        }

        var newState = state with { QtyMmbtu = 0 };
        var notes = new Dictionary<string, object?>
        {
            ["consume.measured"] = p.Measured,
            ["consume.out"] = outQ,
            ["consume.desbalance"] = dmb
        };
        var costs = new List<CostItem>();
        if (penalty is not null) costs.Add(penalty);
        return Result<OpResult>.Ok(new OpResult(newState, costs, notes));
    };
}
