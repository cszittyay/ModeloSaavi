"""
Ejemplo de uso del modelo de transacciones de gas.
Demuestra cómo usar el patrón de Composición para modelar 
la cadena de suministro de gas desde USA hasta México.
"""

from datetime import date
from decimal import Decimal
from gas_transaction_model import (
    DiaGas, 
    CadenaTransaccionGas, 
    ProductorUSA, 
    TransportadorGas, 
    CentralMexico,
    FabricaCadenaGas
)


def ejemplo_basico():
    """Ejemplo básico de uso del modelo."""
    print("=== EJEMPLO BÁSICO: CADENA DE TRANSACCIÓN DE GAS ===\n")
    
    # Crear una demanda inicial de gas
    gas_inicial = DiaGas(
        fecha=date(2024, 1, 15),
        cantidad_mcf=Decimal('500000'),  # 500,000 MCF
        precio_por_mcf=Decimal('3.50')   # $3.50 por MCF
    )
    
    print(f"Gas inicial solicitado:")
    print(f"  Fecha: {gas_inicial.fecha}")
    print(f"  Cantidad: {gas_inicial.cantidad_mcf:,} MCF")
    print(f"  Precio: ${gas_inicial.precio_por_mcf} por MCF")
    print(f"  Valor total: ${gas_inicial.valor_total:,.2f}\n")
    
    # Crear cadena de transacción usando el factory
    cadena = FabricaCadenaGas.crear_cadena_usa_a_mexico()
    
    print("Cadena de operaciones:")
    for i, descripcion in enumerate(cadena.get_resumen_cadena(), 1):
        print(f"  {i}. {descripcion}")
    print()
    
    # Procesar la transacción
    resultados = cadena.procesar_transaccion(gas_inicial)
    
    print("Resultados paso a paso:")
    operaciones = cadena.get_resumen_cadena()
    for i, (gas_resultado, operacion) in enumerate(zip(resultados, operaciones)):
        print(f"  Después de {operacion}:")
        print(f"    Cantidad: {gas_resultado.cantidad_mcf:,.0f} MCF")
        print(f"    Precio: ${gas_resultado.precio_por_mcf:.2f} por MCF")
        print(f"    Valor: ${gas_resultado.valor_total:,.2f}")
        print()
    
    # Calcular métricas de eficiencia
    eficiencia = cadena.calcular_eficiencia_total(gas_inicial)
    print("=== RESUMEN DE EFICIENCIA ===")
    print(f"Eficiencia de cantidad: {eficiencia['eficiencia_cantidad']:.1f}%")
    print(f"Incremento de precio: {eficiencia['incremento_precio']:.1f}%")
    print(f"Valor inicial: ${eficiencia['valor_inicial_total']:,.2f}")
    print(f"Valor final: ${eficiencia['valor_final_total']:,.2f}")


