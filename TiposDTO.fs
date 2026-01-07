
namespace Gnx.Domain



open System

/// IDs (strong types) to evitar mezclar PK/FK accidentalmente.
[<Struct; >]
type ContratoId = ContratoId of int

[<Struct>]
type TransaccionId = TransaccionId of int

[<Struct; NoEquality; NoComparison>]
type CompraGasId = CompraGasId of int

[<Struct; NoEquality; NoComparison>]
type RutaId = RutaId of int

[<Struct; NoEquality; NoComparison>]
type PuntoId = PuntoId of int

[<Struct; NoEquality; NoComparison>]
type PlantaId = PlantaId of int

[<Struct; NoEquality; NoComparison>]
type EstadoContratoId = EstadoContratoId of int

/// ========================
/// DU de catálogos
/// ========================

/// Tipo de contrato: representado por nemónico (tabla TipoContrato) pero modelado como DU extensible.
/// La idea es que tu dominio tenga casos “con significado”, y un escape-hatch.
type TipoContrato =
  | Firme
  | Interrumpible
  | Spot
  | Transporte
  | Peaje
  | Otro of nemonico:string

module TipoContrato =
  /// Map desde el nemónico de la tabla (ej: "FIRME", "INT", etc.) al DU.
  /// Ajustá las reglas de matching según tus catálogos reales.
  let ofNemonico (nem:string) =
    match nem.Trim().ToUpperInvariant() with
    | "FIRME" | "FIRM" | "F" -> Firme
    | "INT" | "INTERR" | "INTERRUPTIBLE" -> Interrumpible
    | "SPOT" -> Spot
    | "TRANSP" | "TRANSPORTE" | "TTE" -> Transporte
    | "PEAJE" | "T6" -> Peaje
    | other -> Otro other

  /// Vuelve a nemónico (para persistir / comparar).
  let toNemonico = function
    | Firme -> "FIRME"
    | Interrumpible -> "INT"
    | Spot -> "SPOT"
    | Transporte -> "TRANSP"
    | Peaje -> "PEAJE"
    | Otro n -> n

/// Tipo de transacción: igual idea (tabla TipoTransaccion).
type TipoTransaccion =
  | Compra
  | Venta
  | Compra_GyT
  | Transporte
  | Otro of descripcion:string

module TipoTransaccion =
  /// Map desde descripción/código a DU (ajustable).
  let ofDescripcion (desc:string) =
    match desc.Trim().ToUpperInvariant() with
    | "Compra de Gas"  -> Compra
    | "Venta de Gas" -> Venta
    | "Compra de Gas y Transporte" -> Compra
    | other -> Otro other

    


/// ========================
/// Entidades de dominio (records)
/// ========================

type Contrato =
  { id: ContratoId
    tipo: TipoContrato
    idParte: int
    idContraparte: int
    vigenciaDesde: DateOnly
    vigenciaHasta: DateOnly
    estadoId: EstadoContratoId
    leyAplicable: string option
    fechaDePago: string option
    fechaDeFirma: DateOnly option
    idMoneda: int
    observaciones: string option
    codigo: string }

type Transaccion =
  { id: TransaccionId
    tipo: TipoTransaccion
    idContrato: ContratoId
    idPuntoEntrega: int
    idTipoServicio: int
    idIndicePrecio: int option
    adder: decimal option
    fuel: decimal
    tarifaTransporte: decimal
    formulaPrecio: string option
    precioFijo: decimal option
    volumen: decimal
    observaciones: string option
    vigenciaDesde: DateOnly
    vigenciaHasta: DateOnly
    idMonedaPrecioFijo: int option
    idUnidadPrecioEnergiaAdder: int option
    idUnidadEnergiaVolumen: int option }

type CompraGas =
  { id: CompraGasId
    diaGas: DateOnly
    idTransaccion: TransaccionId 
    idFlowDetail: int
    buyBack: bool option
    idPuntoEntrega: int
    temporalidad: string
    idIndicePrecio: int option
    precio: decimal option
    adder: decimal option
    nominado: decimal
    confirmado: decimal option
    }




