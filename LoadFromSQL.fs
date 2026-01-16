namespace Gnx.Persistence

open System
open FSharp.Data.Sql
open DbContext
open Gnx.Domain
/// Ejemplo de “mapping” desde SqlProvider a Rows.
/// Ajustá connection string y el provider a tu entorno.
module SQL_Data =

  

  /// Ejemplo: traer contratos + tipo (join) y mapear a Domain.
  let loadContratos () =
    query {
      for c in ctx.Dbo.Contrato do
      join tc in ctx.Dbo.TipoContrato on (c.IdTipoContrato = tc.IdTipoContrato)
      select
        { ContratoRow.Id_Contrato = c.IdContrato
          Id_TipoContrato = c.IdTipoContrato
          NemonicoTipoContrato = Some tc.Nemonico
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
          Codigo = c.Codigo }
    }
    |> Seq.map Mappings.contratoToDomain
    |> Seq.toList

  /// Ejemplo: transacciones + tipo (join) -> Domain
  let loadTransaccionesGas () =
    query {
      for t in ctx.Dbo.TransaccionGas do
      join tt in ctx.Dbo.TipoTransaccion on (t.IdTipoTransaccion = tt.IdTipoTransaccion)
      join c in ctx.Dbo.Contrato on (t.IdContrato = c.IdContrato)
      join el in ctx.Dbo.EntidadLegal on (c.IdContraparte = el.IdEntidadLegal)
      join p in ctx.Dbo.Punto on (t.IdPuntoEntrega = p.IdPunto)
      join elp in ctx.Dbo.EntidadLegal on (c.IdParte = elp.IdEntidadLegal)
      select
        { 
            TransaccionGasJoinRow.Id_TransaccionGas = t.IdTransaccionGas
            Parte = elp.Nombre
            Contraparte = el.Nombre
            ContractRef = c.Codigo
            IdParte = c.IdParte
            IdContraparte = c.IdContraparte
            PuntoEntrega = p.Codigo
            Id_IndicePrecio = t.IdIndicePrecio
            Id_PuntoEntrega = t.IdPuntoEntrega
            Id_TipoTransaccion = t.IdTipoTransaccion
            TipoTransaccionDescripcion = Some tt.Descripcion
            Id_TipoServicio = t.IdTipoServicio
            Id_Contrato = t.IdContrato
            Adder = t.Adder
            Fuel = t.Fuel
            TarifaTransporte = t.TarifaTransporte
            FormulaPrecio = t.FormulaPrecio
            PrecioFijo = t.PrecioFijo
            Volumen = t.Volumen
       }
    }
    |> Seq.map Mappings.transaccionGasJoinToDomain
    |> Seq.toList

  let loadTransaccionesTransporte () =
    query {
      for t in ctx.Dbo.TransaccionTransporte do
      join c in ctx.Dbo.Contrato on (t.IdContrato = c.IdContrato)
      join el in ctx.Dbo.EntidadLegal on (c.IdParte = el.IdEntidadLegal)
      join elcp in ctx.Dbo.EntidadLegal on (c.IdContraparte = elcp.IdEntidadLegal)
      join r in ctx.Dbo.Ruta on (t.IdRuta = r.IdRuta)
      join pr in ctx.Dbo.Punto on (r.IdPuntoRecepcion = pr.IdPunto)
      join pe in ctx.Dbo.Punto on (r.IdPuntoEntrega = pe.IdPunto)
      join elp in ctx.Dbo.EntidadLegal on (c.IdParte = elp.IdEntidadLegal)
      select
        { 
            TransaccionTransporteJoinRow.Id_TransaccionTransporte = t.IdTransaccionTransporte
            Contraparte = elcp.Nombre
            Parte = el.Nombre
            ContractRef = c.Codigo
            Id_Parte = c.IdParte
            Id_Contrato = c.IdContrato
            Id_Contraparte = c.IdContraparte
            PuntoEntrega = pe.Codigo
            PuntoRecepcion = pr.Codigo   
            Id_PuntoEntrega = r.IdPuntoEntrega
            Id_PuntoRecepcion = r.IdPuntoRecepcion
            FuelMode = t.FuelMode
            Id_Ruta = r.IdRuta
            Fuel = r.Fuel
            CMD = t.Cmd
            UsageRate = t.CargoUso
       }

    }
    |> Seq.map Mappings.transaccionTransporteJoinToDomain
    |> Seq.toList

  let loadCompraGas (diaGas:DateOnly) idFlowDetail =
     let dia = diaGas.ToDateTime(TimeOnly.MinValue)
     query {
      for cg in ctx.Dbo.CompraGas do
      where (cg.DiaGas = dia && cg.IdFlowDetail = idFlowDetail)
      select
            { 
                CompraGasRow.Id_CompraGas = cg.IdCompraGas
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
                Confirmado = cg.Confirmado
            }
     }
     |> Seq.map Mappings.compraGasToDomain
     |> Seq.toList


  // Convierte para un día Gas  un idFlowDetail  devuelve el consumo en MMBtu y el punto de entrega

  let loadConsumo (diaGas:DateOnly) idFlowDetail =
     
      let dia = diaGas.ToDateTime(TimeOnly.MinValue)
  
      let result = query {
              for cg in ctx.Dbo.Consumo do
              where (cg.DiaGas = dia && cg.IdFlowDetail.Value = idFlowDetail)
              select
                    (cg.IdPunto, cg.Demanda)
            }
      result


  let private entidadLegalById =
    lazy (ctx.Dbo.EntidadLegal |> Seq.map (fun e -> e.IdEntidadLegal, e) |> Map.ofSeq)


  let puntoCodigoById =
    lazy (ctx.Dbo.Punto |> Seq.map (fun p -> p.IdPunto, p.Codigo) |> Map.ofSeq)

  let  contratosById =
    lazy (loadContratos() |> List.map (fun c -> c.id, c) |> Map.ofList)


  let transaccionesGasById =
        lazy (loadTransaccionesGas() |> List.map (fun t -> t.id, t) |> Map.ofList)

  let transaccionesTransporteById =
        lazy (loadTransaccionesTransporte() |> List.map (fun t -> t.id, t) |> Map.ofList)

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


  let rutaById =
            lazy (ctx.Dbo.Ruta |> Seq.map (fun r -> r.IdRuta, r) |> Map.ofSeq)



  // (IdFlowMaster, Path) -> seq FlowDetail
  let  flowDetailsByMasterPath =
    lazy (
        ctx.Fm.FlowDetail
        |> Seq.groupBy (fun fd -> (fd.IdFlowMaster, fd.Path))
        |> Seq.map (fun (k, v) -> k, v)
        |> Map.ofSeq
    )

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
  let dEnt = entidadLegalById.Value
  let dPto = puntoCodigoById.Value
  let dCont = contratosById.Value
  let dTransGas = transaccionesGasById.Value
  let dTransTte = transaccionesTransporteById.Value

  let dRuta = rutaById.Value
