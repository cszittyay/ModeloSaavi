namespace Gnx.Persistence

open System
open FSharp.Data.Sql
open DbContext
open Gnx.Domain
/// Ejemplo de “mapping” desde SqlProvider a Rows.
/// Ajustá connection string y el provider a tu entorno.
module SqlProviderExample =

  

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
  let loadTransacciones () =
    query {
      for t in ctx.Dbo.Transaccion do
      join tt in ctx.Dbo.TipoTransaccion on (t.IdTipoTransaccion = tt.IdTipoTransaccion)
      select
        { 
        TransaccionJoinRow.Id_Transaccion = t.IdTransaccion
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
        Observaciones = t.Observaciones
        VigenciaDesde = DateOnly.FromDateTime t.VigenciaDesde
        VigenciaHasta = DateOnly.FromDateTime t.VigenciaHasta
        Id_MonedaPrecioFijo = t.IdMonedaPrecioFijo
        Id_UnidadPrecioEnergiaAdder = t.IdUnidadPrecioEnergiaAdder
        Id_UnidadEnergiaVolumen = t.IdUnidadEnergiaVolumen                                                                                                   }

    }
    |> Seq.map Mappings.transaccionJoinToDomain
    |> Seq.toList


