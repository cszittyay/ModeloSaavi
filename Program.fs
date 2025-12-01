open Escenario
open FlowBuilderExcel
open LegosOps





//escenario_Supply_Transport_Trade ()
// escenario_supply_Transport_Sleeve ()

// escenarioSupplyTradeTransporteConsumo()

//let planta = "LR"
//let central = "EAX"

//let planta = "BAJIO"
//let central = "EAVIII"

let planta = "BAJIO.LT"
let central = "EAVIII"


printfn "Planta: %s Central-> %s" planta central


let blocks = buildBlocksFromExcel(excelPath) planta central


let ops = compile blocks

run ops st0