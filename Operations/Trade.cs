using System;
using System.Collections.Generic;
using ModeloSaavi.Domain;

namespace ModeloSaavi.Operations;

public sealed record TradeParams(string Seller, string Buyer, decimal Adder, string ContractRef);

public static class Trade
{
    public static Operation Create(TradeParams p) => state =>
    {
        if (state.QtyMmbtu <= 0) return Result<OpResult>.Fail("Trade: qty <= 0");
        var newState = state with { Owner = p.Buyer, Contract = p.ContractRef };
        var amount = state.QtyMmbtu * p.Adder;
        var fee = new CostItem("FEE-TRADE", state.QtyMmbtu, p.Adder, amount, new Dictionary<string, object?>{ ["seller"] = p.Seller });
        return Result<OpResult>.Ok(new OpResult(newState, new List<CostItem>{ fee }, new Dictionary<string, object?>()));
    };
}
