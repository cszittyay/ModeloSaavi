module DbContext


open FSharp.Data.Sql

//let connectionString = "Server=LAPTOP-9EH9CLGC\MSSQLSERVER03;Database=GNX.Core.local;Trusted_Connection=True;TrustServerCertificate=True;"
// Base de datos: GNX_Develop_Local
// 
[<Literal>]
let connectionString = "Server=(localdb)\MSSQLLocalDB;Database=Testing_GNX_Saavi;Trusted_Connection=True;TrustServerCertificate=True;"

[<Literal>]
let useOptionTypes = FSharp.Data.Sql.Common.NullableColumnType.OPTION

type sqlGnx = SqlDataProvider<
    Common.DatabaseProviderTypes.MSSQLSERVER, 
    connectionString, 
    UseOptionTypes = useOptionTypes>

let mutable ctx = sqlGnx.GetDataContext(connectionString)

// Esta función te permite obtener el contexto con CUALQUIER cadena en runtime
type DbFactory() =
    static member GetContext(runtimeConnString: string) =
        sqlGnx.GetDataContext(runtimeConnString)
    
    static member SetGlobalContext(runtimeConnString: string) =
        ctx <- sqlGnx.GetDataContext(runtimeConnString)