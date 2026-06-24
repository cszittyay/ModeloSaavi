open System
open System.Globalization
open Escenario

let private defaultPlants = [ 1..1 ]
let private defaultDayOffsets = [ 0..0 ]

let private tryParseDateOnly (value: string) =
    match DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None) with
    | true, gasDay -> Some gasDay
    | _ -> None

let private promptDate () =
    let rec loop () =
        printf "Dia gas inicial (yyyy-MM-dd): "
        match Console.ReadLine() |> tryParseDateOnly with
        | Some gasDay -> gasDay
        | None ->
            printfn "Fecha invalida. Ejemplo: 2026-03-01"
            loop ()

    loop ()

let private runPlantDay idPlanta (gasDay: DateOnly) =
    let gasDayText = gasDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)

    printfn ""
    printfn $"Planta {idPlanta} - DiaGas {gasDayText}"

    match FlowRunRepo.runFlowsAndPersistDBByPlanta idPlanta gasDay with
    | Ok results ->
        printfn $"OK - FlowRuns generados: {results.Length}"
        true
    | Error e ->
        printfn $"ERROR - {e}"
        false

let private runBatchAll (gasDay: DateOnly) =
    let gasDayText = gasDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)

    printfn ""
    printfn $"Corrida batch desde {gasDayText}"
    printfn $"Plantas: {defaultPlants}"
    printfn $"Dias   : {defaultDayOffsets}"
    printfn "=============================="

    let mutable ok = 0
    let mutable errors = 0

    for idPlanta in defaultPlants do
        for i in defaultDayOffsets do
            let day = gasDay.AddDays(i)
            if runPlantDay idPlanta day then
                ok <- ok + 1
            else
                errors <- errors + 1

    printfn ""
    printfn "Resumen batch"
    printfn "============="
    printfn $"OK     : {ok}"
    printfn $"Errores: {errors}"

    if errors = 0 then 0 else 1

let private printUsage () =
    printfn "Uso:"
    printfn "  ModeloSaavi.Console.exe batch-all yyyy-MM-dd"
    printfn ""
    printfn "Ejemplo:"
    printfn "  ModeloSaavi.Console.exe batch-all 2026-03-01"
    printfn ""
    printfn "Sin argumentos, la consola pide la fecha inicial y ejecuta plantas [1..5] / dias [0..9]."

[<EntryPoint>]
let main argv =
    //let diaGas = DateOnly(2026, 3,1)
    //runPlantDay 2 diaGas  |> ignore



    match argv |> Array.toList with
    | [] ->
        promptDate () |> runBatchAll
    | [ "batch-all"; dateText ] ->
        match tryParseDateOnly dateText with
        | Some gasDay -> runBatchAll gasDay
        | None ->
            printfn $"Fecha invalida: {dateText}"
            printUsage ()
            1
    | [ "--help" ] | [ "-h" ] | [ "/?" ] ->
        printUsage ()
        0
    | _ ->
        printUsage ()
        1
    