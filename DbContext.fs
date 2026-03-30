module DbContext


open FSharp.Data.Sql
open FSharp.Data.Sql.Common

//let connectionString = "Server=LAPTOP-9EH9CLGC\MSSQLSERVER03;Database=GNX.Core.local;Trusted_Connection=True;TrustServerCertificate=True;"
// Base de datos: GNX_Develop_Local
// 
[<Literal>]
let connectionString = "Server=(localdb)\MSSQLLocalDB;Database=GNX_Develop_local;Trusted_Connection=True;TrustServerCertificate=True;"

[<Literal>]
let useOptionTypes = FSharp.Data.Sql.Common.NullableColumnType.OPTION

type sqlGnx = SqlDataProvider<Common.DatabaseProviderTypes.MSSQLSERVER, connectionString, UseOptionTypes = useOptionTypes>

type DataContext = sqlGnx.dataContext

let ctx =  sqlGnx.GetDataContext()

let createCtx () : DataContext =   sqlGnx.GetDataContext()

let createCtxWithConnectionString (connectionString:string) : DataContext =  sqlGnx.GetDataContext(connectionString)

type ClienteEntity = sqlGnx.dataContext.``dbo.ClienteEntity``
type CompraGasEntity = sqlGnx.dataContext.``dbo.CompraGasEntity``
type ConsumoEntity = sqlGnx.dataContext.``dbo.ConsumoEntity``
type ContratoEntity = sqlGnx.dataContext.``dbo.ContratoEntity``
type EntidadLegalEntity = sqlGnx.dataContext.``dbo.EntidadLegalEntity``
type GasoductoEntity = sqlGnx.dataContext.``dbo.GasoductoEntity``
type MonedaEntity = sqlGnx.dataContext.``dbo.MonedaEntity``
type PuntoEntity = sqlGnx.dataContext.``dbo.PuntoEntity``
type RutaEntity = sqlGnx.dataContext.``dbo.RutaEntity``
type TipoContratoEntity = sqlGnx.dataContext.``dbo.TipoContratoEntity``
type TipoServicioEntity = sqlGnx.dataContext.``dbo.TipoServicioEntity``
type TipoTransaccionEntity = sqlGnx.dataContext.``dbo.TipoTransaccionEntity``
type TransaccionGasEntity = sqlGnx.dataContext.``dbo.TransaccionGasEntity``
type TransaccionTransporteEntity = sqlGnx.dataContext.``dbo.TransaccionTransporteEntity``
type VentaGasEntity = sqlGnx.dataContext.``dbo.VentaGasEntity``



QueryEvents.SqlQueryEvent  |> ignore


QueryEvents.SqlQueryEvent.Add(fun sql ->    printfn "\nSQL Params >>\n %A\n Sql Command:\n%s" sql.Parameters sql.Command)