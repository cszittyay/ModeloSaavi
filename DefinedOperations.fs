module DefinedOperations
open System
open Tipos
open Unidades


module DomainError =
  let msg = function
    | QuantityNonPositive w -> $"Cantidad no positiva en {w}"
    | MissingContract id    -> $"Falta contrato {id}"
    | CapacityExceeded w    -> $"Capacidad excedida: {w}"
    | InvalidUnits d        -> $"Unidades inválidas: {d}"
    | Other s               -> s

module DomainErrorHelpers =
  open Tipos
  let describe (err: DomainError) : string =
    match err with
    | QuantityNonPositive where_ -> sprintf "[QuantityNonPositive] %s → cantidad <= 0" where_
    | MissingContract id -> sprintf "[MissingContract] Faltante: %s" id
    | CapacityExceeded what -> sprintf "[CapacityExceeded] Capacidad excedida en %s" what
    | InvalidUnits detail -> sprintf "[InvalidUnits] %s" detail
    | Other msg -> sprintf "[Other] %s" msg



module Domain =
  let inline amount (qty: Energy) (rate: EnergyPrice) : Money =   qty * rate
  let weightedAvgRate (sps: SupplyParams list)  : EnergyPrice =
    let qty = sps |> List.sumBy (fun sp -> sp.qEnergia)
    if qty = 0.0m<MMBTU> then 0.0m<USD/MMBTU>
    else
      let amt = sps |> List.sumBy (fun sp -> amount sp.qEnergia sp.price)
      (amt / qty)

module Validate =
  type Err =
    | EmptyLegs
    | BuyerMismatch of expected:Party * found:Party
    | GasDayMismatch
    | DeliveryPtMismatch
    | NonPositiveQty of Party
  
  let toString = function
    | EmptyLegs -> "No hay suppliers/legs para consolidar."
    | BuyerMismatch (e,f) -> $"Buyer inconsistente. Esperado={e} encontrado={f}"
    | GasDayMismatch -> "GasDay inconsistente entre legs."
    | DeliveryPtMismatch -> "DeliveryPoint inconsistente entre legs."
    | NonPositiveQty p -> $"Cantidad no positiva en leg del seller={p}"
  
  let legsConsolidados (sps: SupplyParams list) =
    match sps with
    | [] -> Error EmptyLegs
    | x::xs ->
      let b = x.buyer
      let d = x.gasDay
      let p = x.deliveryPt
      let okBuyer    = xs |> List.forall (fun sp -> sp.buyer     = b)
      let okGasDay   = xs |> List.forall (fun sp -> sp.gasDay    = d)
      let okDelivPt  = xs |> List.forall (fun sp -> sp.deliveryPt = p)
      if not okBuyer   then Error (BuyerMismatch (b, xs |> List.tryPick (fun sp -> Some sp.buyer) |> Option.defaultValue "<desconocido>"))
      elif not okGasDay then Error GasDayMismatch
      elif not okDelivPt then Error DeliveryPtMismatch
      else Ok (b,d,p)



