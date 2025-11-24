module DefinedOperations
open System
open Tipos
open Helpers
open Unidades


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
        elif p.tolerancePct < 0.0m then
          Error (InvalidUnits (sprintf "Consume.consume: tolerancePct=%M < 0" p.tolerancePct))
        else
          // Cantidad "a la salida" del sistema (lo que se va a consumir)
          let outQ : Energy = stIn.energy
          // Desbalance respecto a lo medido: positivo => sobredespacho; negativo => subdespacho
          let dmb  : Energy = outQ - p.measured
          // Tolerancia absoluta (Energy) a partir del % sobre outQ
          let tol  : Energy = abs outQ * (p.tolerancePct / 100.0m)

          // Penalidad solo si |dmb| > tol
          let penalty : ItemCost  =
              let excedente : Energy = abs dmb - tol
              let amount : Money = excedente * p.penaltyRate   // (MMBTU * USD/MMBTU) = USD
              {
                kind     = CostKind.Nulo
                qEnergia = excedente
                provider = p.provider
                rate     = p.penaltyRate
                amount   = amount
                meta     =
                  [ "component"  , box "imbalance_penalty"
                    "desbalance" , box (decimal dmb)
                    "tolerancia" , box (decimal tol)
                    "measured"   , box (decimal p.measured)
                    "outQ"       , box (decimal outQ) ]
                  |> Map.ofList
              }
      

          // El consumo deja qty = 0 en el estado
          let stOut : State = { stIn with energy = p.measured }

          // Notas para trazabilidad
          let notes =
            [ "op"                 , box "consume"
              "consume.measured"   , box (round(decimal p.measured))
              "consume.out"        , box (round(decimal outQ))
              "consume.desbalance" , box (round (decimal dmb))
              "consume.tolerance[]"  , box (round  (decimal tol)) ]
            |> Map.ofList

          Ok { state = stOut
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

          let amt = Domain.amount sp.qEnergia sp.price
          let cost =
            [{ kind     = CostKind.Gas
               qEnergia = sp.qEnergia
               rate     = sp.price
               amount   = amt
               provider = sp.seller
               meta     = [ "seller", box sp.seller
                            "tcId"  , box sp.tcId
                            "gasDay", box sp.gasDay ] |> Map.ofList }]

          Ok { state = stOut
               costs = cost
               notes = [ "op", box "supply"
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
            { kind= Sleeve
              provider = p.provider
              qEnergia = stIn.energy
              rate= p.adder
              amount = amount
              meta= [ "seller", box p.seller 
                      "adder", box p.adder
                      "amount", box (decimal amount)
                      ] |> Map.ofList }
          Ok { state=stOut; costs=[fee]; notes= [ "op", box "Sleeve"
                                                  "provider", box p.provider        
                                                  "seller", box p.seller
                                                  "Seeve Side", box p.sleeveSide
                                                  "buyer", box p.buyer
                                                  "adder", box p.adder
                                                  "contractRef", box p.contractRef   ] |> Map.ofList }


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
                                                  "seller", box p.seller
                                                  "buyer", box p.buyer
                                                  "adder", box p.adder
                                                  "contractRef", box p.contractRef   ] |> Map.ofList }


module SupplyTrade =
    open Supply
    open Trade
    // Constructor de SupplyTradeLegParams
    // toma los parámetros de Supply y Trade y ejecuta los dos en orden
    let private runSupplyTradeLeg (p: SupplyTradeParams) : Operation =
      let supplyOp = supply p.supply
      let tradeOp  = trade  p.trade

      fun st0 ->
        match supplyOp st0 with
        | Error e -> Error e
        | Ok tr1 ->
          match tradeOp tr1.state with
          | Error e -> Error e
          | Ok tr2 ->
            // merge de costos y notas (ajustá el merge a tus helpers reales)
            let costs = tr1.costs @ tr2.costs
            let notes =
              Map.fold (fun acc k v -> Map.add k v acc) tr1.notes tr2.notes

            Ok { tr2 with costs = costs; notes = notes }



    let multiSupplyTrade (p: MultiSupplyTradeParams) : Operation =
      fun stIn ->

        // Estado base común a todos los legs en el punto de entrada
        let baseState =
          { stIn with
              energy   = 0.0m<MMBTU>}

        // fold sobre las patas Supply+Trade
        let folder
            (acc: Result<State * ItemCost list * Map<string,obj>, DomainError>)
            (legParams: SupplyTradeParams) =

          acc
          |> Result.bind (fun (aggState, aggCosts, aggNotes) ->
            let opLeg = runSupplyTradeLeg legParams
            match opLeg baseState with
            | Error e -> Error e
            | Ok trLeg ->
              // sumamos energía de esta pata
              let energy' = aggState.energy + trLeg.state.energy

              // estado agregado (misma location/gasDay, energía sumada)
              let state' = { aggState with energy = energy' }

              // acumulamos costos
              let costs' = aggCosts @ trLeg.costs

              // merge de notas (último gana, ajustar si querés otro criterio)
              let notes' =
                Map.fold (fun acc k v -> Map.add k v acc) aggNotes trLeg.notes

              Ok (state', costs', notes')
          )

        let zero : Result<State * ItemCost list * Map<string,obj>, DomainError> =
          Ok (baseState, [], Map.empty)

        match List.fold folder zero p.legs with
        | Error e -> Error e
        | Ok (stFinal, costs, notes) ->
          let notes' =
            notes
            |> Map.add "op" (box "MultiSupplyTrade")
            |> Map.add "legsCount" (box p.legs.Length)

          Ok { state = stFinal
               costs = costs
               notes = notes' }


module Transport = 
/// Operación de transporte de gas

    let transport (p: TransportParams) : Operation =
      fun stIn ->
        // Validaciones de dominio
        if String.IsNullOrWhiteSpace p.shipper then
          Error (MissingContract "shipper")

        elif stIn.location <> p.entry then
          Error (Other (sprintf "State@%s, esperado entry %s" stIn.location p.entry))

        elif stIn.energy <= 0.0m<MMBTU> then
          Error (QuantityNonPositive "transport.qEnergia")

        elif p.fuelPct < 0m || p.fuelPct >= 1m then
          Error (InvalidUnits "transport.fuelPct debe estar en [0,1)")
        else
          // Cálculos: shrink por fuel y costo de uso
          let qtyIn  : Energy = stIn.energy
          let fuel   : Energy = qtyIn * p.fuelPct
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
              rate     = p.usageRate
              amount   = usageAmount
              meta     =
                [ "component",  box "usage"
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
                  "entry",      box p.entry
                  "exit",       box p.exit ]
                |> Map.ofList }

          let costs =
            if p.reservation > 0.0m<USD/MMBTU> then [ costUsage; costReservation ]
            else [ costUsage ]

          let notes =
            [ "op",          box "transport"
              "fuelPct",     box (Math.Round(decimal p.fuelPct * 100.0m, 3))
              "qty.in",      box (Math.Round(decimal qtyIn, 2))
              "qty.fuel",    box (Math.Round(decimal fuel, 2))
              "qty.out",     box (Math.Round(decimal qtyOut,2))
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