/// Tipos generados a partir de ScriptsTablas_GNX_develop_Local.sql
module Db =

  // --- Strongly-typed IDs (evita mezclar IDs de tablas distintas) ---

  [<Struct; NoEquality; NoComparison>]
  type CompraGasId = CompraGasId of int

  [<Struct; NoEquality; NoComparison>]
  type CompraSpotId = CompraSpotId of int

  [<Struct; NoEquality; NoComparison>]
  type ConsumoId = ConsumoId of int

  [<Struct; NoEquality; NoComparison>]
  type ContratoId = ContratoId of int

  [<Struct; NoEquality; NoComparison>]
  type ContratoPlantaId = ContratoPlantaId of int

  [<Struct; NoEquality; NoComparison>]
  type EstadoContratoId = EstadoContratoId of int

  [<Struct; NoEquality; NoComparison>]
  type PlantaId = PlantaId of int

  [<Struct; NoEquality; NoComparison>]
  type ProductorId = ProductorId of int

  [<Struct; NoEquality; NoComparison>]
  type PuntoId = PuntoId of int

  [<Struct; NoEquality; NoComparison>]
  type RutaId = RutaId of int

  [<Struct; NoEquality; NoComparison>]
  type TipoCompraSpotId = TipoCompraSpotId of int

  [<Struct; NoEquality; NoComparison>]
  type TipoContratoId = TipoContratoId of byte

  [<Struct; NoEquality; NoComparison>]
  type TipoPuntoId = TipoPuntoId of int

  [<Struct; NoEquality; NoComparison>]
  type TipoServicioId = TipoServicioId of int

  [<Struct; NoEquality; NoComparison>]
  type TipoTransaccionId = TipoTransaccionId of int

  [<Struct>]
  type TransaccionId = TransaccionId of int

  // --- Tablas ---

  [<CLIMutable>]
  type CompraGas = {
    /// SQL: [Id_CompraGas] [int] IDENTITY(1,1) NOT NULL
    idCompraGas: CompraGasId
    /// SQL: [DiaGas] [date] NOT NULL
    diaGas: System.DateOnly
    /// SQL: [Nominado] [decimal] (16, 4) NOT NULL
    nominado: decimal
    /// SQL: [Confirmado] [decimal] (16, 4) NULL
    confirmado: decimal option
    /// SQL: [Asignado] [decimal] (16, 4) NULL
    asignado: decimal option
    /// SQL: [Id_PuntoEntrega] [int] NOT NULL
    idPuntoEntrega: int
    /// SQL: [Id_Ruta] [int] NULL
    idRuta: RutaId option
    /// SQL: [Id_Transaccion] [int] NULL
    idTransaccion: int option
    /// SQL: [Id_CompraSpot] [int] NULL
    idCompraSpot: CompraSpotId option
    /// SQL: [Temporalidad] [varchar] (50) NOT NULL
    temporalidad: string
    /// SQL: [Id_VentaGas] [int] NULL
    idVentaGas: int option
  }

  [<CLIMutable>]
  type CompraSpot = {
    /// SQL: [Id_CompraSpot] [int] IDENTITY(1,1) NOT NULL
    idCompraSpot: CompraSpotId
    /// SQL: [Id_Proveedor] [int] NOT NULL
    idProveedor: int
    /// SQL: [Id_IndicePrecio] [int] NULL
    idIndicePrecio: int option
    /// SQL: [Precio] [decimal] (16, 4) NULL
    precio: decimal option
    /// SQL: [Adder] [decimal] (16, 4) NULL
    adder: decimal option
    /// SQL: [Cantidad] [decimal] (16, 4) NULL
    cantidad: decimal option
    /// SQL: [Formula] [varchar] (200) NULL
    formula: string option
    /// SQL: [TipoOperacion] [varchar] (50) NULL
    tipoOperacion: string option
  }

  [<CLIMutable>]
  type Consumo = {
    /// SQL: [Id_Consumo] [int] IDENTITY(1,1) NOT NULL
    idConsumo: ConsumoId
    /// SQL: [Id_Punto] [int] NOT NULL
    idPunto: PuntoId
    /// SQL: [EnergiaGJ] [decimal] (13, 6) NULL
    energiaGJ: decimal option
    /// SQL: [ConsumoM3] [decimal] (13, 6) NULL
    consumoM3: decimal option
    /// SQL: [PoderCalorificoMJ] [decimal] (10, 6) NULL
    poderCalorificoMJ: decimal option
    /// SQL: [DiaGas] [date] NOT NULL
    diaGas: System.DateOnly
    /// SQL: [Demanda] [decimal] (13, 6) NULL
    demanda: decimal option
  }

  [<CLIMutable>]
  type Contrato = {
    /// SQL: [Id_Contrato] [int] IDENTITY(1,1) NOT NULL
    idContrato: ContratoId
    /// SQL: [Id_TipoContrato] [tinyint] NOT NULL
    idTipoContrato: TipoContratoId
    /// SQL: [Id_Parte] [int] NOT NULL
    idParte: int
    /// SQL: [Id_Contraparte] [int] NOT NULL
    idContraparte: int
    /// SQL: [VigenciaDesde] [date] NOT NULL
    vigenciaDesde: System.DateOnly
    /// SQL: [VigenciaHasta] [date] NOT NULL
    vigenciaHasta: System.DateOnly
    /// SQL: [Id_EstadoContrato] [int] NOT NULL
    idEstadoContrato: EstadoContratoId
    /// SQL: [LeyAplicable] [varchar] (50) NULL
    leyAplicable: string option
    /// SQL: [FechaDePago] [varchar] (50) NULL
    fechaDePago: string option
    /// SQL: [FechaDeFirma] [date] NULL
    fechaDeFirma: System.DateOnly option
    /// SQL: [Id_Moneda] [int] NOT NULL
    idMoneda: int
    /// SQL: [Observaciones] [varchar] (300) NULL
    observaciones: string option
    /// SQL: [Codigo] [nvarchar] (30) NOT NULL
    codigo: string
  }

  [<CLIMutable>]
  type ContratoPlanta = {
    /// SQL: [Id_ContratoPlanta] [int] IDENTITY(1,1) NOT NULL
    idContratoPlanta: ContratoPlantaId
    /// SQL: [ID_Contrato] [int] NOT NULL
    iDContrato: ContratoId
    /// SQL: [ID_Planta] [int] NOT NULL
    iDPlanta: PlantaId
  }

  [<CLIMutable>]
  type EstadoContrato = {
    /// SQL: [Id_EstadoContrato] [int] IDENTITY(1,1) NOT NULL
    idEstadoContrato: EstadoContratoId
    /// SQL: [Nemonico] [varchar] (25) NOT NULL
    nemonico: string
    /// SQL: [Descripcion] [varchar] (50) NOT NULL
    descripcion: string
  }

  [<CLIMutable>]
  type Planta = {
    /// SQL: [ID_Planta] [int] IDENTITY(1,1) NOT NULL
    iDPlanta: PlantaId
    /// SQL: [Nombre] [nvarchar] (100) NOT NULL
    nombre: string
  }

  [<CLIMutable>]
  type Productor = {
    /// SQL: [Id_Productor] [int] NOT NULL
    idProductor: ProductorId
    /// SQL: [X_Cenagas] [varchar] (50) NULL
    xCenagas: string option
    /// SQL: [X_Esentia] [varchar] (50) NULL
    xEsentia: string option
    /// SQL: [Codigo_Quorum] [varchar] (5) NULL
    codigoQuorum: string option
    /// SQL: [NotificaBaseload] [bit] NOT NULL
    notificaBaseload: bool
  }

  [<CLIMutable>]
  type Punto = {
    /// SQL: [Id_Punto] [int] IDENTITY(1,1) NOT NULL
    idPunto: PuntoId
    /// SQL: [Nombre] [varchar] (50) NOT NULL
    nombre: string
    /// SQL: [Codigo] [varchar] (50) NOT NULL
    codigo: string
    /// SQL: [ID_Planta] [int] NULL
    iDPlanta: PlantaId option
    /// SQL: [ID_Gasoducto] [int] NULL
    iDGasoducto: int option
  }

  [<CLIMutable>]
  type Ruta = {
    /// SQL: [Id_Ruta] [int] IDENTITY(1,1) NOT NULL
    idRuta: RutaId
    /// SQL: [ID_Contrato] [int] NOT NULL
    iDContrato: ContratoId
    /// SQL: [ID_PuntoRecepcion] [int] NOT NULL
    iDPuntoRecepcion: int
    /// SQL: [ID_PuntoEntrega] [int] NOT NULL
    iDPuntoEntrega: int
    /// SQL: [ID_UnidadEnergia] [int] NOT NULL
    iDUnidadEnergia: int
    /// SQL: [VigenciaDesde] [date] NOT NULL
    vigenciaDesde: System.DateOnly
    /// SQL: [VigenciaHasta] [date] NULL
    vigenciaHasta: System.DateOnly option
    /// SQL: [Fuel] [decimal] (12, 4) NOT NULL
    fuel: decimal
    /// SQL: [ID_TipoServicioTransporte] [int] NULL
    iDTipoServicioTransporte: int option
    /// SQL: [Cantidad] [decimal] (12, 3) NOT NULL
    cantidad: decimal
    /// SQL: [PrecioNegociado] [decimal] (18, 4) NULL
    precioNegociado: decimal option
    /// SQL: [Id_UnidadPrecioEnergia] [int] NULL
    idUnidadPrecioEnergia: int option
    /// SQL: [FormulaPrecio] [varchar] (200) NULL
    formulaPrecio: string option
    /// SQL: [CargoReserva] [decimal] (10, 4) NOT NULL
    cargoReserva: decimal
    /// SQL: [CargoAdministrativo] [decimal] (10, 4) NOT NULL
    cargoAdministrativo: decimal
    /// SQL: [CargoUso] [decimal] (10, 4) NOT NULL
    cargoUso: decimal
    /// SQL: [CargoOtros] [decimal] (10, 4) NOT NULL
    cargoOtros: decimal
  }

  [<CLIMutable>]
  type TipoCompraSpot = {
    /// SQL: [Id_TipoCompraSpot] [int] IDENTITY(1,1) NOT NULL
    idTipoCompraSpot: TipoCompraSpotId
    /// SQL: [Descripcion] [varchar] (50) NOT NULL
    descripcion: string
    /// SQL: [Nemonico] [varchar] (50) NOT NULL
    nemonico: string
  }

  [<CLIMutable>]
  type TipoContrato = {
    /// SQL: [Id_TipoContrato] [tinyint] IDENTITY(1,1) NOT NULL
    idTipoContrato: TipoContratoId
    /// SQL: [Nemonico] [varchar] (25) NOT NULL
    nemonico: string
    /// SQL: [Descripcion] [varchar] (50) NOT NULL
    descripcion: string
  }

  [<CLIMutable>]
  type TipoPunto = {
    /// SQL: [Id_TipoPunto] [int] IDENTITY(1,1) NOT NULL
    idTipoPunto: TipoPuntoId
    /// SQL: [Nemonico] [varchar] (25) NOT NULL
    nemonico: string
    /// SQL: [Rol] [char] (1) NOT NULL
    rol: string
    /// SQL: [Descripcion] [varchar] (100) NOT NULL
    descripcion: string
  }

  [<CLIMutable>]
  type TipoServicio = {
    /// SQL: [Id_TipoServicio] [int] IDENTITY(1,1) NOT NULL
    idTipoServicio: TipoServicioId
    /// SQL: [Descripcion] [varchar] (50) NOT NULL
    descripcion: string
  }

  [<CLIMutable>]
  type TipoTransaccion = {
    /// SQL: [Id_TipoTransaccion] [int] IDENTITY(1,1) NOT NULL
    idTipoTransaccion: TipoTransaccionId
    /// SQL: [Descripcion] [varchar] (50) NOT NULL
    descripcion: string
  }

  [<CLIMutable>]
  type Transaccion = {
    /// SQL: [Id_Transaccion] [int] IDENTITY(1,1) NOT NULL
    idTransaccion: TransaccionId
    /// SQL: [Id_IndicePrecio] [int] NULL
    idIndicePrecio: int option
    /// SQL: [Id_PuntoEntrega] [int] NOT NULL
    idPuntoEntrega: int
    /// SQL: [Id_TipoTransaccion] [int] NOT NULL
    idTipoTransaccion: TipoTransaccionId
    /// SQL: [Id_TipoServicio] [int] NOT NULL
    idTipoServicio: TipoServicioId
    /// SQL: [Id_Contrato] [int] NOT NULL
    idContrato: ContratoId
    /// SQL: [Adder] [decimal] (18, 4) NULL
    adder: decimal option
    /// SQL: [Fuel] [decimal] (18, 6) NOT NULL
    fuel: decimal
    /// SQL: [TarifaTransporte] [decimal] (18, 6) NOT NULL
    tarifaTransporte: decimal
    /// SQL: [FormulaPrecio] [nvarchar] (200) NULL
    formulaPrecio: string option
    /// SQL: [PrecioFijo] [decimal] (18, 4) NULL
    precioFijo: decimal option
    /// SQL: [Volumen] [decimal] (18, 6) NOT NULL
    volumen: decimal
    /// SQL: [Observaciones] [nvarchar] (200) NULL
    observaciones: string option
    /// SQL: [VigenciaDesde] [date] NOT NULL
    vigenciaDesde: System.DateOnly
    /// SQL: [VigenciaHasta] [date] NOT NULL
    vigenciaHasta: System.DateOnly
    /// SQL: [Id_MonedaPrecioFijo] [int] NULL
    idMonedaPrecioFijo: int option
    /// SQL: [Id_UnidadPrecioEnergiaAdder] [int] NULL
    idUnidadPrecioEnergiaAdder: int option
    /// SQL: [Id_UnidadEnergiaVolumen] [int] NULL
    idUnidadEnergiaVolumen: int option
  }