module Consume =

    /// Es el consumo en el punto de medición final.
    // Toma el qty disponible en el State (outQ), lo compara contra lo medido (p.measured),
    // aplica tolerancia porcentual y, si corresponde, genera una línea de costo (Fee)
    // por el excedente de desbalance a la tasa p.penaltyRate.
    // El State sale con qty = 0 (lo consumido deja el sistema).
    let consume (p: ConsumeParams) : Operation =
      fun stIn ->
        // Validaciones de dominio
        if stIn.location <> p.meterLocation then
          Error (Other (sprintf "Consume: State@%s, esperado meterLocation=%s" stIn.location p.meterLocation))
        elif p.measured < 0.0m<MMBTU> then
          Error (InvalidUnits (sprintf "Consume.consume: measured=%M < 0" (decimal p.measured)))
        else
          // Cantidad "a la salida" del sistema (lo que se va a consumir)
            let outQ : Energy = stIn.energy
              // Desbalance respecto a lo medido: positivo => sobredespacho; negativo => subdespacho
            let dmb  : Energy = outQ - p.measured
              // Tolerancia absoluta (Energy) a partir del % sobre outQ
            
            let penalty : ItemCost  =
              // Penalidad solo si |dmb| > tol
                {
                kind     = CostKind.Nulo
                qEnergia = outQ
                rate = 0.0m<USD/MMBTU>
                amount = 0.0m<USD>
                provider = p.provider
                meta     =
                  [ "measured"   , box (round(decimal p.measured))
                    "outQ"       , box (round(decimal outQ))]
                  |> Map.ofList
              }
      

          // El consumo deja qty = 0 en el estado

          // Notas para trazabilidad
            let notes =
                [ "op"                 , box "consume"
                  "consumeParams"      , box p
                  "consume.measured"   , box (round(decimal p.measured))
                  "qtyConsume"        , box (round(decimal outQ))]
                 |> Map.ofList

            Ok { state = stIn
                 costs = penalty :: []
                 notes = notes }

module Supply =
    /// Compra simple (un solo supplier) a partir de una TransactionConfirmation
    let supply (sp: SupplyParams) : Operation =
      fun stIn ->
          // La compra aumenta posición del buyer; si tu State ya trae qty, podés sumar:
          let stOut =
            { stIn with
                owner    = sp.buyer
                contract = sp.contractRef
                energy   = sp.qEnergia
                location = sp.deliveryPt }
          // si hay un precio calculado por formula (price != 0) , se usa ese; si no, se usa index + adder
          let price = if sp.price = 0.0m<USD/MMBTU> then sp.adder + sp.index else sp.price
          let amt = Domain.amount sp.qEnergia price
          let cost =
            [{ kind     = CostKind.Gas
               qEnergia = sp.qEnergia
               rate     = price
               amount   = amt
               provider = sp.seller
               meta     = [ "seller", box sp.seller
                            "tcId"  , box sp.tcId
                            "index" , box (decimal sp.index)
                            "adder" , box (decimal sp.adder)
                            "price" , box (decimal price)
                            "gasDay", box sp.gasDay ] |> Map.ofList }]

          Ok { state = stOut
               costs = cost
               notes = [ 
                
                            
                         "op", box "supply"
                         "supplyParamsMany", box [sp]
                         "legsCount", box 1
                         "seller", box sp.seller
                         "buyer", box sp.buyer
                         "deliveryPt", box sp.deliveryPt ] |> Map.ofList }


    /// Compra multi-supplier, consolidando en una sola Operation
    let supplyMany (sps: SupplyParams list) : Operation =
      fun stIn ->
        match Validate.legsConsolidados sps with
        | Error e -> Error (Other (Validate.toString e))
        | Ok (buyer, gasDay, deliveryPt) ->
          let totalQty = sps |> List.sumBy (fun sp -> sp.qEnergia)
          if totalQty <= 0.0m<MMBTU> then Error (QuantityNonPositive "SupplyMany: qty total <= 0")
          else
            // nuevo estado consolidando la compra multi-supplier
            let stOut =
              { stIn with
                  owner    = buyer
                  contract = "MULTI"
                  energy   = totalQty
                  location = deliveryPt
                  gasDay   = gasDay }

            // costos por cada leg (mantiene trazabilidad por seller/contract/tcId)
            let costs =
              sps
              |> List.map (fun sp ->
                  let amt : Money = sp.qEnergia * (sp.price + sp.adder)
                  { provider = sp.seller
                    kind     = CostKind.Gas
                    qEnergia = sp.qEnergia
                    rate     = sp.price
                    amount   = amt
                    meta     = [ "cycle"     , box sp.temporalidad
                                 "tradingHub", box sp.tradingHub
                                 "adder"    , box sp.adder ] |> Map.ofList })

            // precio promedio ponderado (opcional en notes)
            let amtSum : Money = costs |> List.sumBy (fun c -> c.amount)
            let wavg = if totalQty > 0.0m<MMBTU> then amtSum / totalQty else 0.0m<USD/MMBTU>

            Ok { state = stOut
                 costs = costs
                 notes = [ "op"        , box "supplyMany"
                           "supplyParamsMany", box sps
                           "legsCount", box sps.Length
                           "buyer"     , box buyer
                           "gasDay"    , box gasDay
                           "deliveryPt", box deliveryPt
                           "wavgPrice:[USD/MMBTU]" , box (Math.Round(decimal wavg, 2))
                           "legsCount" , box sps.Length ] |> Map.ofList }


