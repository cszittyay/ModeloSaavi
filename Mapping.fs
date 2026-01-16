

namespace Gnx.Persistence

open System
open Gnx.Domain
open Tipos

module Mappings =
  

  let contratoToDomain (r:ContratoRow) : Contrato =
    { id = r.Id_Contrato
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
    { Id_Contrato = c.id
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



  let transaccionGasJoinToDomain (r: TransaccionGasJoinRow) : TransaccionGas =
      { id = r.Id_TransaccionGas
        tipo =
          match r.TipoTransaccionDescripcion with
          | Some d -> TipoTransaccion.ofDescripcion d
          | None -> TipoTransaccion.Otro (string r.Id_TipoTransaccion)
        contratRef = r.ContractRef
        idContrato = r.Id_Contrato
        idBuyer = r.IdParte
        idSeller = r.IdContraparte
        buyer = r.Parte
        seller = r.Contraparte
        puntoEntrega = r.PuntoEntrega
        idPuntoEntrega = r.Id_PuntoEntrega
        adder = r.Adder
        }

  let transaccionTransporteJoinToDomain (r: TransaccionTransporteJoinRow) : TransaccionTransporte =
      { id = r.Id_TransaccionTransporte
        contratRef = r.ContractRef
        idContrato = r.Id_Contrato
        idBuyer = r.Id_Parte
        idSeller = r.Id_Contraparte
        buyer = r.Parte
        seller = r.Contraparte
        puntoEntrega = r.PuntoEntrega
        puntoRecepcion = r.PuntoRecepcion
        idPuntoEntrega = r.Id_PuntoEntrega
        idPuntoRecepcion = r.Id_PuntoRecepcion
        fuelMode = r.FuelMode
        idRuta = r.Id_Ruta
        cmd = r.CMD* 1.0m<MMBTU>
        usageRate = r.UsageRate * 1.0m<USD/MMBTU>
        fuel = r.Fuel
        }


  let compraGasToDomain (r:CompraGasRow) : CompraGas =
    { id = CompraGasId r.Id_CompraGas
      diaGas = r.DiaGas
      idTransaccion =  r.Id_Transaccion
      idFlowDetail = r.Id_FlowDetail
      buyBack = r.BuyBack
      idPuntoEntrega = r.Id_PuntoEntrega
      temporalidad = r.Temporalidad
      idIndicePrecio = r.Id_IndicePrecio
      adder = r.Adder
      precio = r.Precio
      nominado = r.Nominado
      confirmado = r.Confirmado
      }


