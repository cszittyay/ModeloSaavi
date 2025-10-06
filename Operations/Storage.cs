using System;
using System.Collections.Generic;
using ModeloSaavi.Domain;

namespace ModeloSaavi.Operations;

public sealed record StorageState(
    string Location,
    decimal Inventory,
    decimal InventoryMax,
    decimal InjMax,
    decimal WdrMax,
    decimal InjEfficiency,
    decimal WdrEfficiency,
    decimal UsageRateInj,
    decimal UsageRateWdr,
    decimal? DemandCharge,
    decimal CarryApy);

public sealed record InjectParams(StorageState Storage, decimal QtyIn);
public sealed record WithdrawParams(StorageState Storage, decimal QtyOut);
public sealed record CarryParams(StorageState Storage, int Days);

public static class Storage
{
    public static Operation Inject(InjectParams p) => state =>
    {
        if (!string.Equals(state.Location, p.Storage.Location, StringComparison.Ordinal))
            return Result<OpResult>.Fail($"Inject: estado en {state.Location}, se esperaba {p.Storage.Location}");
        if (p.QtyIn < 0) return Result<OpResult>.Fail("Inject: qtyIn < 0");
        if (p.QtyIn > p.Storage.InjMax) return Result<OpResult>.Fail($"Inject: qtyIn excede injMax ({p.Storage.InjMax})");
        if (state.QtyMmbtu < p.QtyIn) return Result<OpResult>.Fail("Inject: qty insuficiente en estado de entrada");

        var effectiveIn = p.QtyIn * p.Storage.InjEfficiency;
        if (p.Storage.Inventory + effectiveIn > p.Storage.InventoryMax)
            return Result<OpResult>.Fail("Inject: invMax excedido");

        var newState = state with { QtyMmbtu = state.QtyMmbtu - p.QtyIn };
        var cUsoAmt = p.QtyIn * p.Storage.UsageRateInj;
        var cUso = new CostItem("STORAGE-INJ-USAGE", p.QtyIn, p.Storage.UsageRateInj, cUsoAmt, new Dictionary<string, object?>());
        var costs = new List<CostItem> { cUso };
        if (p.Storage.DemandCharge is { } dem)
            costs.Add(new CostItem("STORAGE-DEMAND", 0, 0, dem, new Dictionary<string, object?>()));
        var notes = new Dictionary<string, object?>
        {
            ["storage.invBefore"] = p.Storage.Inventory,
            ["storage.effectiveIn"] = effectiveIn,
            ["storage.invAfter"] = p.Storage.Inventory + effectiveIn
        };
        return Result<OpResult>.Ok(new OpResult(newState, costs, notes));
    };

    public static Operation Withdraw(WithdrawParams p) => state =>
    {
        if (!string.Equals(state.Location, p.Storage.Location, StringComparison.Ordinal))
            return Result<OpResult>.Fail($"Withdraw: estado en {state.Location}, se esperaba {p.Storage.Location}");
        if (p.QtyOut < 0) return Result<OpResult>.Fail("Withdraw: qtyOut < 0");
        if (p.QtyOut > p.Storage.WdrMax) return Result<OpResult>.Fail($"Withdraw: qtyOut excede wdrMax ({p.Storage.WdrMax})");
        if (p.Storage.WdrEfficiency <= 0) return Result<OpResult>.Fail("Withdraw: wdrEfficiency <= 0");

        var invNeeded = p.QtyOut / p.Storage.WdrEfficiency;
        if (invNeeded > p.Storage.Inventory) return Result<OpResult>.Fail("Withdraw: inventario insuficiente");

        var newState = state with { QtyMmbtu = state.QtyMmbtu + p.QtyOut };
        var cUsoAmt = p.QtyOut * p.Storage.UsageRateWdr;
        var cUso = new CostItem("STORAGE-WDR-USAGE", p.QtyOut, p.Storage.UsageRateWdr, cUsoAmt, new Dictionary<string, object?>());
        var notes = new Dictionary<string, object?>
        {
            ["storage.invBefore"] = p.Storage.Inventory,
            ["storage.invNeeded"] = invNeeded,
            ["storage.invAfter"] = p.Storage.Inventory - invNeeded
        };
        return Result<OpResult>.Ok(new OpResult(newState, new List<CostItem>{ cUso }, notes));
    };

    public static Operation CarryCost(CarryParams p) => state =>
    {
        var qty = p.Storage.Inventory;
        const decimal notionalPerMmbtu = 1m; // 1 USD/MMBtu referencial
        var prorata = p.Days / 365m;
        var unitCost = prorata * p.Storage.CarryApy * notionalPerMmbtu;
        var amount = qty * unitCost;
        var ci = new CostItem("STORAGE-CARRY", qty, unitCost, amount, new Dictionary<string, object?>
        {
            ["apy"] = p.Storage.CarryApy,
            ["days"] = p.Days
        });
        return Result<OpResult>.Ok(new OpResult(state, new List<CostItem>{ ci }, new Dictionary<string, object?>()));
    };
}
