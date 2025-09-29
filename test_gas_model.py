"""
Tests básicos para el modelo de transacciones de gas.
Valida la funcionalidad principal del patrón de Composición.
"""

import unittest
from datetime import date
from decimal import Decimal
from gas_transaction_model import (
    DiaGas,
    ProductorUSA,
    TransportadorGas,
    CentralMexico,
    CadenaTransaccionGas,
    FabricaCadenaGas
)


class TestDiaGas(unittest.TestCase):
    """Tests para la clase DiaGas."""
    
    def test_crear_dia_gas(self):
        """Test creación básica de DiaGas."""
        dia = DiaGas(
            fecha=date(2024, 1, 1),
            cantidad_mcf=Decimal('1000'),
            precio_por_mcf=Decimal('3.50')
        )
        
        self.assertEqual(dia.fecha, date(2024, 1, 1))
        self.assertEqual(dia.cantidad_mcf, Decimal('1000'))
        self.assertEqual(dia.precio_por_mcf, Decimal('3.50'))
    
    def test_valor_total(self):
        """Test cálculo del valor total."""
        dia = DiaGas(
            fecha=date(2024, 1, 1),
            cantidad_mcf=Decimal('1000'),
            precio_por_mcf=Decimal('3.50')
        )
        
        self.assertEqual(dia.valor_total, Decimal('3500'))


class TestProductorUSA(unittest.TestCase):
    """Tests para la clase ProductorUSA."""
    
    def setUp(self):
        self.productor = ProductorUSA(
            nombre="Test Producer",
            ubicacion="Texas",
            capacidad_diaria_mcf=Decimal('100000')
        )
    
    def test_procesar_dentro_capacidad(self):
        """Test procesamiento dentro de la capacidad."""
        dia_gas = DiaGas(
            fecha=date(2024, 1, 1),
            cantidad_mcf=Decimal('50000'),
            precio_por_mcf=Decimal('3.00')
        )
        
        resultado = self.productor.procesar(dia_gas)
        
        self.assertEqual(resultado.cantidad_mcf, Decimal('50000'))
        self.assertEqual(resultado.precio_por_mcf, Decimal('3.00'))
    
    def test_procesar_excede_capacidad(self):
        """Test procesamiento que excede la capacidad."""
        dia_gas = DiaGas(
            fecha=date(2024, 1, 1),
            cantidad_mcf=Decimal('150000'),
            precio_por_mcf=Decimal('3.00')
        )
        
        resultado = self.productor.procesar(dia_gas)
        
        # Debe limitarse a la capacidad del productor
        self.assertEqual(resultado.cantidad_mcf, Decimal('100000'))
        self.assertEqual(resultado.precio_por_mcf, Decimal('3.00'))


class TestTransportadorGas(unittest.TestCase):
    """Tests para la clase TransportadorGas."""
    
    def setUp(self):
        self.transportador = TransportadorGas(
            nombre="Test Transport",
            ruta="Test Route",
            factor_perdida=Decimal('0.02'),  # 2% pérdida
            costo_transporte_por_mcf=Decimal('0.50')
        )
    
    def test_procesar_con_perdidas(self):
        """Test procesamiento con pérdidas y costos."""
        dia_gas = DiaGas(
            fecha=date(2024, 1, 1),
            cantidad_mcf=Decimal('100000'),
            precio_por_mcf=Decimal('3.00')
        )
        
        resultado = self.transportador.procesar(dia_gas)
        
        # Cantidad después de 2% de pérdida
        self.assertEqual(resultado.cantidad_mcf, Decimal('98000'))
        # Precio aumenta en $0.50
        self.assertEqual(resultado.precio_por_mcf, Decimal('3.50'))


class TestCentralMexico(unittest.TestCase):
    """Tests para la clase CentralMexico."""
    
    def setUp(self):
        self.central = CentralMexico(
            nombre="Test Central",
            ubicacion="Test Location",
            demanda_diaria_mcf=Decimal('80000')
        )
    
    def test_procesar_dentro_demanda(self):
        """Test procesamiento dentro de la demanda."""
        dia_gas = DiaGas(
            fecha=date(2024, 1, 1),
            cantidad_mcf=Decimal('50000'),
            precio_por_mcf=Decimal('4.00')
        )
        
        resultado = self.central.procesar(dia_gas)
        
        self.assertEqual(resultado.cantidad_mcf, Decimal('50000'))
        self.assertEqual(resultado.precio_por_mcf, Decimal('4.00'))
    
    def test_procesar_excede_demanda(self):
        """Test procesamiento que excede la demanda."""
        dia_gas = DiaGas(
            fecha=date(2024, 1, 1),
            cantidad_mcf=Decimal('100000'),
            precio_por_mcf=Decimal('4.00')
        )
        
        resultado = self.central.procesar(dia_gas)
        
        # Debe limitarse a la demanda de la central
        self.assertEqual(resultado.cantidad_mcf, Decimal('80000'))
        self.assertEqual(resultado.precio_por_mcf, Decimal('4.00'))


