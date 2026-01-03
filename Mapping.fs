

namespace Gnx.Persistence

open System
open Gnx.Domain

module Mappings =

  let contratoToDomain (r:ContratoRow) : Contrato =
    { id = ContratoId r.Id_Contrato
      tipo =
        match r.NemonicoTipoContrato with
        | Some nem -> TipoContrato.ofNemonico nem
        | None ->
            // si no hiciste join a TipoContrato, podés mapear por Id_TipoContrato
            // dejando un placeholder; ideal es traer nemonico siempre.
            TipoContrato.Otro (string r.Id_TipoContrato)
      idParte = r.Id_Parte
      idContraparte = r.Id_Contraparte
      vigenciaDesde = r.VigenciaDesde
      vigenciaHasta = r.VigenciaHasta
      estadoId = EstadoContratoId r.Id_EstadoContrato
      leyAplicable = r.LeyAplicable
      fechaDePago = r.FechaDePago
      fechaDeFirma = r.FechaDeFirma
      idMoneda = r.Id_Moneda
      observaciones = r.Observaciones
      codigo = r.Codigo }

  let contratoToRow (c:Contrato) : ContratoRow =
    { Id_Contrato =
        let (ContratoId v) = c.id in v
      Id_TipoContrato = 0uy // si persistís por id, resolvelo desde catálogo
      NemonicoTipoContrato = Some (TipoContrato.toNemonico c.tipo)
      Id_Parte = c.idParte
      Id_Contraparte = c.idContraparte
      VigenciaDesde = c.vigenciaDesde
      VigenciaHasta = c.vigenciaHasta
      Id_EstadoContrato = let (EstadoContratoId v) = c.estadoId in v
      LeyAplicable = c.leyAplicable
      FechaDePago = c.fechaDePago
      FechaDeFirma = c.fechaDeFirma
      Id_Moneda = c.idMoneda
      Observaciones = c.observaciones
      Codigo = c.codigo }



  let transaccionJoinToDomain (r: TransaccionJoinRow) : Transaccion =
      { id = TransaccionId r.Id_Transaccion
        tipo =
          match r.TipoTransaccionDescripcion with
          | Some d -> TipoTransaccion.ofDescripcion d
          | None -> TipoTransaccion.Otro (string r.Id_TipoTransaccion)
        idContrato = ContratoId r.Id_Contrato
        idPuntoEntrega = r.Id_PuntoEntrega
        idTipoServicio = r.Id_TipoServicio
        idIndicePrecio = r.Id_IndicePrecio
        adder = r.Adder
        fuel = r.Fuel
        tarifaTransporte = r.TarifaTransporte
        formulaPrecio = r.FormulaPrecio
        precioFijo = r.PrecioFijo
        volumen = r.Volumen
        observaciones = r.Observaciones
        vigenciaDesde = r.VigenciaDesde
        vigenciaHasta = r.VigenciaHasta
        idMonedaPrecioFijo = r.Id_MonedaPrecioFijo
        idUnidadPrecioEnergiaAdder = r.Id_UnidadPrecioEnergiaAdder
        idUnidadEnergiaVolumen = r.Id_UnidadEnergiaVolumen }

  let compraGasToDomain (r:CompraGasRow) : CompraGas =
    { id = CompraGasId r.Id_CompraGas
      diaGas = r.DiaGas
      nominado = r.Nominado
      confirmado = r.Confirmado
      asignado = r.Asignado
      idPuntoEntrega = r.Id_PuntoEntrega
      idRuta = r.Id_Ruta |> Option.map RutaId
      idTransaccion =  TransaccionId r.Id_Transaccion |> Some
      idCompraSpot = r.Id_CompraSpot |> Option.map CompraSpotId
      temporalidad = r.Temporalidad
      idVentaGas = r.Id_VentaGas }


