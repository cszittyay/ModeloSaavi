#r "nuget: ClosedXML, 0.102.0"

open ClosedXML.Excel

let path = @"C:\Users\cszit\Downloads\EscenarioSupplyTrade_validated.xlsx"
let wb   = new XLWorkbook()

let wsSupply = wb.AddWorksheet("Supply")
wsSupply.Cell("A1").InsertData([
    ["tcId";"gasDay";"tradingHub";"temporalidad";"deliveryPt";"seller";"buyer";"qEnergia";"price";"adder";"contractRef"];
    ["TC-001";"2025-11-20";"Mainline";"DayAhead";"EHRENBERG";"Koch";"SES";"5300";"2.95";"0.029";"Sleeve-Trafigura"];
    ["TC-002";"2025-11-20";"Mainline";"DayAhead";"EHRENBERG";"JP Morgan";"SES";"4700";"3.95";"0.029";"Sleeve-Trafigura"];
])

// *********************
// Validación TradingHub
// *********************
let hubValidation = wsSupply.Range("C2:C1000").SetDataValidation()
hubValidation.AllowedValues  <- XLAllowedValues.List
hubValidation.InCellDropdown <- true
hubValidation.List("Mainline,Waha,HenryHub")

// *********************
// Validación Temporalidad
// *********************
let tempValidation = wsSupply.Range("D2:D1000").SetDataValidation()
tempValidation.AllowedValues  <- XLAllowedValues.List
tempValidation.InCellDropdown <- true
tempValidation.List("DayAhead,Intraday,Monthly")

// Resto de hojas (Trade, Transport, Consume) igual que antes…
let wsTrade = wb.AddWorksheet("Trade")
wsTrade.Cell("A1").InsertData([
    ["seller";"buyer";"adder";"contractRef"];
    ["Suppliers USA";"SES";"0.50";"MARKET_Z"];
    ["Suppliers USA";"SES";"0.60";"MARKET_Z"];
])

let wsTransport = wb.AddWorksheet("Transport")
wsTransport.Cell("A1").InsertData([
    ["provider";"entry";"exit";"shipper";"fuelPct";"usageRate";"reservation"];
    ["TC Energy";"EHRENBERG";"OGILBY";"EAX";"0.007";"0.08";"0.50"];
    ["Gasoducto Aguprieta";"OGILBY";"Planta_La_Estrella";"EAX";"0.001751";"0.08";"0.50"];
])

let wsConsume = wb.AddWorksheet("Consume")
wsConsume.Cell("A1").InsertData([
    ["provider";"meterLocation";"measured";"tolerancePct";"penaltyRate"];
    ["Savi Energía";"Planta_La_Estrella";"15300";"5.0";"0.10"];
])

wb.SaveAs(path)
printfn $"Archivo creado en: {path}"
