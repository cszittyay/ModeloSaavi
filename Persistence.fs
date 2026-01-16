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



type TransaccionGasJoinRow =
  { Id_TransaccionGas: int
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


 
type TransaccionTransporteJoinRow =
  { Id_TransaccionTransporte: int
    Id_Contrato: int
    ContractRef : string 
    Parte : string 
    Contraparte : string
    Id_Parte : int
    Id_Contraparte : int
    PuntoEntrega : string
    PuntoRecepcion : string
    Id_PuntoEntrega: int
    Id_PuntoRecepcion: int
    Id_Ruta: int 
    CMD: decimal 
    UsageRate: decimal
    FuelMode: string
    Fuel: decimal
 }



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
