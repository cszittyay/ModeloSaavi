module Escenario

open System
open Unidades
open Tipos
open Transport
open Consume
open Supply
open Helpers
open Storage
open Trade

let testEscenario () =
   
    let day = DateTime(2025,1,15)
    let init : GasState =
      { qtyMMBtu = 10_000.0m<MMBTU>
        owner = "Proveedor-USA"
        location   = "WAHA-ENTRY"
        ts    = day
        contract  = "Contrato GAS"}

    // storage config (ejemplo)
    let stConf =
      { location      = "AGUA-DULCE"
        inv            = 50_000.0m<MMBTU>
        invMax         = 200_000.0m<MMBTU>
        injMax         = 20_000.0m<MMBTU>
        wdrMax         = 25_000.0m<MMBTU>
        injEfficiency  = 0.98m
        wdrEfficiency  = 0.97m
        usageRateInj   = 0.03M<USD/MMBTU>        // $/MMBtu inyectado efectivo (por USD)
        usageRateWdr   = 0.04m<USD/MMBTU>        
        demandCharge   = Some 500.00M<USD>            // fijo por periodo (si aplica) (por USD)
        carryAPY       = 0.10M } // 10% anual

    let ops =
      [ supply { seller="Proveedor-USA"; buyer="SaaviMX"; priceFix= 2.85M<USD/MMBTU>; contractRef= "NAESB-A006F1" }
        transport { entry="WAHA-ENTRY"; exit="AGUA-DULCE"; shipper="SaaviMX-Trans"; fuelPct=0.02m; usageRate=0.15M<USD/MMBTU>; reservation=0.05M<USD/MMBTU> }
        // Inyección a storage (inyectamos 5,000 MMBTU)
        inject { storage=stConf; qtyIn=5_000.0m<MMBTU> }
        // Trade al cliente
        trade { seller="SaaviMX"; buyer="Planta-MER"; adder=0.02M<USD/MMBTU>; contractRef= "SBF_044_17" }
        // Retiro del storage para cubrir consumo (retiramos 1,000 MMBTU)
        withdraw { storage=stConf; qtyOut=1_000.0m<MMBTU> }
        // Consumo medido
        consume { meterLocation="AGUA-DULCE"; measured=9_600.0m<MMBTU>; penaltyRate=1.00M<USD/MMBTU>; tolerancePct=0.05m }
        // Cargo financiero por 30 días (si corresponde)
        carryCost { storage=stConf; days=30 } ]

    match run ops init with
    | Error e -> printfn "❌ %s" e
    | Ok r ->
        printfn "✅ Estado final: qty=%f MMBTU owner=%s loc=%s" (decimal r.state.qtyMMBtu) r.state.owner r.state.location
        printfn "Costos:"
        r.costs |> List.iter (fun c ->
            printfn " - %-22s qty=%A rate=%A amount=%A"  c.kind c.qtyMMBtu  c.rate  c.amount)
        printfn "Notas: %A" r.notes
