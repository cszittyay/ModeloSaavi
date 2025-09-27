module Storage

open Tipos
open Unidades


          // qty a inyectar (entrada del gasoducto al storage)

let inject (fx: Currency -> Currency -> decimal) (p: InjectParams) : Operation =
  fun stIn ->
    if stIn.loc <> p.storage.loc then
      Error (sprintf "Inject: estado en %s, se esperaba %s" stIn.loc p.storage.loc)
    elif p.qtyIn < 0.0<mmbtu> then
      Error "Inject: qtyIn < 0"
    elif p.qtyIn > p.storage.injMax then
      Error (sprintf "Inject: qtyIn excede injMax (%f)" (float p.storage.injMax))
    elif stIn.qty < p.qtyIn then
      Error "Inject: qty insuficiente en estado de entrada"
    else
      // Efecto de eficiencia: de qtyIn que entra, solo eff * qtyIn queda como inventario
      let effectiveIn = p.qtyIn * (p.storage.injEfficiency |> float |> LanguagePrimitives.FloatWithMeasure<mmbtu>)
      if p.storage.inv + effectiveIn > p.storage.invMax then
        Error "Inject: invMax excedido"
      else
        // Estado físico del gas: se "parkea" → qty reduce en pipeline
        let stOut = { stIn with qty = stIn.qty - p.qtyIn }
        // Costos: uso por inyección + cargo fijo (si corresponde)
        let cUso =
          match p.storage.usageRateInj with
          | None -> []
          | Some rateInj ->
              let amt = scaleMoney (decimal (float p.qtyIn)) rateInj |> round2
              [{ kind="STORAGE-INJ-USAGE"; qty=Some p.qtyIn; rate=Some rateInj; amount=amt; meta= Map.empty; currency = USD }]
        let cDem =
          p.storage.demandCharge
          |> Option.map (fun dem -> { kind="STORAGE-DEMAND"; qtyMMBtu=None; rate=Some dem; amount=round2 dem; meta=Map.empty; currency = 100.0<USD> })
          |> Option.toList
        let notes =
          [ "storage.invBefore", box (float p.storage.inv)
            "storage.effectiveIn", box (float effectiveIn)
            "storage.invAfter", box (float (p.storage.inv + effectiveIn)) ]
          |> Map.ofList
        Ok { state=stOut; costs=cUso @ cDem; notes=notes }





let withdraw (fx: Currency -> Currency -> decimal) (p: WithdrawParams) : Operation =
  fun stIn ->
    if stIn.location <> p.storage.loc then
      Error (sprintf "Withdraw: estado en %s, se esperaba %s" stIn.loc p.storage.loc)
    elif p.qtyOut < 0.0<mmbtu> then
      Error "Withdraw: qtyOut < 0"
    elif p.qtyOut > p.storage.wdrMax then
      Error (sprintf "Withdraw: qtyOut excede wdrMax (%f)" (float p.storage.wdrMax))
    else
      // Eficiencia de retiro: para entregar qtyOut al pipeline, necesitas qtyOut/eff de inventario
      let eff = p.storage.wdrEfficiency
      if eff <= 0.0 then Error "Withdraw: wdrEfficiency <= 0" else
      let invNeeded =
        (float p.qtyOut) / eff
        |> LanguagePrimitives.FloatWithMeasure<mmbtu>
      if invNeeded > p.storage.inv then
        Error "Withdraw: inventario insuficiente"
      else
        let stOut = { stIn with qty = stIn.qty + p.qtyOut }
        let cUso =
          match p.storage.usageRateWdr with
          | None -> []
          | Some rateW ->
              let amt = scaleMoney (decimal (float p.qtyOut)) rateW |> round2
              [{ kind="STORAGE-WDR-USAGE"; qtyMMBtu = p.qtyOut; rate=Some rateW; amount=amt; meta= Map.empty; currency = USD }]
        let notes =
          [ "storage.invBefore", box (float p.storage.inv)
            "storage.invNeeded", box (float invNeeded)
            "storage.invAfter",  box (float (p.storage.inv - invNeeded)) ]
          |> Map.ofList
        Ok { state=stOut; costs=cUso; notes=notes }




let carryCost (fx: Currency -> Currency -> decimal) (p: CarryParams) : Operation =
  fun stIn ->
    match p.storage.carryAPY with
    | None -> Ok { state=stIn; costs=[]; notes= Map.empty }
    | Some apy ->
        // monto = inventario * (apy anual) * (days/365) * (precio de valuación?)
        // Aquí (por simplicidad) consideramos un "costo financiero por unidad" fijo: apy se aplica sobre 1 USD/MMBtu
        // Si deseas valuación mark-to-market, introduce un precio referencial aquí.
        let qty = p.storage.inv
        let notionalPerMMBtu = money 1.0M USD
        let prorata = (decimal p.days) / 365.0M
        let unitCost = scaleMoney (apy * prorata) notionalPerMMBtu
        let amount = scaleMoney (decimal (float qty)) unitCost |> round2
        let ci =
          { kind="STORAGE-CARRY"
            qty=Some qty
            rate=Some unitCost
            amount=amount
            meta= [ "apy", box (float apy); "days", box p.days ] |> Map.ofList }
        Ok { state=stIn; costs=[ci]; notes= Map.empty }
