module Storage

open Tipos
open Unidades


// qtyMMBtu a inyectar (entrada del gasoducto al storage)

let inject  (p: InjectParams) : Operation =
  fun stIn ->
    if stIn.location <> p.storage.location then
      Error (sprintf "Inject: estado en %s, se esperaba %s" stIn.location p.storage.location)
    elif p.qtyIn < 0.0m<MMBTU> then
      Error "Inject: qtyIn < 0"
    elif p.qtyIn > p.storage.injMax then
      Error (sprintf "Inject: qtyIn excede injMax (%f)" ( p.storage.injMax))
    elif stIn.qtyMMBtu < p.qtyIn then
      Error "Inject: qtyMMBtu insuficiente en estado de entrada"
    else
      // Efecto de eficiencia: de qtyIn que entra, solo eff * qtyIn queda como inventario
      let effectiveIn = p.qtyIn * p.storage.injEfficiency
      if p.storage.inv + effectiveIn > p.storage.invMax then
        Error "Inject: invMax excedido"
      else
        // Estado físico del gas: se "parkea" → qtyMMBtu reduce en pipeline
        let stOut = { stIn with qtyMMBtu = stIn.qtyMMBtu - p.qtyIn }
        // Costos: uso por inyección + cargo fijo (si corresponde)
        let cUso =
            let amt = p.qtyIn * p.storage.usageRateInj 
            [{ kind="STORAGE-INJ-USAGE"; qtyMMBtu= p.qtyIn; rate= p.storage.usageRateInj; amount=amt; meta= Map.empty; }]
        let cDem =
          p.storage.demandCharge
          |> Option.map (fun dem -> { kind="STORAGE-DEMAND"; qtyMMBtu=0.0m<MMBTU>; rate= 0m<USD/MMBTU>; amount= dem; meta=Map.empty; })
          |> Option.toList
        let notes =
          [ "storage.invBefore", box (decimal p.storage.inv)
            "storage.effectiveIn", box (decimal effectiveIn)
            "storage.invAfter", box (decimal (p.storage.inv + effectiveIn)) ]
          |> Map.ofList
        Ok { state=stOut; costs=cUso @ cDem; notes=notes }





let withdraw (p: WithdrawParams) : Operation =
  fun stIn ->
    if stIn.location <> p.storage.location then
      Error (sprintf "Withdraw: estado en %s, se esperaba %s" stIn.location p.storage.location)
    elif p.qtyOut < 0.0m<MMBTU> then
      Error "Withdraw: qtyOut < 0"
    elif p.qtyOut > p.storage.wdrMax then
      Error (sprintf "Withdraw: qtyOut excede wdrMax (%f)" p.storage.wdrMax)
    else
      // Eficiencia de retiro: para entregar qtyOut al pipeline, necesitas qtyOut/eff de inventario
      let eff = p.storage.wdrEfficiency
      if eff <= 0.0m then Error "Withdraw: wdrEfficiency <= 0" else
      let invNeeded = p.qtyOut / eff

        
      if invNeeded > p.storage.inv then
        Error "Withdraw: inventario insuficiente"
      else
        let stOut = { stIn with qtyMMBtu = stIn.qtyMMBtu + p.qtyOut }
        let cUso =
            let amt = p.qtyOut * p.storage.usageRateWdr 
            [{ kind="STORAGE-WDR-USAGE"; qtyMMBtu = p.qtyOut; rate = p.storage.usageRateWdr; amount=amt; meta= Map.empty;}]
        let notes =
            [ "storage.invBefore", box (decimal p.storage.inv)
              "storage.invNeeded", box (decimal invNeeded)
              "storage.invAfter",  box (decimal (p.storage.inv - invNeeded)) ]
            |> Map.ofList
              
        Ok { state=stOut; costs=cUso; notes=notes }




let carryCost (p: CarryParams) : Operation =
  fun stIn ->
        // monto = inventario * (apy anual) * (days/365) * (precio de valuación?)
        // Aquí (por simplicidad) consideramos un "costo financiero por unidad" fijo: apy se aplica sobre 1 USD/MMBtu
        // Si deseas valuación mark-to-market, introduce un precio referencial aquí.
        
        let qtyMMBtu = p.storage.inv
        let notionalPerMMBtu = 1m<USD/MMBTU>
        let prorata = (decimal p.days) / 365.0M
        let unitCost = prorata * p.storage.carryAPY * notionalPerMMBtu
        let amount = qtyMMBtu * unitCost 
        let ci =
          { kind="STORAGE-CARRY"
            qtyMMBtu = qtyMMBtu
            rate = unitCost
            amount = amount
            meta= [ "apy", box (float p.storage.carryAPY); "days", box p.days ] |> Map.ofList }
        Ok { state=stIn; costs=[ci]; notes= Map.empty }