module Sleeve =

    let sleeve (p: SleeveParams) : Operation =
      fun stIn ->
        if stIn.energy <= 0.0m<MMBTU> then Error (Other "Trade: qEnergia <= 0")
        else
          let stOut = { stIn with owner = p.buyer; contract = p.contractRef }
          let amount = stIn.energy * p.adder
          let fee =
            { kind = CostKind.Sleeve
              provider = p.provider
              qEnergia = stIn.energy
              rate= p.adder
              // si es export, el costo es negativo
              amount = if p.sleeveSide = SleeveSide.Export then -amount else amount
              meta= [ "op"    , box "Sleeve"
                      "seller", box p.seller 
                      "adder", box (decimal p.adder)
                      "index" , box (decimal p.index)
                      "amount", box (decimal amount)
                      ] |> Map.ofList }
          Ok { state=stOut; costs=[fee]; notes= [ "op", box "Sleeve"
                                                  "sleeveParams", box p
                                                  "location", box p.location
                                                  "qty", box (decimal stIn.energy)
                                                  "provider", box p.provider        
                                                  "seller", box p.seller
                                                  "Seeve Side", box p.sleeveSide
                                                  "buyer", box p.buyer
                                                  "adder", box p.adder
                                                  "contractRef", box p.contractRef   ] |> Map.ofList }



module Sell =

  let sell (s: SellParams) : Operation =
      fun stIn ->
        if stIn.energy <= s.qty then Error (Other $"Sell: La cantidad a vender {s.qty} es mayor a la disponible: {stIn.energy}")
        else
          let stOut =
              { stIn with
                  energy   = stIn.energy - s.qty
                  owner    = s.buyer
                  contract = s.contractRef }
          
          
          let amount = s.qty * s.price
          let fee =
            { kind = CostKind.Sell
              provider = Party "S/D"
              qEnergia = s.qty
              rate= s.adder
              amount = amount
              meta= [ "seller", box s.seller
                      "buyer",  box s.buyer
                      "DiaGas", box s.gasDay
                      "QtyMMBTU", box (decimal s.qty)
                      "Amount", box (decimal amount)
                     ]              
                      |> Map.ofList }
          Ok { state=stOut; costs=[fee]; notes= [ "op", box "Sell"
                                                  "sellParams", box s
                                                  "location", box s.location        
                                                  "seller", box s.seller
                                                  "buyer", box s.buyer
                                                  "adder", box s.adder
                                                  "contractRef", box s.contractRef   ] |> Map.ofList }


// Operación de venta de gas a un supplier o cliente
module Trade =

  let trade (p: TradeParams) : Operation =
      fun stIn ->
        if stIn.energy <= 0.0m<MMBTU> then Error (Other "Trade: qEnergia <= 0")
        else
          let stOut = { stIn with owner = p.buyer; contract = p.contractRef }
          let amount = stIn.energy * p.adder
          let fee =
            { kind= Fee
              provider = Party "S/D"
              qEnergia = stIn.energy
              rate= p.adder
              amount = amount
              meta= [ "seller", box p.seller ] |> Map.ofList }
          Ok { state=stOut; costs=[fee]; notes= [ "op", box "Trade"
                                                  "tradeParams", box p
                                                  "location", box p.location        
                                                  "seller", box p.seller
                                                  "buyer", box p.buyer
                                                  "adder", box p.adder
                                                  "contractRef", box p.contractRef   ] |> Map.ofList }


