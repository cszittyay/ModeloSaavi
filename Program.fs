open Escenario
open FlowBuilderExcel
open LegosOps





//escenario_Supply_Transport_Trade ()
printfn "\n\n======================================================\n\n"
// escenario_supply_Transport_Sleeve ()

// escenarioSupplyTradeTransporteConsumo()


let blocks = buildBlocksFromExcel(@"EscenarioSample.xlsx") "LR" "EAX"


let ops = compile blocks

run ops st0