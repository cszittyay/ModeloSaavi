open System
open Escenario
open Tipos
open FlowBuilderExcel
open LegosOps
open ProjectOperations




//escenario_Supply_Transport_Trade ()
// escenario_supply_Transport_Sleeve ()

// escenarioSupplyTradeTransporteConsumo()
type Config = { modo: string;  central: string }

// Configuraciones 
let getConfig modo central : Config =
    // Aquí puedes cambiar los valores para probar diferentes configuraciones
    match modo, central with
    | "CUR-A", "ESLP"-> { modo = "CUR-A"; central = "ESLP"  }  
    | "CUR-A", "BAJIO"-> { modo = "CUR-A";  central = "EAVIII"}
    | "LT", "BAJIO"-> { modo = "LT"; central = "EAVIII"}
    | "CUR", "LR"-> { modo = "CUR";  central = "EAX"}
    | "LTF", "LR"-> { modo = "LTF";  central = "EBC"}
    | "CUR", "EAX"-> { modo = "CUR";  central = "EAX"}
    | "CUR", "ESLP" -> { modo = "CUR";  central = "ESLP" }
    | _ -> failwith "Configuración no encontrada."


let config = getConfig "CUR" "ESLP"
let diaGas = DateOnly(2025, 12, 10)

printfn "Modo %s\tPlanta: %s\tCentral-> %s" config.modo config.central |> ignore

let res = runAllModoCentral excelPath config.modo config.central diaGas


showTransitions res

