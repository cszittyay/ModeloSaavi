module Unidades
open System

// ===== Unidades (Units of Measure) =====
[<Measure>] type MMBTU
[<Measure>] type GJ
[<Measure>] type USD
[<Measure>] type MXN

// conversión: 1 MMBtu ≈ 1.055056 GJ
[<Literal>]
let gj_per_mmbtu = 1.055056m

let inline toGJ (e: decimal<MMBTU>) : decimal<GJ> =   (decimal e) * gj_per_mmbtu |> LanguagePrimitives.DecimalWithMeasure<GJ>

let inline toMMBtu (e: decimal<GJ>) : decimal<MMBTU> =  (decimal e) / gj_per_mmbtu |> LanguagePrimitives.DecimalWithMeasure<MMBTU>

// ===== Moneda =====


