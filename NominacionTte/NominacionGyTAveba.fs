module NominacionGyTAveba
open System
open Tipos

module AvebaTypes =

    type AvebaActionCode =
      | BUY
      | T      // Transport path
      | MAKEUP
      | PAYBACK
      | SELL

    type AvebaTransTypeCode =
      | CurrentNomination   // 01
      | Overrun             // 02
      | Makeup              // 03
      | Payback             // 04
      | Park                // 05
      | Loan                // 06
      | SAT                 // 07
      | STTI                // 08

    type AvebaBuyLeg =
      { idRecLoc: string    // dbo.Punto.Codigo
        recQty: decimal
        srCtrNo: string }

    type AvebaTransportLeg =
      { idRecLoc: string
        idDelLoc: string
        delQty: decimal
        srCtrNo: string
        idPkg: string option
        rank: int option }

    type AvebaDeliveryLeg =
      { idDelLoc: string
        delQty: decimal
        srCtrNo: string }


    type AvebaRow =
      { SubmitDate     : DateOnly option
        BegGasDay      : DateOnly
        EndGasDay      : DateOnly
        IdRecLoc       : string option
        IdDelLoc       : string option
        ActnCode       : AvebaActionCode
        SrCtrNo        : string               // Service Requestor / Contract No (ej: "SBF/043/17")
        RecBpNo        : string option        // UPID (ej: "0136")
        DelBpNo        : string option        // DnID (ej: "DS-SAAVI")
        TransTypeCode  : AvebaTransTypeCode
        RouteType      : string option
        RecQty         : decimal option
        DelQty         : decimal option
        RouteCode      : string option
        IdPkg          : string option
        IdRecPkg       : string option
        IdDelPkg       : string option
        Rank           : int option }

    // “Wire” (para exportar tal cual a archivo)
    // DTO used for CSV serialization/deserialization
    [<CLIMutable>]
    type AvebaRowDto =
      { SubmitDate    : string
        BegGasDay     : string
        EndGasDay     : string
        IdRecLoc      : string
        IdDelLoc      : string
        ActnCode      : string
        SrCtrNo       : string
        RecBpNo       : string
        DelBpNo       : string
        TransTypeCode : string
        ``Route Type``: string
        RecQty        : decimal
        DelQty        : decimal
        RouteCode     : string
        IdPkg         : string
        IdRecPkg      : string
        IdDelPkg      : string
        Rank          : int }


    type AvebaNominationConfig =
      { srCtrNo       : string             // ej "SBF/043/17"
        recBpNo       : string option      // ej "0136" (UPID)
        delBpNo       : string option      // ej "DS-SAAVI" (DnID)
        defaultTransType : AvebaTransTypeCode  // típicamente CurrentNomination
        packageForPath : string -> string option   // map path->PKG1/PKG2...
        rankForPath    : string -> int option }

    type RunId = int

    type SupplyResult =
      { RunId: RunId
        GasDay: DateOnly
        AvebaRow: AvebaRow }

    type TransportResult =
      { RunId: RunId
        GasDay: DateOnly
        AvebaRow: AvebaRow }

    type InfraError =
      | NotFound of string
      | DbError of string
      | IOError of string
      | UnknownError of string

    type AvebaLoaders =
      { getSupplyResults   : RunId -> DateOnly -> Result<SupplyResult list, InfraError>
        getTransportResults: RunId -> DateOnly -> Result<TransportResult list, InfraError>}


module AvebaRowDtoMapping =
    open System.Globalization
    open AvebaTypes
    
    let private fmtDate (d: DateOnly) = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)

    let private actnToStr = function
      | BUY -> "BUY" | T -> "T" | MAKEUP -> "MAKEUP" | PAYBACK -> "PAYBACK" | SELL -> "SELL"

    let private ttToStr = function
      | CurrentNomination -> "01" | Overrun -> "02" | Makeup -> "03" | Payback -> "04"
      | Park -> "05" | Loan -> "06" | SAT -> "07" | STTI -> "08"

    let toDto (r: AvebaRow) : AvebaRowDto =
      { SubmitDate    = r.SubmitDate |> Option.map fmtDate |> Option.defaultValue ""
        BegGasDay     = fmtDate r.BegGasDay
        EndGasDay     = fmtDate r.EndGasDay
        IdRecLoc      = r.IdRecLoc |> Option.defaultValue ""
        IdDelLoc      = r.IdDelLoc |> Option.defaultValue ""
        ActnCode      = actnToStr r.ActnCode
        SrCtrNo       = r.SrCtrNo
        RecBpNo       = r.RecBpNo |> Option.defaultValue ""
        DelBpNo       = r.DelBpNo |> Option.defaultValue ""
        TransTypeCode = ttToStr r.TransTypeCode
        ``Route Type``= r.RouteType |> Option.defaultValue ""
        RecQty        = r.RecQty |> Option.defaultValue 0m
        DelQty        = r.DelQty |> Option.defaultValue 0m
        RouteCode     = r.RouteCode |> Option.defaultValue ""
        IdPkg         = r.IdPkg |> Option.defaultValue ""
        IdRecPkg      = r.IdRecPkg |> Option.defaultValue ""
        IdDelPkg      = r.IdDelPkg |> Option.defaultValue ""
        Rank          = r.Rank |> Option.defaultValue 0 }


module AvebaNominationGenerator =
    0