def ejemplo_cadena_personalizada():
    """Ejemplo de creación de una cadena personalizada."""
    print("\n\n=== EJEMPLO PERSONALIZADO: CADENA ESPECÍFICA ===\n")
    
    # Crear operaciones específicas
    productor = ProductorUSA(
        nombre="Chevron",
        ubicacion="Louisiana",
        capacidad_diaria_mcf=Decimal('750000')
    )
    
    transporte_principal = TransportadorGas(
        nombre="Kinder Morgan",
        ruta="Louisiana-Texas-Frontera",
        factor_perdida=Decimal('0.025'),  # 2.5% pérdida
        costo_transporte_por_mcf=Decimal('0.75')
    )
    
    transporte_mexico = TransportadorGas(
        nombre="Sistrangas",
        ruta="Frontera-Bajío",
        factor_perdida=Decimal('0.015'),  # 1.5% pérdida
        costo_transporte_por_mcf=Decimal('0.40')
    )
    
    central = CentralMexico(
        nombre="CFE Salamanca",
        ubicacion="Guanajuato",
        demanda_diaria_mcf=Decimal('600000')
    )
    
    # Crear cadena personalizada
    cadena_personalizada = (CadenaTransaccionGas()
                           .agregar_operacion(productor)
                           .agregar_operacion(transporte_principal)
                           .agregar_operacion(transporte_mexico)
                           .agregar_operacion(central))
    
    # Gas de prueba
    gas_prueba = DiaGas(
        fecha=date(2024, 2, 1),
        cantidad_mcf=Decimal('700000'),
        precio_por_mcf=Decimal('3.25')
    )
    
    print(f"Solicitud de gas:")
    print(f"  Cantidad: {gas_prueba.cantidad_mcf:,} MCF")
    print(f"  Precio inicial: ${gas_prueba.precio_por_mcf} por MCF\n")
    
    print("Cadena personalizada:")
    for i, desc in enumerate(cadena_personalizada.get_resumen_cadena(), 1):
        print(f"  {i}. {desc}")
    print()
    
    # Procesar
    resultados = cadena_personalizada.procesar_transaccion(gas_prueba)
    gas_final = resultados[-1]
    
    print("Resultado final:")
    print(f"  Gas entregado: {gas_final.cantidad_mcf:,.0f} MCF")
    print(f"  Precio final: ${gas_final.precio_por_mcf:.2f} por MCF")
    print(f"  Costo total: ${gas_final.valor_total:,.2f}")
    
    # Eficiencia
    eficiencia = cadena_personalizada.calcular_eficiencia_total(gas_prueba)
    print(f"  Eficiencia: {eficiencia['eficiencia_cantidad']:.1f}%")


def ejemplo_multiples_dias():
    """Ejemplo de procesamiento de múltiples días."""
    print("\n\n=== EJEMPLO: PROCESAMIENTO DE MÚLTIPLES DÍAS ===\n")
    
    cadena = FabricaCadenaGas.crear_cadena_usa_a_mexico(
        productor_nombre="Shell",
        central_nombre="CFE Manzanillo"
    )
    
    # Simular una semana de operaciones
    fechas = [date(2024, 3, i) for i in range(1, 8)]
    cantidades = [Decimal('450000'), Decimal('520000'), Decimal('480000'), 
                  Decimal('610000'), Decimal('590000'), Decimal('380000'), Decimal('420000')]
    precios = [Decimal('3.40'), Decimal('3.45'), Decimal('3.38'), 
               Decimal('3.52'), Decimal('3.48'), Decimal('3.42'), Decimal('3.39')]
    
    print("Procesamiento semanal:")
    print("Día      | Solicitado | Entregado  | Precio Final | Eficiencia")
    print("-" * 65)
    
    total_solicitado = Decimal('0')
    total_entregado = Decimal('0')
    
    for fecha, cantidad, precio in zip(fechas, cantidades, precios):
        gas_dia = DiaGas(fecha=fecha, cantidad_mcf=cantidad, precio_por_mcf=precio)
        resultados = cadena.procesar_transaccion(gas_dia)
        gas_final = resultados[-1]
        
        eficiencia = (gas_final.cantidad_mcf / gas_dia.cantidad_mcf * 100)
        
        print(f"{fecha.strftime('%m/%d')}     | {cantidad:>8,} | {gas_final.cantidad_mcf:>8,.0f} | "
              f"${gas_final.precio_por_mcf:>7.2f}   | {eficiencia:>6.1f}%")
        
        total_solicitado += cantidad
        total_entregado += gas_final.cantidad_mcf
    
    eficiencia_total = (total_entregado / total_solicitado * 100)
    print("-" * 65)
    print(f"TOTAL    | {total_solicitado:>8,} | {total_entregado:>8,.0f} | "
          f"{'':>10} | {eficiencia_total:>6.1f}%")


if __name__ == "__main__":
    ejemplo_basico()
    ejemplo_cadena_personalizada()
    ejemplo_multiples_dias()
    
    print("\n" + "="*60)
    print("MODELO COMPLETADO")
    print("El modelo implementa el patrón de Composición para")
    print("cadenas de transacciones de gas desde USA a México.")
    print("="*60)