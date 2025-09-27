module Escenario

open System
open Unidades
open Tipos


let testEscenario () =
    // FX simple (identidad): no convertimos entre monedas en este ejemplo
    let fx (fromC:Currency) (toC:Currency) =
        if fromC = toC then 1.0M else
        // aquí podrías poner tasas, p. ej. USD->MXN
        if fromC = USD && toC = MXN then 18.0M
        elif fromC = MXN && toC = USD then 1.0M/18.0M
        else 1.0M

    let day = DateTime(2025,1,15)
    let init : GasState =
      { qty = 10_000.0<mmbtu>
        owner = "Proveedor-USA"
        loc   = "WAHA-ENTRY"
        ts    = Some day
        cntr  = None }

    // storage config (ejemplo)
    let stConf =
      { loc            = "AGUA-DULCE"
        inv            = 50_000.0<mmbtu>
        invMax         = 200_000.0<mmbtu>
        injMax         = 20_000.0<mmbtu>
        wdrMax         = 25_000.0<mmbtu>
        injEfficiency  = 0.98
        wdrEfficiency  = 0.97
        usageRateInj   = Some (money 0.03M USD)
        usageRateWdr   = Some (money 0.04M USD)
        demandCharge   = Some (money 500.00M USD)
        carryAPY       = Some 0.10M } // 10% anual

    let ops =
      [ supply { seller="Proveedor-USA"; buyer="SaaviMX"; priceFix=Some (money 2.85M USD); contractRef=Some "NAESB-A006F1" }
        transport { entry="WAHA-ENTRY"; exit="AGUA-DULCE"; shipper="SaaviMX-Trans"; fuelPct=0.02; usageRate=(money 0.15M USD); reservation=Some (money 0.05M USD) }
        // Inyección a storage (inyectamos 5,000 MMBtu)
        inject fx { storage=stConf; qtyIn=5_000.0<mmbtu> }
        // Trade al cliente
        trade { seller="SaaviMX"; buyer="Planta-MER"; adder=(money 0.02M USD); contractRef=Some "SBF_044_17" }
        // Retiro del storage para cubrir consumo (retiramos 1,000 MMBtu)
        withdraw fx { storage=stConf; qtyOut=1_000.0<mmbtu> }
        // Consumo medido
        consume { meterLocation="AGUA-DULCE"; measured=9_600.0<mmbtu>; penaltyRate=Some (money 1.00M USD); tolerancePct=0.05 }
        // Cargo financiero por 30 días (si corresponde)
        carryCost fx { storage=stConf; days=30 } ]

    match run ops init with
    | Error e -> printfn "❌ %s" e
    | Ok r ->
        printfn "✅ Estado final: qty=%f MMBtu owner=%s loc=%s" (float r.state.qty) r.state.owner r.state.loc
        printfn "Costos:"
        r.costs |> List.iter (fun c ->
            printfn " - %-22s qty=%A rate=%A amount=%s"
                c.kind (c.qty |> Option.map float) (c.rate |> Option.map toStringMoney) (toStringMoney c.amount))
        printfn "Notas: %A" r.notes
