using System;
using System.Collections.Generic;
using ModeloSaavi.Domain;

namespace ModeloSaavi.Operations;

public sealed record TransportParams(string Entry, string Exit, string Shipper, decimal FuelPct, decimal UsageRate, decimal Reservation);

public static class Transport
{
    public static Operation Create(TransportParams p) => state =>
    {
        if (!string.Equals(state.Location, p.Entry, StringComparison.Ordinal))
            return Result<OpResult>.Fail($"Transport: estado en {state.Location}, se esperaba {p.Entry}");
        if (state.QtyMmbtu < 0)
            return Result<OpResult>.Fail("Transport: qty negativa");

        var fuel = state.QtyMmbtu * p.FuelPct / 100m;
        var qtyOut = Math.Max(0, state.QtyMmbtu - fuel);
        var newState = state with { QtyMmbtu = qtyOut, Location = p.Exit };
        var usoAmount = qtyOut * p.UsageRate;
        var cUso = new CostItem("TRANSPORT-USAGE", qtyOut, p.UsageRate, usoAmount, new Dictionary<string, object?>
        {
            ["shipper"] = p.Shipper,
            ["fuelMMBtu"] = fuel
        });
        var cRes = new CostItem("TRANSPORT-RESERVATION", 0, p.Reservation, p.Reservation * qtyOut, new Dictionary<string, object?>
        {
            ["shipper"] = p.Shipper
        });
        var notes = new Dictionary<string, object?>
        {
            ["transport.fuelPct"] = p.FuelPct,
            ["transport.fuelMMBtu"] = fuel,
            ["transport.entry"] = p.Entry,
            ["transport.exit"] = p.Exit
        };
        return Result<OpResult>.Ok(new OpResult(newState, new List<CostItem>{ cUso, cRes }, notes));
    };
}
