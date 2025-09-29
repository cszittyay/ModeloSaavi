"""
Modelo de transacciones encadenadas de gas desde productores en USA hasta centrales en México.
Implementa el patrón de Composición para modelar la secuencia de operaciones.
"""

from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import List, Optional
from datetime import date
from decimal import Decimal


@dataclass
class DiaGas:
    """Representa la cantidad de gas en un día específico."""
    fecha: date
    cantidad_mcf: Decimal  # Miles de pies cúbicos
    precio_por_mcf: Decimal
    
    @property
    def valor_total(self) -> Decimal:
        """Calcula el valor total del gas para este día."""
        return self.cantidad_mcf * self.precio_por_mcf


class OperacionTransaccion(ABC):
    """Interfaz base para todas las operaciones en la cadena de transacciones."""
    
    @abstractmethod
    def procesar(self, dia_gas: DiaGas) -> DiaGas:
        """Procesa una cantidad diaria de gas y retorna el resultado."""
        pass
    
    @abstractmethod
    def get_descripcion(self) -> str:
        """Retorna una descripción de la operación."""
        pass


class ProductorUSA(OperacionTransaccion):
    """Representa un productor de gas en USA."""
    
    def __init__(self, nombre: str, ubicacion: str, capacidad_diaria_mcf: Decimal):
        self.nombre = nombre
        self.ubicacion = ubicacion
        self.capacidad_diaria_mcf = capacidad_diaria_mcf
    
    def procesar(self, dia_gas: DiaGas) -> DiaGas:
        """Produce gas según la capacidad y demanda."""
        cantidad_producida = min(dia_gas.cantidad_mcf, self.capacidad_diaria_mcf)
        return DiaGas(
            fecha=dia_gas.fecha,
            cantidad_mcf=cantidad_producida,
            precio_por_mcf=dia_gas.precio_por_mcf
        )
    
    def get_descripcion(self) -> str:
        return f"Productor {self.nombre} en {self.ubicacion}, USA"


class TransportadorGas(OperacionTransaccion):
    """Representa el transporte de gas con pérdidas y costos."""
    
    def __init__(self, nombre: str, ruta: str, factor_perdida: Decimal, costo_transporte_por_mcf: Decimal):
        self.nombre = nombre
        self.ruta = ruta
        self.factor_perdida = factor_perdida  # Porcentaje de pérdida (0.01 = 1%)
        self.costo_transporte_por_mcf = costo_transporte_por_mcf
    
    def procesar(self, dia_gas: DiaGas) -> DiaGas:
        """Transporta gas aplicando pérdidas y costos de transporte."""
        cantidad_final = dia_gas.cantidad_mcf * (Decimal('1') - self.factor_perdida)
        precio_final = dia_gas.precio_por_mcf + self.costo_transporte_por_mcf
        
        return DiaGas(
            fecha=dia_gas.fecha,
            cantidad_mcf=cantidad_final,
            precio_por_mcf=precio_final
        )
    
    def get_descripcion(self) -> str:
        return f"Transporte {self.nombre} - Ruta: {self.ruta}"


class CentralMexico(OperacionTransaccion):
    """Representa una central en México que recibe el gas."""
    
    def __init__(self, nombre: str, ubicacion: str, demanda_diaria_mcf: Decimal):
        self.nombre = nombre
        self.ubicacion = ubicacion
        self.demanda_diaria_mcf = demanda_diaria_mcf
    
    def procesar(self, dia_gas: DiaGas) -> DiaGas:
        """Recibe gas hasta su demanda máxima."""
        cantidad_recibida = min(dia_gas.cantidad_mcf, self.demanda_diaria_mcf)
        return DiaGas(
            fecha=dia_gas.fecha,
            cantidad_mcf=cantidad_recibida,
            precio_por_mcf=dia_gas.precio_por_mcf
        )
    
    def get_descripcion(self) -> str:
        return f"Central {self.nombre} en {self.ubicacion}, México"


class CadenaTransaccionGas:
    """Implementa el patrón de Composición para secuenciar operaciones de gas."""
    
    def __init__(self):
        self.operaciones: List[OperacionTransaccion] = []
    
    def agregar_operacion(self, operacion: OperacionTransaccion) -> 'CadenaTransaccionGas':
        """Agrega una operación a la cadena (patrón Builder)."""
        self.operaciones.append(operacion)
        return self
    
    def procesar_transaccion(self, dia_gas_inicial: DiaGas) -> List[DiaGas]:
        """Procesa el gas a través de toda la cadena de operaciones."""
        resultados = []
        gas_actual = dia_gas_inicial
        
        for operacion in self.operaciones:
            gas_actual = operacion.procesar(gas_actual)
            resultados.append(gas_actual)
        
        return resultados
    
    def get_resumen_cadena(self) -> List[str]:
        """Retorna un resumen de todas las operaciones en la cadena."""
        return [operacion.get_descripcion() for operacion in self.operaciones]
    
    def calcular_eficiencia_total(self, dia_gas_inicial: DiaGas) -> dict:
        """Calcula métricas de eficiencia de la cadena."""
        resultados = self.procesar_transaccion(dia_gas_inicial)
        
        if not resultados:
            return {}
        
        gas_final = resultados[-1]
        
        return {
            'cantidad_inicial_mcf': dia_gas_inicial.cantidad_mcf,
            'cantidad_final_mcf': gas_final.cantidad_mcf,
            'eficiencia_cantidad': (gas_final.cantidad_mcf / dia_gas_inicial.cantidad_mcf * 100),
            'precio_inicial_por_mcf': dia_gas_inicial.precio_por_mcf,
            'precio_final_por_mcf': gas_final.precio_por_mcf,
            'incremento_precio': ((gas_final.precio_por_mcf - dia_gas_inicial.precio_por_mcf) / dia_gas_inicial.precio_por_mcf * 100),
            'valor_inicial_total': dia_gas_inicial.valor_total,
            'valor_final_total': gas_final.valor_total
        }


# Clase de utilidad para crear cadenas de transacción predefinidas
class FabricaCadenaGas:
    """Factory para crear cadenas de transacción comunes."""
    
    @staticmethod
    def crear_cadena_usa_a_mexico(
        productor_nombre: str = "ExxonMobil",
        central_nombre: str = "CFE Tuxpan"
    ) -> CadenaTransaccionGas:
        """Crea una cadena típica de USA a México."""
        return (CadenaTransaccionGas()
                .agregar_operacion(ProductorUSA(
                    nombre=productor_nombre,
                    ubicacion="Texas",
                    capacidad_diaria_mcf=Decimal('1000000')
                ))
                .agregar_operacion(TransportadorGas(
                    nombre="Pipeline Internacional",
                    ruta="Texas-Frontera México",
                    factor_perdida=Decimal('0.02'),  # 2% de pérdida
                    costo_transporte_por_mcf=Decimal('0.50')
                ))
                .agregar_operacion(TransportadorGas(
                    nombre="Distribuidora México",
                    ruta="Frontera-Central",
                    factor_perdida=Decimal('0.01'),  # 1% de pérdida
                    costo_transporte_por_mcf=Decimal('0.30')
                ))
                .agregar_operacion(CentralMexico(
                    nombre=central_nombre,
                    ubicacion="Veracruz",
                    demanda_diaria_mcf=Decimal('800000')
                )))