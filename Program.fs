open Escenario
open FlowBuilderExcel
open LegosOps





//escenario_Supply_Transport_Trade ()
// escenario_supply_Transport_Sleeve ()

// escenarioSupplyTradeTransporteConsumo()

let blocks = buildBlocksFromExcel(excelPath) "BAJIO.LT" "EAVIII"


let ops = compile blocks

run ops st0