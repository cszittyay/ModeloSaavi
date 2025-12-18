module DbContext


open FSharp.Data.Sql

[<Literal>]
let connectionString = "Server=LAPTOP-9EH9CLGC\MSSQLSERVER03;Database=GNX.Core.local;Trusted_Connection=True;TrustServerCertificate=True;"

[<Literal>]
let useOptionTypes = FSharp.Data.Sql.Common.NullableColumnType.OPTION

type sqlGnx = SqlDataProvider<Common.DatabaseProviderTypes.MSSQLSERVER, connectionString, UseOptionTypes = useOptionTypes>

let ctx = sqlGnx.GetDataContext()

