open System
open Escenario
open Tipos
open FlowBuilderExcel
open ProjectOperations
open ErrorLog.Logging
open ErrorLog.RunStages
open Gnx.Persistence.SQL_Data
open FlowBuilderDB

let idFlowDetail = 1
let diaGas = DateOnly(2026, 1, 1)


//let t = loadTransacciones()
//let c = loadContratos()
//let cg = loadCompraGas diaGas idFlowDetail

//cg|> Seq.iter (printfn "%A")


//let sd = buildSupplysDB  diaGas idFlowDetail
//sd|> Seq.iter (printfn "%A")


let trades = buildTradesDB "CUR ESLP" "Path1"
// trades |> List.iter(fun x -> printfn $"Master:{x.IdFlowMaster}\tPath:{x.Path}\tDetail:{x.IdFlowDetail}\tOrden:{x.Orden}")
trades.Values |>  Seq.iter (printfn "%A")

////escenario_Supply_Transport_Trade ()
//// escenario_supply_Transport_Sleeve ()

//// escenarioSupplyTradeTransporteConsumo()
//type Config = { modo: string;  central: string }

//// Configuraciones 
//let getConfig modo central : Config =
//    // Aquí puedes cambiar los valores para probar diferentes configuraciones
//    match modo, central with
//    | "CUR-A", "ESLP"-> { modo = "CUR-A"; central = "ESLP"  }  
//    | "CUR-A", "BAJIO"-> { modo = "CUR-A";  central = "EAVIII"}
//    | "LT", "BAJIO"-> { modo = "LT"; central = "EAVIII"}
//    | "CUR", "LR"-> { modo = "CUR";  central = "EAX"}
//    | "LTF", "LR"-> { modo = "LTF";  central = "EBC"}
//    | "CUR", "EAX"-> { modo = "CUR";  central = "EAX"}
//    | "CUR", "ECHI"-> { modo = "CUR";  central = "ECHI"}
//    | "CUR", "ESLP" -> { modo = "CUR";  central = "ESLP" }
//    | _ -> failwith "Configuración no encontrada."


//// let config = getConfig "CUR" "ESLP"

////let config = getConfig "CUR" "EAX"

//let config = getConfig "CUR" "ESLP"

//let diaGas = DateOnly(2025, 12, 19)

//printfn "Modo %s\tPlanta: %s\tCentral-> %s" config.modo config.central |> ignore

////let res = runAllModoCentral excelPath config.modo config.central diaGas


//// showTransitions res



//// let r = runFlowAndPersist excelPath config.modo config.central diaGas st0 
//let runKey = Guid.NewGuid()
//let modo = "CUR"
//let central = "EAX"
//let gasDay = DateOnly(2025,12,19)

//init "logs"

//let result =
//    withRunContext runKey modo central gasDay (fun () ->
//      logRunStarted()

//      // Acá llamás tu función real
//      match runFlowAndPersist excelPath modo central gasDay st0 with
//      | Ok (runId, finalState, transitions) ->
//          logRunOk (Some runId) transitions.Length
//          Ok runId
//      | Error e ->
//          logRunFailed e
//          Error e
//    )

