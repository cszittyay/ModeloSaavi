namespace Gnx.Persistence

open System

/// Records “planos” (primitivos) para persistencia.
/// Son los más convenientes para SqlProvider/Dapper (sin IDs strong-typed).
/// Columnas NULL => option.
[<CLIMutable>]
type ContratoRow =
  { Id_Contrato: int
    Id_TipoContrato: byte
    NemonicoTipoContrato: string option  // opcional si lo traés via join
    Id_Parte: int
    Id_Contraparte: int
    VigenciaDesde: DateOnly
    VigenciaHasta: DateOnly
    Id_EstadoContrato: int
    LeyAplicable: string option
    FechaDePago: string option
    FechaDeFirma: DateOnly option
    Id_Moneda: int
    Observaciones: string option
    Codigo: string }

[<CLIMutable>]
type TransaccionRow =
  { Id_Transaccion: int
    Id_TipoTransaccion: int
    DescripcionTipoTransaccion: string option // opcional si lo traés via join
    Id_Contrato: int option
    DiaGas: DateOnly
    Nominado: decimal
    Confirmado: decimal option
    Asignado: decimal option
    Id_PuntoEntrega: int
    Id_Ruta: int option
    Id_CompraSpot: int option
    Temporalidad: string
    Id_VentaGas: int option }

[<CLIMutable>]
type CompraGasRow =
  { Id_CompraGas: int
    DiaGas: DateOnly
    Nominado: decimal
    Confirmado: decimal option
    Asignado: decimal option
    Id_PuntoEntrega: int
    Id_Ruta: int option
    Id_Transaccion: int option
    Id_CompraSpot: int option
    Temporalidad: string
    Id_VentaGas: int option }

