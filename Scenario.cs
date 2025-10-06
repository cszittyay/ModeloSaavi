using System;
using System.Collections.Generic;
using ModeloSaavi.Domain;
using ModeloSaavi.Operations;
using static ModeloSaavi.Domain.Helpers;

namespace ModeloSaavi;

public static class Scenario
{
    public static void RunScenario()
    {
        var day = new DateTime(2025, 1, 15);
        var init = new GasState(
            QtyMmbtu: 10000m,
            Owner: "JP Morgan",
            Location: "Ehrenberg",
            Timestamp: day,
            Contract: "Contrato GAS- JP Morgan");

        var stConf = new StorageState(
            Location: "AGUA-DULCE",
            Inventory: 50000m,
            InventoryMax: 200000m,
            InjMax: 20000m,
            WdrMax: 25000m,
            InjEfficiency: 0.98m,
            WdrEfficiency: 0.97m,
            UsageRateInj: 0.03m,
            UsageRateWdr: 0.04m,
            DemandCharge: 500m,
            CarryApy: 0.10m);

        var ops = new List<Operation>
        {
            Supply.Create(new("JP Morgan","SaaviMX",2.85m,"NAESB-A006F1")),
            Transport.Create(new("Ehrenberg","AGUA-DULCE","SaaviMX-Trans",0.1m,0.15m,0.05m)),
            Storage.Inject(new(stConf, 5000m)),
            Trade.Create(new("SES","SE",0.02m,"SBF_044_17")),
            Storage.Withdraw(new(stConf, 1000m)),
            Consume.Create(new("AGUA-DULCE",9600m,1.00m,0.05m)),
            Storage.CarryCost(new(stConf, 30))
        };

        var result = Run(ops, init);
        if (!result.IsOk)
        {
            Console.WriteLine($"? {result.Error}");
            return;
        }

        Console.WriteLine("Costos:");
        decimal total = 0;
        foreach (var c in result.Value!.Costs)
        {
            Console.WriteLine($" - {c.Kind,-22} qty={c.QtyMmbtu} rate={c.RatePerMmbtu} amount={c.Amount}");
            total += c.Amount;
        }
        Console.WriteLine($"\nCosto total: {total:F2} USD");

        Console.WriteLine("Notas:");
        foreach (var kv in result.Value.Notes)
            Console.WriteLine($" - {kv.Key,-22} {kv.Value}");
    }
}
