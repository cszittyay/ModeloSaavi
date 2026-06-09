open System
open System.Globalization
open Tipos
open Escenario

type RunArgs =
    { IdPlanta: int
      GasDay: DateOnly
      CantidadFuente: CompraGasCantidadFuente
      Dias: int }

let private plantas =
    [ 1, "LR"
      2, "Bajio (EAVIII)"
      3, "ESLP"
      4, "ECHI"
      5, "CTM" ]

let private parseDate (value: string) =
    DateOnly.Parse(value, CultureInfo.InvariantCulture)

let private parseCantidadFuente (value: string) =
    match value.Trim().ToLowerInvariant() with
    | "1" | "n" | "nominado" | "nominated" -> CompraGasCantidadFuente.Nominado
    | "2" | "c" | "confirmado" | "confirmed" -> CompraGasCantidadFuente.Confirmado
    | other -> failwith $"Tipo de corrida no valido: {other}. Use Nominado o Confirmado."

let private parsePositiveInt (value: string) =
    let parsed = Int32.Parse(value, CultureInfo.InvariantCulture)

    if parsed <= 0 then failwith "Debe ser mayor a cero."
    else parsed

let private readText prompt defaultValue =
    printf "%s [%s]: " prompt defaultValue
    let value = Console.ReadLine()

    if String.IsNullOrWhiteSpace value then defaultValue
    else value.Trim()

let rec private readValue prompt defaultValue parser =
    let value = readText prompt defaultValue

    try
        parser value
    with ex ->
        printfn $"Valor invalido: {ex.Message}"
        readValue prompt defaultValue parser

let private showHeader () =
    printfn ""
    printfn "ModeloSaavi - Corrida de Flow"
    printfn "============================="
    printfn ""
    printfn "Plantas:"
    plantas |> List.iter (fun (id, name) -> printfn $"  {id} - {name}")
    printfn ""
    printfn "Tipo de corrida CompraGas:"
    printfn "  1 - Nominado"
    printfn "  2 - Confirmado"
    printfn ""

let private parseArgs (argv: string array) =
    { IdPlanta = Int32.Parse(argv[0], CultureInfo.InvariantCulture)
      GasDay = parseDate argv[1]
      CantidadFuente = parseCantidadFuente argv[2]
      Dias =
        if argv.Length >= 4 then parsePositiveInt argv[3]
        else 1 }

let private readInteractiveArgs () =
    showHeader ()

    { IdPlanta = readValue "IdPlanta" "3" (fun x -> Int32.Parse(x, CultureInfo.InvariantCulture))
      GasDay = readValue "DiaGas (yyyy-MM-dd)" "2026-06-07" parseDate
      CantidadFuente = readValue "Tipo CompraGas" "Nominado" parseCantidadFuente
      Dias = readValue "Cantidad de dias" "1" parsePositiveInt }

let private getRunArgs (argv: string array) =
    match argv.Length with
    | 0 -> readInteractiveArgs ()
    | length when length >= 3 -> parseArgs argv
    | _ ->
        failwith "Argumentos: <idPlanta> <yyyy-MM-dd> <Nominado|Confirmado> [dias]"

let private runOneDay args offset =
    let gasDay = args.GasDay.AddDays(offset)
    let gasDayText = gasDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    printfn ""
    printfn $"Ejecutando Planta={args.IdPlanta} DiaGas={gasDayText} CompraGas={args.CantidadFuente}"

    match FlowRunRepo.runFlowsAndPersistDBByPlantaWithCantidadFuente args.CantidadFuente args.IdPlanta gasDay with
    | Ok results ->
        printfn $"Corrida OK. Runs generados: {results.Length}"
        results |> List.iter (fun r -> printfn $"  RunId: {r.RunId}")
        Ok ()
    | Error e ->
        printfn $"Error en corrida batch: {e}"
        Error e

[<EntryPoint>]
let main argv =

    try
        let args = getRunArgs argv
        let desdeText = args.GasDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)

        printfn ""
        printfn $"Parametros: Planta={args.IdPlanta}; Desde={desdeText}; Dias={args.Dias}; CompraGas={args.CantidadFuente}"

        [ 0 .. args.Dias - 1 ]
        |> List.fold
            (fun hasError offset ->
                match runOneDay args offset with
                | Ok () -> hasError
                | Error _ -> true)
            false
        |> function
            | true -> 1
            | false -> 0
    with ex ->
        printfn $"Error: {ex.Message}"
        1