module Transport = 
/// Operación de transporte de gas

    let transport (p: TransportParams) : Operation =
      fun stIn ->
        // Validaciones de dominio
        if String.IsNullOrWhiteSpace p.shipper then
          Error (MissingContract "TransportParams: shipper")

        elif stIn.location <> p.entry then
          Error (Other (sprintf "TransportParams: State@%s, esperado entry %s" stIn.location p.entry))

        elif stIn.energy <= 0.0m<MMBTU> then
          Error (QuantityNonPositive "TransportParams: transport.qEnergia")

        elif p.fuelPct < 0m || p.fuelPct >= 1m then
          Error (InvalidUnits "TransportParams: transport.fuelPct debe estar en [0,1)")
        else
          // Cálculos: shrink por fuel y costo de uso
          let qtyIn  : Energy = stIn.energy
          // La cantiad de Fuel expresada segun el qtyIn (recibido) y el fuelPct y segun el fuelMode
          let fuel   : Energy = if p.fuelMode = FuelMode.RxBase then qtyIn * p.fuelPct else qtyIn * p.fuelPct /(1.0m+p.fuelPct) 
          let qtyOut : Energy = qtyIn - fuel

          // Líneas de costo: USAGE y RESERVATION
          let usageAmount : Money = qtyOut * p.usageRate 

          // Reservation: registramos como “fijo” (basis=reservation_fixed).
          // Para materializar a USD respetando unidades, multiplicamos por 1<MMBTU>.
          let reservationAmount : Money = 1.0m<MMBTU> * p.reservation

          let costUsage : ItemCost =
            { kind     = CostKind.Transport
              provider = p.provider
              qEnergia = qtyOut
              rate     = p.usageRate + p.acaRate
              amount   = usageAmount
              meta     =
                [ "component",  box "usage"
                  "pipeline",   box p.pipeline
                  "shipper",    box p.shipper
                  "entry",      box p.entry
                  "exit",       box p.exit ]
                |> Map.ofList }

          let costReservation : ItemCost =
            { provider = p.provider
              kind     = CostKind.Transport
              qEnergia = 1.0m<MMBTU>            // basis sintético para obtener USD
              rate     = p.reservation
              amount   = reservationAmount
              meta     =
                [ "component",  box "reservation"
                  "basis",      box "reservation_fixed"
                  "shipper",    box p.shipper
                  "ACA",        box p.acaRate
                  "entry",      box p.entry
                  "exit",       box p.exit ]
                |> Map.ofList }

          let costs =
            if p.reservation > 0.0m<USD/MMBTU> then [ costUsage; costReservation ]
            else [ costUsage ]

          let notes =
            [ "op",          box "transport"
              "transportParams", box p
              "fuelPct",     box (Math.Round(decimal p.fuelPct * 100.0m, 3))
              "qtyIn",      box (Math.Round(decimal qtyIn, 2))
              "fuelQty",    box (Math.Round(decimal fuel, 2))
              "qtyOut",     box (Math.Round(decimal qtyOut,2))
              "usageRate",   box (Math.Round(decimal p.usageRate, 3))
              "reservation", box (decimal p.reservation)
              "shipper",     box p.shipper
              "entry",       box p.entry
              "exit",        box p.exit ]
            |> Map.ofList

          let stOut : State =
            { stIn with
                energy = qtyOut
                location = p.exit
                contract = if String.IsNullOrWhiteSpace stIn.contract then p.shipper else stIn.contract
                meta     =
                  stIn.meta
                  |> Map.add "transport.entry"   (box p.entry)
                  |> Map.add "transport.exit"    (box p.exit)
                  |> Map.add "transport.shipper" (box p.shipper)
                  |> Map.add "transport.fuelPct" (box p.fuelPct)
                  |> Map.add "transport.usage"   (box (decimal p.usageRate))
                  |> Map.add "transport.resv"    (box (decimal p.reservation)) }

          Ok { state = stOut; costs = costs; notes = notes }

        
