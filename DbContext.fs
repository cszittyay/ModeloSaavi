module DbContext


open FSharp.Data.Sql

//let connectionString = "Server=LAPTOP-9EH9CLGC\MSSQLSERVER03;Database=GNX.Core.local;Trusted_Connection=True;TrustServerCertificate=True;"
// Base de datos: GNX_Develop_Local
// 
[<Literal>]
let connectionString = "Server=DESKTOP-8GOI1HK\MSSQLSERVER04;Database=GNX_Develop_local;Trusted_Connection=True;TrustServerCertificate=True;"

[<Literal>]
let useOptionTypes = FSharp.Data.Sql.Common.NullableColumnType.OPTION
type sqlGnx = SqlDataProvider<Common.DatabaseProviderTypes.MSSQLSERVER, connectionString, UseOptionTypes = useOptionTypes>

let xxx = sqlGnx.GetDataContext(connectionString) 
let con = xxx.CreateConnection()

module FlowDB =

    type sqlGnx = SqlDataProvider<Common.DatabaseProviderTypes.MSSQLSERVER, connectionString, UseOptionTypes = useOptionTypes>

    type Ctx = sqlGnx.dataContext

    let createCtx (connectionString: string) = sqlGnx.GetDataContext(connectionString)

    type FlowMaster = Ctx.``fm.FlowMasterEntity``
    type FlowDetail = Ctx.``fm.FlowDetailEntity``   
    type FlowRun = Ctx.``fm.FlowRunEntity``
    type FlowTrade  = Ctx.``fm.TradeEntity``
    type FlowSupply = Ctx.``fm.SupplyEntity``
    type FlowTransport = Ctx.``fm.TransportEntity``
    type FlowSleeve = Ctx.``fm.SleeveEntity``
    type IndicePrecio = Ctx.``platts.IndicePrecioEntity``    
    type Ruta = Ctx.``dbo.RutaEntity``
    type Punto = Ctx.``dbo.PuntoEntity``
    type Contrato = Ctx.``dbo.ContratoEntity``
    type Cliente = Ctx.``dbo.ClienteEntity``
    type TransaccionGas = Ctx.``dbo.TransaccionGasEntity``
    type TransaccionTransporte = Ctx.``dbo.TransaccionTransporteEntity``
    type EntidadLegal = Ctx.``dbo.EntidadLegalEntity``
    type VentaGas = Ctx.``dbo.VentaGasEntity``

