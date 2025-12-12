open System
open Escenario
open FlowBuilderExcel
open LegosOps





//escenario_Supply_Transport_Trade ()
// escenario_supply_Transport_Sleeve ()

// escenarioSupplyTradeTransporteConsumo()
type Config = { modo: string; planta: string; central: string }

// Configuraciones 
let getConfig modo planta central : Config =
    // Aquí puedes cambiar los valores para probar diferentes configuraciones
    match modo, planta, central with
    | "CUR-A", "ESLP", "ESLP" -> { modo = "CUR-A"; planta = "ESLP"; central = "ESLP" }  
    | "CUR-A", "BAJIO", "EAVIII" -> { modo = "CUR-A"; planta = "BAJIO"; central = "EAVIII" }
    | "LT", "BAJIO", "EAVIII" -> { modo = "LT"; planta = "BAJIO"; central = "EAVIII" }
    | "CUR", "LR", "EAX" -> { modo = "CUR"; planta = "LR"; central = "EAX" }
    | "LTF", "LR", "EBC" -> { modo = "LTF"; planta = "LR"; central = "EBC" }
    | _ -> failwith "Configuración no encontrada."



let config = getConfig "CUR" "LR" "EAX"
let diaGas = DateOnly(2025, 12, 10)

printfn "Modo %s\tPlanta: %s\tCentral-> %s" config.modo config.planta config.central


let blocks = buildBlocksFromExcel(excelPath) config.modo config.planta config.central diaGas


let ops = compile blocks

run ops st0