using System;
using System.Collections.Generic;
using ModeloSaavi.Domain;

namespace ModeloSaavi.Operations;

public sealed record SupplyParams(string Seller, string Buyer, decimal PriceFix, string ContractRef);

public static class Supply
{
    public static Operation Create(SupplyParams p) => state =>
    {
        if (state.QtyMmbtu <= 0) return Result<OpResult>.Fail("Supply: qty <= 0");
        var newState = state with { Owner = p.Buyer, Contract = p.ContractRef };
        var amount = state.QtyMmbtu * p.PriceFix;
        var cost = new CostItem("GAS", state.QtyMmbtu, p.PriceFix, amount, new Dictionary<string, object?>
        {
            ["seller"] = p.Seller
        });
        var notes = new Dictionary<string, object?>
        {
            ["supply.seller"] = p.Seller,
            ["buyer"] = p.Buyer,
            ["contract"] = p.ContractRef,
            ["priceFix"] = p.PriceFix
        };
        return Result<OpResult>.Ok(new OpResult(newState, new List<CostItem>{ cost }, notes));
    };
}
