module DbContext


open FSharp.Data.Sql

//let connectionString = "Server=(localdb)\MSSQLLocalDB;Database=Testing_GNX_Saavi;Trusted_Connection=True;TrustServerCertificate=True;"
// Base de datos: GNX_Develop_Local
(*
    Server=sql-test.clxowb3nocuv.us-east-1.rds.amazonaws.com;
    Database=UAT_GNX_Saavi;
    User ID=gnx-uat;
    Password=B0C6-!$2s*qAgaM2o(£];
    TrustServerCertificate=True;
*)

// 
[<Literal>]
//let connectionString = "Server=sql-test.clxowb3nocuv.us-east-1.rds.amazonaws.com;User Id=gnx-uat;Database=UAT_GNX_Saavi;Password=B0C6-!$2s*qAgaM2o(£];TrustServerCertificate=True;"
let connectionString = "Server=(localdb)\MSSQLLocalDB;Database=PRD_GNX_Saavi;Trusted_Connection=True;TrustServerCertificate=True;"


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

type FlowDetail = sqlGnx.dataContext.``fm.FlowDetailEntity``   