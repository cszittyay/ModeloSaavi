open System
open Escenario
open Tipos
open ProjectOperations
open ErrorLog.Logging
open ErrorLog.RunStages
open Gnx.Persistence.SQL_Data
open FlowBuilderDB



//let t = loadTransacciones()
//let c = loadContratos()
//let cg = loadCompraGas diaGas idFlowDetail

//cg|> Seq.iter (printfn "%A")


//let sd = buildSupplysDB diaGas 1 "Default"
//sd.Values|> Seq.iter (fun s -> printfn "%A\n" s)


//let sleeves = buildSleevesDB 2 "Default"
//sleeves.Values |>  Seq.iter (printfn "%A")

//let transport = buildTransportsDB 2 "Default"
//transport.Values |>  Seq.iter (printfn "%A")

//let trades = buildTradesDB 2  "Default"
//trades.Values |>  Seq.iter (printfn "%A")


//let cd = buildConsumeDB diaGas 1 "Default"
//cd.Values|> Seq.iter (fun s -> printfn "%A\n" s)

//let sd = buildSellsDB diaGas 1 "Default"
//sd.Values|> Seq.iter (fun s -> printfn "%A\n" s)

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



// let r = runFlowAndPersist excelPath config.modo config.central diaGas st0 
//let runKey = Guid.NewGuid()
//let modo = "CUR"
//let central = "EAX"
//let gasDay = DateOnly(2026,1,1)

//init "logs"

//let result =
//    withRunContext runKey modo central gasDay (fun () ->
//      logRunStarted()

//       Acá llamás tu función real
//      match runFlowAndPersist excelPath modo central gasDay st0 with
//      | Ok (runId, finalState, transitions) ->
//          logRunOk (Some runId) transitions.Length
//          Ok runId
//      | Error e ->
//          logRunFailed e
//          Error e
//    )


let diaGas = DateOnly(2026, 1, 20)

let runKey = Guid.NewGuid()
let modo = "CUR"
let central = "EAX"

init "logs"
let flowMaster = "CUR EBC"
let path = "Default"
let flowMasterId = 2

let result =
    withRunContext runKey flowMaster central diaGas (fun () ->
      logRunStarted()

      // Acá llamás tu función real
      match runrFlowAndPersistDB  flowMasterId path gasDay st0 with
      | Ok (runId, finalState, transitions) ->
          logRunOk (Some runId) transitions.Length
          Ok runId
      | Error e ->
          logRunFailed e
          Error e
    )
