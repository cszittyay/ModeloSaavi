module Unidades
open System

// ===== Unidades (Units of Measure) =====
[<Measure>] type mmbtu
[<Measure>] type gj

// conversión: 1 MMBtu ≈ 1.055056 GJ
[<Literal>]
let gj_per_mmbtu = 1.055056

let inline toGJ (e: float<mmbtu>) : float<gj> =
    (float e) * gj_per_mmbtu |> LanguagePrimitives.FloatWithMeasure<gj>

let inline toMMBtu (e: float<gj>) : float<mmbtu> =
    (float e) / gj_per_mmbtu |> LanguagePrimitives.FloatWithMeasure<mmbtu>

// ===== Moneda =====
type Currency = USD | MXN

type Money = decimal * Currency

let money (amt: decimal) (ccy: Currency) : Money = (amt, ccy)

let addMoney (fx: Currency -> Currency -> decimal) ((a,ca):Money) ((b,cb):Money) : Money =
    // suma convirtiendo b->ca con una función FX provista por el caller
    let rate = fx cb ca
    (a + b * rate, ca)

let scaleMoney (k: decimal) ((a,ca):Money) : Money = (k*a, ca)
let round2 ((a,ca):Money) : Money = (Math.Round(a,2), ca)
let toStringMoney ((a,ca):Money) =
    let sym = match ca with USD -> "USD" | MXN -> "MXN"
    sprintf "%s %.2f" sym (float a)


