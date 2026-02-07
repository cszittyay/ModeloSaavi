module NominacionTransporteGRO
open System


type TransportNomSource =
  { RunId: int
    GasDay: DateOnly
    IdFlowDetail: int
    IdTransaccionTransporte: int
    CodContrato: string
    CodPuntoRecibo: string
    CodPuntoEntrega: string
    QtyInMMBtu: decimal
    QtyOutMMBtu: decimal
    FuelQtyMMBtu: decimal
  }

let mmbtuToGJ (x: decimal) : decimal =  x * 1.0550558m
