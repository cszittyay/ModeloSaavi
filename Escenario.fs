module Escenario

open System
open Unidades
open Tipos
open Transport
open Consume
open Supply
open Helpers
open Trade

   
let escenarioDosSuppliers () =
  // Datos comunes
  let gasDay     = DateOnly(2025,10,22)
  let deliveryPt = "Waha"
  let buyer      = "INDUSTRIA_X"

  // Supplier 1
  let tc1 : TransactionConfirmation =
    { tcId        = "TC-001"
      gasDay      = gasDay
      deliveryPt  = deliveryPt
      seller      = "SUPPLIER_A"
      buyer       = buyer
      qtyMMBtu    = 8_000.0m<MMBTU>
      price       = 3.10m<USD/MMBTU>
      contractRef = "C-A-2025"
      meta        = Map.empty }

  // Supplier 2
  let tc2 : TransactionConfirmation =
    { tcId        = "TC-002"
      gasDay      = gasDay
      deliveryPt  = deliveryPt
      seller      = "SUPPLIER_B"
      buyer       = buyer
      qtyMMBtu    = 5_000.0m<MMBTU>
      price       = 3.35m<USD/MMBTU>
      contractRef = "C-B-2025"
      meta        = Map.empty }

  let legs : SupplierLeg list = [ { tc = tc1 }; { tc = tc2 } ]

  // Estado inicial "vacío" (ajustá contract/meta si lo necesitás)
  let st0 : State =
    { qtyMMBtu = 0.0m<MMBTU>
      owner    = buyer
      contract = "INIT"
      location = deliveryPt
      gasDay   = gasDay
      meta     = Map.empty }

  // 1) Consolidar compra multi-supplier
  let rSupplyMany = (supplyMany legs) st0

  // 2) Transportar el total consolidado (un único transporte)
  let rAll =
    result {
      let! tSupply = rSupplyMany
      // rate de transporte de ejemplo: 0.08 $/MMBtu
      let! tTransp = transportOne 0.08m<USD/MMBTU> deliveryPt "CityGate_X" "SHIPPER_Y" tSupply.state
      return [ tSupply; tTransp ]
    }

  // 3) Balance diario + chequeo
  match rAll with
  | Error e ->
      printfn "ERROR escenario: %s" e
  | Ok transitions ->
      // Construir balances por (fecha, hub)
      let balances = fromTransitions transitions
      balances |> List.iter (fun b ->
        // tolerancia 0.001 MMBtu
        match check 0.001m<MMBTU> b with
        | Ok () ->
            printfn "OK balance %s/%s  buy=%M sell=%M inject=%M withdraw=%M consume=%M"
              (b.fecha.ToString "yyyy-MM-dd") b.hub (decimal b.buy) (decimal b.sell)
              (decimal b.inject) (decimal b.withdraw) (decimal b.consume)
        | Error msg ->
            printfn "DESBALANCE: %s" msg)

      // Mostrar totales y precio ponderado guardado en notes
      let tSupply = transitions.[0]
      let totalQty = tSupply.state.qtyMMBtu
      let wavg =
        match Meta.get<decimal> "wavgPrice" tSupply.notes with
        | Some w -> w
        | None   -> 0M
      printfn "Compra consolidada: qty=%M MMBtu; wavg=%M $/MMBtu" (decimal totalQty) wavg

// Ejecutar el escenario de prueba
escenarioDosSuppliers ()


    //match run opsSimpleTrade3 init with
    //| Error e -> printfn "❌ %s" e
    //| Ok r ->
    //    // printfn "✅ Estado final: qty=%f MMBTU owner=%s loc=%s" (decimal r.state.qtyMMBtu) r.state.owner r.state.location
    //    printfn "Costos:"
    //    r.costs |>  List.iter (fun c ->
    //        printfn " - %-22s qty=%A rate=%A amount=%A"  c.kind c.qtyMMBtu  c.rate  c.amount)

    //    printfn "\nCosto total: %.2fUSD"  (r.costs |> List.sumBy(fun c -> c.amount)  )

    //    printfn "Notas:" 
    //    r.notes |> Map.toList |> List.iter (fun (k,v) -> printfn " - %-22s %A" k v) 
        

