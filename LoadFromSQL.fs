namespace Gnx.Persistence

open System
open System.Collections.Generic
open System.Linq
open FSharp.Data.Sql
open DbContext
open Gnx.Domain
/// Ejemplo de “mapping” desde SqlProvider a Rows.
/// Ajustá connection string y el provider a tu entorno.



module SQL_Data =

  open ModeloSaavi.Infrastructure

  /// Ejemplo: traer contratos + tipo (join) y mapear a Domain.
  let loadContratos (lc:LoadContext) =
        lc.Run.ContratosById
          |> Map.values
          |> Seq.map (fun c ->
            let tipoCto = lc.Catalogs.TiposContratoById.[int c.IdTipoContrato]

            { ContratoRow.Id_Contrato = c.IdContrato
              Id_TipoContrato = c.IdTipoContrato
              NemonicoTipoContrato = Some tipoCto.Nemonico
              Id_Parte = c.IdParte
              Id_Contraparte = c.IdContraparte
              VigenciaDesde =  DateOnly.FromDateTime c.VigenciaDesde
              VigenciaHasta = DateOnly.FromDateTime c.VigenciaHasta
              Id_EstadoContrato = c.IdEstadoContrato
              LeyAplicable = c.LeyAplicable
              FechaDePago = c.FechaDePago
              FechaDeFirma = Option.bind (DateOnly.FromDateTime >> Some) c.FechaDeFirma
              Id_Moneda = c.IdMoneda
              Observaciones = c.Observaciones
              Codigo = c.Codigo 
            })
          |> Seq.map Mappings.contratoToDomain
          |> Seq.toList


  let loadTransaccionesGas (lc: LoadContext) =
        lc.Run.TransaccionesGasById
        |> Map.values
        |> Seq.map (fun t ->

            let c = lc.Run.ContratosById[t.IdContrato]
            let tt = lc.Catalogs.TiposTransaccionById[t.IdTipoTransaccion]
            let p = lc.Catalogs.PuntosById[t.IdPuntoRecepcion]
            let elcp = lc.Catalogs.EntidadLegalById.[c.IdContraparte]
            let elp = lc.Catalogs.EntidadLegalById.[c.IdParte]

            { TransaccionGasJoinRow.Id_TransaccionGas  = t.IdTransaccionGas
              Parte = elp.Nombre
              Contraparte = elcp.Nombre
              ContractRef = c.Codigo
              IdParte = c.IdParte
              IdContraparte = c.IdContraparte
              PuntoEntrega = p.Codigo
              Id_IndicePrecio = t.IdIndicePrecio
              Id_PuntoEntrega = t.IdPuntoRecepcion
              Id_TipoTransaccion = t.IdTipoTransaccion
              TipoTransaccionDescripcion = Some tt.Descripcion
              Id_TipoServicio = t.IdTipoServicio
              Id_Contrato = t.IdContrato
              Adder = t.Adder
              FormulaPrecio = t.FormulaPrecio
              PrecioFijo = t.PrecioFijo
              Volumen = t.Volumen
              })

        |> Seq.map Mappings.transaccionGasJoinToDomain
        |> Seq.toList

  
  let loadTransaccionesTransporte (lc: LoadContext) =
            lc.Run.TransaccionesTransporteById
            |> Map.values
            |> Seq.map (fun t ->

                   let c = lc.Run.ContratosById[t.IdContrato]
                   let r = lc.Catalogs.RutasById[t.IdRuta]
                   let puntoEntrada = lc.Catalogs.PuntosById[r.IdPuntoRecepcion]
                   let puntoSalida = lc.Catalogs.PuntosById[r.IdPuntoEntrega]
                   let elcp = lc.Catalogs.EntidadLegalById.[c.IdContraparte]
                   let elp = lc.Catalogs.EntidadLegalById.[c.IdParte]


                   {TransaccionTransporteJoinRow.Id_TransaccionTransporte = t.IdTransaccionTransporte
                    Parte = elp.Nombre
                    Contraparte = elcp.Nombre
                    ContractRef = c.Codigo
                    Id_Parte = c.IdParte
                    Id_Contrato = c.IdContrato
                    Id_Contraparte = c.IdContraparte
                    PuntoEntrega = puntoEntrada.Codigo
                    PuntoRecepcion = puntoSalida.Codigo  
                    Id_PuntoEntrega = r.IdPuntoEntrega
                    Id_PuntoRecepcion = r.IdPuntoRecepcion
                    FuelMode = t.FuelMode
                    Id_Ruta = r.IdRuta
                    Fuel = r.Fuel
                    CMD = t.Cmd
                    UsageRate = t.CargoUso
               })

            |> Seq.map Mappings.transaccionTransporteJoinToDomain
            |> Seq.toList





  let loadCompraGas (diaGas: DateOnly) idFlowDetail =
        let dia = diaGas.ToDateTime(TimeOnly.MinValue)

        query {
            for cg in ctx.Dbo.CompraGas do
            where (cg.DiaGas = dia && cg.IdFlowDetail = idFlowDetail)
            select cg
        }
        |> Seq.toList
        |> List.map (fun cg ->
            { CompraGasRow.Id_CompraGas = cg.IdCompraGas
              DiaGas = diaGas
              Id_Transaccion = cg.IdTransaccionGas
              Id_FlowDetail = cg.IdFlowDetail
              BuyBack = cg.BuyBack
              Id_PuntoEntrega = cg.IdPuntoEntrega
              Temporalidad = cg.Temporalidad
              Id_IndicePrecio = cg.IdIndicePrecio
              Adder = cg.Adder
              Precio = cg.Precio
              Nominado = cg.Nominado
              Confirmado = cg.Confirmado })
        |> List.map Mappings.compraGasToDomain


  // Convierte para un día Gas  un idFlowDetail  devuelve el consumo en MMBtu y el punto de entrega

  let loadConsumo (diaGas:DateOnly) idFlowDetail =
     
      let dia = diaGas.ToDateTime(TimeOnly.MinValue)
  
      let result = 
           query {
                  for cg in ctx.Dbo.Consumo do
                  where (cg.DiaGas = dia && cg.IdFlowDetail.Value = idFlowDetail)
                  select (cg)
              }
              |> Seq.toList
              |> List.map(fun c -> c.IdPunto, c.Demanda)
      result


  let private entidadLegalById =
    lazy (ctx.Dbo.EntidadLegal |> Seq.map (fun e -> e.IdEntidadLegal, e) |> Map.ofSeq)


  let gasoductoById =
    lazy (ctx.Dbo.Gasoducto |> Seq.map (fun g -> g.IdGasoducto, g) |> Map.ofSeq)    

  let puntoCodigoById =
    lazy (ctx.Dbo.Punto |> Seq.map (fun p -> p.IdPunto, p.Codigo) |> Map.ofSeq)

  let  contratosById lc =
    lazy (loadContratos(lc:LoadContext) |> List.map (fun c -> c.id, c) |> Map.ofList)


  let transaccionesGasById(lc: LoadContext) =
        lazy (loadTransaccionesGas(lc) |> List.map (fun t -> t.id, t) |> Map.ofList)

  let transaccionesTransporteById(lc)=
        lazy (loadTransaccionesTransporte(lc) |> List.map (fun t -> t.id, t) |> Map.ofList)

  let flowMasterByNombre =
        lazy (ctx.Fm.FlowMaster |> Seq.map (fun fm -> fm.Nombre, fm) |> Map.ofSeq)

  let flowMasterById =
            lazy (ctx.Fm.FlowMaster |> Seq.map (fun fm -> fm.IdFlowMaster, fm) |> Map.ofSeq)


  let tipoOperacionByDesc =
            lazy (
                ctx.Fm.TipoOperacion
                |> Seq.map (fun top -> top.Descripcion, top.IdTipoOperacion)
                |> Seq.toList
                |> Map.ofList
            )

  let tipoOperacionById =
            lazy (
                ctx.Fm.TipoOperacion
                |> Seq.map (fun top -> top.IdTipoOperacion, top.Descripcion)
                |> Seq.toList
                |> Map.ofList
            )


  let rutaById () =
            query {
             for r in ctx.Dbo.Ruta do
                 select (r.IdRuta, r)
            }|> Seq.toList |> Map.ofList



  // (IdFlowMaster, Path) -> seq FlowDetail
  let  flowDetailsByMasterPath =
    lazy (
        ctx.Fm.FlowDetail
        |> Seq.groupBy (fun fd -> (fd.IdFlowMaster, fd.Path))
        |> Seq.map (fun (k, v) -> k, v)
        |> Map.ofSeq
    )



  let ventasByFlowDetailId (diaGas:DateOnly) (flowDetailIds:int list) =
    let dia = diaGas.ToDateTime(TimeOnly.MinValue)
    let ids = flowDetailIds |> List.toArray
    query {
      for v in ctx.Dbo.VentaGas do
      where (v.DiaGas = dia && (ids.Contains(v.IdFlowDetail)))
      select  v
    } |> Seq.toList

// Operaciones “master data” por IdFlowDetail (las transaccionales suelen requerir diaGas)
  let tradeByFlowDetailId =
    lazy (ctx.Fm.Trade |> Seq.map (fun t -> t.IdFlowDetail, t) |> Map.ofSeq)

// NOTE: estas tablas pueden o no existir en tu schema `Fm`. Si existen, descomentá.
  let sleeveByFlowDetailId =
    lazy (ctx.Fm.Sleeve |> Seq.map (fun s -> s.IdFlowDetail, s) |> Map.ofSeq)
//
  let transportByFlowDetailId =
    lazy (ctx.Fm.Transport |> Seq.map (fun t -> t.IdFlowDetail, t) |> Map.ofSeq)
//

  let tradingHubNemonicoById =
    lazy (ctx.Platts.IndicePrecio |> Seq.map (fun th -> th.IdIndicePrecio, th.Nemonico) |> Map.ofSeq)


  let dFlowMaster = flowMasterById.Value
  let dPto = puntoCodigoById.Value
  let dGasoducto = gasoductoById.Value
  