class TestCadenaTransaccionGas(unittest.TestCase):
    """Tests para la clase CadenaTransaccionGas."""
    
    def setUp(self):
        self.cadena = CadenaTransaccionGas()
        
        # Crear operaciones de prueba
        self.productor = ProductorUSA("Test Producer", "Texas", Decimal('100000'))
        self.transportador = TransportadorGas("Test Transport", "Test Route", 
                                            Decimal('0.01'), Decimal('0.25'))
        self.central = CentralMexico("Test Central", "Mexico", Decimal('90000'))
    
    def test_agregar_operaciones(self):
        """Test agregar operaciones a la cadena."""
        cadena = (self.cadena
                 .agregar_operacion(self.productor)
                 .agregar_operacion(self.transportador)
                 .agregar_operacion(self.central))
        
        self.assertEqual(len(cadena.operaciones), 3)
    
    def test_procesar_transaccion_completa(self):
        """Test procesamiento de transacción completa."""
        cadena = (self.cadena
                 .agregar_operacion(self.productor)
                 .agregar_operacion(self.transportador)
                 .agregar_operacion(self.central))
        
        dia_gas_inicial = DiaGas(
            fecha=date(2024, 1, 1),
            cantidad_mcf=Decimal('95000'),
            precio_por_mcf=Decimal('3.00')
        )
        
        resultados = cadena.procesar_transaccion(dia_gas_inicial)
        
        self.assertEqual(len(resultados), 3)
        
        # Verificar resultado final
        gas_final = resultados[-1]
        
        # Después del productor: 95000 MCF
        # Después del transportador: 95000 * 0.99 = 94050 MCF, precio = 3.25
        # Después de la central: 90000 MCF (limitado por demanda)
        self.assertEqual(gas_final.cantidad_mcf, Decimal('90000'))
        self.assertEqual(gas_final.precio_por_mcf, Decimal('3.25'))
    
    def test_calcular_eficiencia(self):
        """Test cálculo de eficiencia."""
        cadena = (self.cadena
                 .agregar_operacion(self.productor)
                 .agregar_operacion(self.transportador))
        
        dia_gas_inicial = DiaGas(
            fecha=date(2024, 1, 1),
            cantidad_mcf=Decimal('100000'),
            precio_por_mcf=Decimal('3.00')
        )
        
        eficiencia = cadena.calcular_eficiencia_total(dia_gas_inicial)
        
        self.assertEqual(eficiencia['cantidad_inicial_mcf'], Decimal('100000'))
        self.assertEqual(eficiencia['cantidad_final_mcf'], Decimal('99000'))  # 99% después de transporte
        self.assertEqual(eficiencia['eficiencia_cantidad'], Decimal('99.0'))


class TestFabricaCadenaGas(unittest.TestCase):
    """Tests para la clase FabricaCadenaGas."""
    
    def test_crear_cadena_usa_a_mexico(self):
        """Test creación de cadena predefinida."""
        cadena = FabricaCadenaGas.crear_cadena_usa_a_mexico()
        
        self.assertEqual(len(cadena.operaciones), 4)
        
        # Verificar tipos de operaciones
        self.assertIsInstance(cadena.operaciones[0], ProductorUSA)
        self.assertIsInstance(cadena.operaciones[1], TransportadorGas)
        self.assertIsInstance(cadena.operaciones[2], TransportadorGas)
        self.assertIsInstance(cadena.operaciones[3], CentralMexico)
    
    def test_cadena_personalizada(self):
        """Test cadena con nombres personalizados."""
        cadena = FabricaCadenaGas.crear_cadena_usa_a_mexico(
            productor_nombre="Shell",
            central_nombre="CFE Test"
        )
        
        self.assertEqual(cadena.operaciones[0].nombre, "Shell")
        self.assertEqual(cadena.operaciones[3].nombre, "CFE Test")


if __name__ == '__main__':
    unittest.main(verbosity=2)