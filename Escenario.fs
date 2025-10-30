module Escenario

open System
open Unidades           // Energy, RateGas, Money (decimal<...>)
open Tipos              // State, Transition, DomainError, SupplierLeg, TransactionConfirmation, CostKind, etc.
open Supply             // supplyMany : SupplierLeg list -> Operation
open Transport          // transport  : RateGas -> string -> string -> string -> Operation
open Kleisli            // runAll     : Operation list -> Plan (State -> Result<Transition list, _>)

/// Helpers de impresión (opcionales)
let private printMoney (m: Money) = (decimal m).ToString("0.#####")
let private printRate (r: RateGas) = (decimal r).ToString("0.#####")
let private printQty  (q: Energy) = (decimal q).ToString("0.#####")

/// Construye un escenario: compra consolidada (2 suppliers) + transporte único
let escenarioSupplyManyMasTransport () =
  // Datos comunes del día y hub
  let gasDay     = DateOnly(2025, 10, 22)
  let deliveryPt = "Waha"
  let buyer      = "INDUSTRIA_X"

  // TC 1
  let tc1 : TransactionConfirmation =
    { tcId        = "TC-001"
      gasDay      = gasDay
      deliveryPt  = deliveryPt
      seller      = "SUPPLIER_A"
      buyer       = buyer
      qtyMMBtu    = 8000.0m<MMBTU>
      price       = 3.10m<USD/MMBTU>
      contractRef = "C-A-2025"
      meta        = Map.empty }

  // TC 2
  let tc2 : TransactionConfirmation =
    { tcId        = "TC-002"
      gasDay      = gasDay
      deliveryPt  = deliveryPt
      seller      = "SUPPLIER_B"
      buyer       = buyer
      qtyMMBtu    = 5000.0m<MMBTU>
      price       = 3.35m<USD/MMBTU>
      contractRef = "C-B-2025"
      meta        = Map.empty }

  let legs : SupplierLeg list = [ { tc = tc1 }; { tc = tc2 } ]

  // Estado inicial
  let st0 : State =
    { qtyMMBtu = 0.0m<MMBTU>
      owner    = buyer
      contract = "INIT"
      location = deliveryPt
      gasDay   = gasDay
      meta     = Map.empty }

  // Pipeline: supplyMany -> transport
  let rate : RateGas = 0.08m<USD/MMBTU>
  let ops : Operation list =
    [ supplyMany legs
      transport rate deliveryPt "CityGate_X" "SHIPPER_Y" ]

  // Ejecutar y acumular transiciones
  match runAll ops st0 with
  | Error e ->
      printfn "ERROR en escenario: %A" e

  | Ok transitions ->
      printfn "Transiciones ejecutadas: %d" (List.length transitions)
      transitions
      |> List.iteri (fun i t ->
          let s = t.state
          printfn "#%d op=%A qty=%s MMBtu owner=%s loc=%s contract=%s"
            (i+1)
            (t.notes |> Map.tryFind "op" |> Option.defaultValue (box ""))
            (printQty s.qtyMMBtu) s.owner s.location s.contract

          // listar costos de la transición
          if not (List.isEmpty t.costs) then
            t.costs
            |> List.iter (fun c ->
                printfn "   cost kind=%A qty=%s rate=%s USD/MMBTU amount=%s USD"
                  c.kind (printQty c.qtyMMBtu) (printRate c.rate) (printMoney c.amount))
          else
            printfn "   cost: (sin costos)")

      // Agregados útiles (ejemplo: costo total de transporte)
      let totalTransportUSD : Money =
        transitions
        |> List.collect (fun t -> t.costs)
        |> List.filter   (fun c -> c.kind = CostKind.Transport)
        |> List.sumBy    (fun c -> c.amount)

      let finalState = (List.last transitions).state
      printfn "TOTAL transporte = %s USD" (printMoney totalTransportUSD)
      printfn "Estado final: qty=%s MMBtu loc=%s contract=%s"
        (printQty finalState.qtyMMBtu) finalState.location finalState.contract

// Ejecutar el escenario
escenarioSupplyManyMasTransport ()
