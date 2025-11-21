module SupplyTrade

open System
open Unidades
open Tipos
open Supply
open Trade
open Helpers



let private runSupplyTradeLeg (p: SupplyTradeLegParams) : Operation =
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
          energy   = 0.0m<MMBTU>
          location = p.entryPoint
          gasDay   = p.gasDay }

    // fold sobre las patas Supply+Trade
    let folder
        (acc: Result<State * ItemCost list * Map<string,obj>, DomainError>)
        (legParams: SupplyTradeLegParams) =

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
