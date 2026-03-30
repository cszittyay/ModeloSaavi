namespace ModeloSaavi.Infrastructure

open System
open DbContext


type StableCatalogs =
    { PuntosById            : Map<int, PuntoEntity>
      ClientesById          : Map<int, ClienteEntity>
      EntidadLegalById      : Map<int, EntidadLegalEntity>
      TiposServicioById     : Map<int, TipoServicioEntity>
      TiposContratoById     : Map<int, TipoContratoEntity>
      GasoductosById        : Map<int, GasoductoEntity >
      TiposTransaccionById  : Map<int, TipoTransaccionEntity>
      MonedasById           : Map<int, MonedaEntity> }

type RunSnapshot =
    { ContratosById               : Map<int, ContratoEntity>
      TransaccionesGasById        : Map<int, TransaccionGasEntity>
      TransaccionesTransporteById : Map<int, TransaccionTransporteEntity>
      ComprasGas                  : CompraGasEntity list
      VentasGas                   : VentaGasEntity list
      Consumos                    : ConsumoEntity list }



type LoadContext =
    { Catalogs : StableCatalogs
      Run      : RunSnapshot }


[<RequireQualifiedAccess>]
module Lookup =

    let byId (getId: 'T -> int) (items: seq<'T>) : Map<int,'T> =
        items |> Seq.map (fun x -> getId x, x) |> Map.ofSeq

    let inline get (id: int) (map: Map<int,'T>) : 'T =
        map[id]

    let inline tryGet (id: int) (map: Map<int,'T>) : 'T option =
        Map.tryFind id map

    let inline value (selector: 'T -> 'V) (id: int) (map: Map<int,'T>) : 'V =
        map[id] |> selector

[<RequireQualifiedAccess>]
module StableCatalogs =
    
    let load (ctx: DataContext) : StableCatalogs =
        { PuntosById =
            ctx.Dbo.Punto
            |> Seq.toList
            |> Lookup.byId (fun x -> x.IdPunto)

          ClientesById =
            ctx.Dbo.Cliente
            |> Seq.toList
            |> Lookup.byId (fun x -> x.IdCliente)

          EntidadLegalById =
            ctx.Dbo.EntidadLegal
            |> Seq.toList
            |> Lookup.byId (fun x -> x.IdEntidadLegal)

          TiposServicioById =
            ctx.Dbo.TipoServicio
            |> Seq.toList
            |> Lookup.byId (fun x -> x.IdTipoServicio)

          TiposContratoById =
            ctx.Dbo.TipoContrato
            |> Seq.toList
            |> Lookup.byId (fun x -> int x.IdTipoContrato)

          TiposTransaccionById =
            ctx.Dbo.TipoTransaccion
            |> Seq.toList
            |> Lookup.byId (fun x -> x.IdTipoTransaccion)

          GasoductosById =
            ctx.Dbo.Gasoducto
            |> Seq.toList
            |> Lookup.byId (fun x -> x.IdGasoducto)

          MonedasById =
            ctx.Dbo.Moneda
            |> Seq.toList
            |> Lookup.byId (fun x -> x.IdMoneda) }





[<RequireQualifiedAccess>]
module RunSnapshot =
    
    open DbContext
    open System

    let load
        (ctx: DataContext)
        (diaGas: DateOnly)
        : RunSnapshot =

        let dia = diaGas.ToDateTime(TimeOnly.MinValue)

        { ContratosById =
            ctx.Dbo.Contrato
            |> Seq.toList
            |> Lookup.byId (fun x -> x.IdContrato)

          TransaccionesGasById =
            ctx.Dbo.TransaccionGas
            |> Seq.toList
            |> Lookup.byId (fun x -> x.IdTransaccionGas)

          TransaccionesTransporteById =
            ctx.Dbo.TransaccionTransporte
            |> Seq.toList
            |> Lookup.byId (fun x -> x.IdTransaccionTransporte)

          ComprasGas =
            query {
                for x in ctx.Dbo.CompraGas do
                where (x.DiaGas = dia)
                select x
            } |> Seq.toList

          VentasGas =
            query {
                for x in ctx.Dbo.VentaGas do
                where (x.DiaGas = dia)
                select x
            } |> Seq.toList

          Consumos =
            query {
                for x in ctx.Dbo.Consumo do
                where (x.DiaGas = dia)
                select x
            } |> Seq.toList }




[<RequireQualifiedAccess>]
module StableCatalogCache =

    let mutable private cache : StableCatalogs option = None
    let private syncRoot = obj()

    let getOrLoad (ctxFactory: unit -> DataContext) : StableCatalogs =
        lock syncRoot (fun () ->
            match cache with
            | Some catalogs -> catalogs
            | None ->
                let dc = ctxFactory()
                let catalogs = StableCatalogs.load dc
                cache <- Some catalogs
                catalogs)

    let clear () =
        lock syncRoot (fun () ->
            cache <- None)

    let reload (ctxFactory: unit -> DataContext) : StableCatalogs =
        lock syncRoot (fun () ->
            let ctx = ctxFactory()
            let catalogs = StableCatalogs.load ctx
            cache <- Some catalogs
            catalogs)

[<RequireQualifiedAccess>]
module LoadContext =

    let create
        (ctxFactory: unit -> DataContext)
        (diaGas: DateOnly)
        : LoadContext =

        let catalogs = StableCatalogCache.getOrLoad ctxFactory

        let ctx = ctxFactory()
        let run = RunSnapshot.load ctx diaGas

        { Catalogs = catalogs
          Run = run }