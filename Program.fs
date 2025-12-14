open System
open Escenario
open FlowBuilderExcel
open LegosOps





//escenario_Supply_Transport_Trade ()
// escenario_supply_Transport_Sleeve ()

// escenarioSupplyTradeTransporteConsumo()
type Config = { modo: string;  central: string; path: string }

// Configuraciones 
let getConfig modo central path : Config =
    // Aquí puedes cambiar los valores para probar diferentes configuraciones
    match modo, central, path with
    | "CUR-A", "ESLP", "Default" -> { modo = "CUR-A"; central = "ESLP"; path = "Default" }  
    | "CUR-A", "BAJIO", "Default" -> { modo = "CUR-A";  central = "EAVIII"; path = "Default" }
    | "LT", "BAJIO", "Default" -> { modo = "LT"; central = "EAVIII"; path = "Default" }
    | "CUR", "LR", "Default" -> { modo = "CUR";  central = "EAX"; path = "Default" }
    | "LTF", "LR", "Default" -> { modo = "LTF";  central = "EBC"; path = "Default" }
    | "CUR", "ESLP", "Path1" -> { modo = "CUR";  central = "ESLP"; path = "Path1" }
    | _ -> failwith "Configuración no encontrada."



let config = getConfig "CUR" "ESLP" "Path1"
let diaGas = DateOnly(2025, 12, 10)

printfn "Modo %s\tPlanta: %s\tCentral-> %s" config.modo config.central config.path



// let blocks = buildBlocksFromExcel(excelPath) config.modo config.planta config.central
let flowSteps = buildFlowSteps(excelPath) config.modo  config.central config.path diaGas


let res = runSteps flowSteps st0

printfn "%A" res




//let ops = compile blocks


0