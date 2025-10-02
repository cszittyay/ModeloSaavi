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
        owner = "JP Morgan"
        location   = "Ehrenberg"
        ts    = day
        contract  = "Contrato GAS- JP Morgan"}

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


    // Solo Transporte
    let opsSimple =
      [ supply { seller="JP Morgan"; buyer="SaaviMX"; priceFix= 2.85M<USD/MMBTU>; contractRef= "NAESB-A006F1" }
        transport { entry="Ehrenberg"; exit="AGUA-DULCE"; shipper="SaaviMX-Trans"; fuelPct=2.0m; usageRate=0.15M<USD/MMBTU>; reservation=0.05M<USD/MMBTU> }
        consume { meterLocation="AGUA-DULCE"; measured=9_600.0m<MMBTU>; penaltyRate=1.00M<USD/MMBTU>; tolerancePct=0.05m }
        ]


    let opsSimpleTrade =
      [ supply { seller="JP Morgan"; buyer="SaaviMX"; priceFix= 2.85M<USD/MMBTU>; contractRef= "NAESB-A006F1" }
        transport { entry="Ehrenberg"; exit="AGUA-DULCE"; shipper="SaaviMX-Trans"; fuelPct=2.0m; usageRate=0.15M<USD/MMBTU>; reservation=0.05M<USD/MMBTU> }
        trade { seller="SES"; buyer="SE"; adder=0.02M<USD/MMBTU>; contractRef= "SBF_044_17" }
        consume { meterLocation="AGUA-DULCE"; measured=9_600.0m<MMBTU>; penaltyRate=1.00M<USD/MMBTU>; tolerancePct=0.05m }
        ]

    
    // Trade antes de transporte
    let opsSimpleTrade3 =
       [supply { seller="JP Morgan"; buyer="SaaviMX"; priceFix= 2.85M<USD/MMBTU>; contractRef= "NAESB-A006F1" }
        trade { seller="SES"; buyer="SE"; adder=0.02M<USD/MMBTU>; contractRef= "SBF_044_17" }
        transport { entry="Ehrenberg"; exit="AGUA-DULCE"; shipper="SaaviMX-Trans"; fuelPct=2.0m; usageRate=0.15M<USD/MMBTU>; reservation=0.05M<USD/MMBTU> }
        consume { meterLocation="AGUA-DULCE"; measured=9_600.0m<MMBTU>; penaltyRate=1.00M<USD/MMBTU>; tolerancePct=0.05m }
        ]


    
 
    let ops01 =
      [ supply { seller="JP Morgan"; buyer="SaaviMX"; priceFix= 2.85M<USD/MMBTU>; contractRef= "NAESB-A006F1" }
        transport { entry="Ehrenberg"; exit="AGUA-DULCE"; shipper="SaaviMX-Trans"; fuelPct=0.1m; usageRate=0.15M<USD/MMBTU>; reservation=0.05M<USD/MMBTU> }
        // Inyección a storage (inyectamos 5,000 MMBTU)
        inject { storage=stConf; qtyIn=5_000.0m<MMBTU> }
        // Trade al cliente
        trade { seller="SES"; buyer="SE"; adder=0.02M<USD/MMBTU>; contractRef= "SBF_044_17" }
        // Retiro del storage para cubrir consumo (retiramos 1,000 MMBTU)
        withdraw { storage=stConf; qtyOut=1_000.0m<MMBTU> }
        consume { meterLocation="AGUA-DULCE"; measured=9_600.0m<MMBTU>; penaltyRate=1.00M<USD/MMBTU>; tolerancePct=0.05m }
        // Consumo medido
        // Cargo financiero por 30 días (si corresponde)
        carryCost { storage=stConf; days=30 } 
        ]


    // Dos transportes
    let opsSimpleTrade2Transportes =
      [ supply { seller="JP Morgan"; buyer="SaaviMX"; priceFix= 2.85M<USD/MMBTU>; contractRef= "NAESB-A006F1" }
        transport { entry="Ehrenberg"; exit="Ogilby"; shipper="EAX"; fuelPct=2.0m; usageRate=0.15M<USD/MMBTU>; reservation=0.05M<USD/MMBTU> }
        trade { seller="SES"; buyer="SE"; adder=0.02M<USD/MMBTU>; contractRef= "SBF_044_17" }
        transport { entry="Ogilby"; exit="AGUA-DULCE"; shipper="EAX"; fuelPct=1.0m; usageRate=0.15M<USD/MMBTU>; reservation=0.05M<USD/MMBTU> }
        consume { meterLocation="AGUA-SALADA"; measured=9_600.0m<MMBTU>; penaltyRate=1.00M<USD/MMBTU>; tolerancePct=0.05m }
        ]


    match run ops01 init with
    | Error e -> printfn "❌ %s" e
    | Ok r ->
        // printfn "✅ Estado final: qty=%f MMBTU owner=%s loc=%s" (decimal r.state.qtyMMBtu) r.state.owner r.state.location
        printfn "Costos:"
        r.costs |>  List.iter (fun c ->
            printfn " - %-22s qty=%A rate=%A amount=%A"  c.kind c.qtyMMBtu  c.rate  c.amount)

        printfn "\nCosto total: %.2fUSD"  (r.costs |> List.sumBy(fun c -> c.amount)  )

        printfn "Notas:" 
        r.notes |> Map.toList |> List.iter (fun (k,v) -> printfn " - %-22s %A" k v) 
        

