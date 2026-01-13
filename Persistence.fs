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



type TransaccionJoinRow =
  { Id_Transaccion: int
    ContractRef : string 
    Id_IndicePrecio: int option
    Parte : string 
    Contraparte : string
    IdParte : int
    IdContraparte : int
    PuntoEntrega : string
    Id_PuntoEntrega: int
    Id_TipoTransaccion: int
    TipoTransaccionDescripcion: string option
    Id_TipoServicio: int
    Id_Contrato: int
    Adder: decimal option
    Fuel: decimal
    TarifaTransporte: decimal
    FormulaPrecio: string option
    PrecioFijo: decimal option
    Volumen: decimal
 }

[<CLIMutable>]
type TransaccionRow =
  { Id_Transaccion: int
    Id_IndicePrecio: int option
    Id_PuntoEntrega: int
    Id_TipoTransaccion: int
    Id_TipoServicio: int
    Id_Contrato: int
    Adder: decimal option           // (18,4)
    Fuel: decimal                   // (18,6)
    TarifaTransporte: decimal       // (18,6)
    FormulaPrecio: string option    // nvarchar(200)
    PrecioFijo: decimal option      // (18,4)
    Volumen: decimal                // (18,6)
    Observaciones: string option    // nvarchar(200)
    VigenciaDesde: DateOnly
    VigenciaHasta: DateOnly
    Id_MonedaPrecioFijo: int option
    Id_UnidadPrecioEnergiaAdder: int option
    Id_UnidadEnergiaVolumen: int option }



type CompraGasRow =
  { Id_CompraGas: int
    DiaGas: DateOnly
    Id_Transaccion: int
    Id_FlowDetail: int
    BuyBack: bool option
    Id_PuntoEntrega: int
    Temporalidad: string
    Id_IndicePrecio: int option
    Adder: decimal option
    Precio: decimal option
    Nominado: decimal
    Confirmado: decimal option
     }
