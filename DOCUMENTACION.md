# Modelo de Transacciones de Gas USA-México

## Descripción

Este proyecto implementa un modelo de transacciones encadenadas para el suministro de gas natural desde productores en Estados Unidos hasta centrales eléctricas en México. Utiliza el **patrón de Composición** para modelar la secuencia de operaciones que participan en la cadena de suministro.

## Arquitectura del Sistema

### Patrón de Composición

El modelo implementa el patrón de Composición (Composition Pattern) para crear una cadena de operaciones que procesan gas natural de manera secuencial. Cada operación en la cadena:

1. Recibe una cantidad de gas (DiaGas)
2. Aplica sus reglas de negocio específicas
3. Pasa el resultado a la siguiente operación

### Componentes Principales

#### 1. `DiaGas`
Representa la cantidad diaria de gas natural con:
- **fecha**: Fecha de la transacción
- **cantidad_mcf**: Cantidad en miles de pies cúbicos (MCF)
- **precio_por_mcf**: Precio por MCF
- **valor_total**: Valor total calculado

#### 2. `OperacionTransaccion` (Interfaz Abstracta)
Define la interfaz común para todas las operaciones:
- `procesar(dia_gas)`: Procesa el gas y retorna el resultado
- `get_descripcion()`: Retorna descripción de la operación

#### 3. Operaciones Concretas

##### `ProductorUSA`
- Modela productores de gas en Estados Unidos
- Limita la producción según capacidad diaria
- Parámetros: nombre, ubicación, capacidad_diaria_mcf

##### `TransportadorGas`
- Modela el transporte de gas con pérdidas y costos
- Aplica factor de pérdida durante el transporte
- Agrega costos de transporte al precio
- Parámetros: nombre, ruta, factor_perdida, costo_transporte_por_mcf

##### `CentralMexico`
- Modela centrales eléctricas en México (clientes finales)
- Limita la recepción según demanda diaria
- Parámetros: nombre, ubicación, demanda_diaria_mcf

#### 4. `CadenaTransaccionGas`
Implementa el patrón de Composición:
- Mantiene lista de operaciones secuenciales
- Procesa gas a través de toda la cadena
- Calcula métricas de eficiencia
- Soporta patrón Builder para construcción fluida

#### 5. `FabricaCadenaGas`
Factory para crear cadenas predefinidas comunes.

## Flujo de Operación

```
Gas Inicial → Productor USA → Transporte Internacional → Transporte Nacional → Central México → Gas Final
```

### Ejemplo de Flujo:

1. **Solicitud inicial**: 500,000 MCF a $3.50/MCF
2. **Productor ExxonMobil**: Capacidad 1,000,000 MCF → Produce 500,000 MCF
3. **Transporte Internacional**: 2% pérdida + $0.50/MCF → 490,000 MCF a $4.00/MCF
4. **Transporte Nacional**: 1% pérdida + $0.30/MCF → 485,100 MCF a $4.30/MCF
5. **Central CFE**: Demanda 800,000 MCF → Recibe 485,100 MCF a $4.30/MCF

## Uso del Sistema

### Ejemplo Básico

```python
from gas_transaction_model import DiaGas, FabricaCadenaGas
from datetime import date
from decimal import Decimal

# Crear solicitud de gas
gas_inicial = DiaGas(
    fecha=date(2024, 1, 15),
    cantidad_mcf=Decimal('500000'),
    precio_por_mcf=Decimal('3.50')
)

# Crear cadena predefinida
cadena = FabricaCadenaGas.crear_cadena_usa_a_mexico()

# Procesar transacción
resultados = cadena.procesar_transaccion(gas_inicial)

# Obtener resultado final
gas_final = resultados[-1]
print(f"Gas entregado: {gas_final.cantidad_mcf} MCF")
print(f"Precio final: ${gas_final.precio_por_mcf}/MCF")
```

### Ejemplo de Cadena Personalizada

```python
from gas_transaction_model import *

# Crear operaciones específicas
productor = ProductorUSA("Chevron", "Louisiana", Decimal('750000'))
transporte = TransportadorGas("Kinder Morgan", "Louisiana-Texas", 
                             Decimal('0.025'), Decimal('0.75'))
central = CentralMexico("CFE Salamanca", "Guanajuato", Decimal('600000'))

# Construir cadena
cadena = (CadenaTransaccionGas()
          .agregar_operacion(productor)
          .agregar_operacion(transporte)
          .agregar_operacion(central))

# Procesar
resultados = cadena.procesar_transaccion(gas_inicial)
```

## Métricas de Eficiencia

El sistema calcula automáticamente:

- **Eficiencia de cantidad**: Porcentaje de gas que llega al destino
- **Incremento de precio**: Porcentaje de aumento en el precio
- **Valores totales**: Comparación de valor inicial vs final

```python
eficiencia = cadena.calcular_eficiencia_total(gas_inicial)
print(f"Eficiencia: {eficiencia['eficiencia_cantidad']:.1f}%")
print(f"Incremento precio: {eficiencia['incremento_precio']:.1f}%")
```

## Archivos del Proyecto

- **`gas_transaction_model.py`**: Modelo principal con todas las clases
- **`ejemplo_uso.py`**: Ejemplos de uso del modelo
- **`test_gas_model.py`**: Suite de pruebas unitarias
- **`README.md`**: Esta documentación

## Ejecutar Ejemplos

```bash
# Ejecutar ejemplos de uso
python ejemplo_uso.py

# Ejecutar pruebas
python test_gas_model.py
```

## Características del Diseño

### Ventajas del Patrón de Composición

1. **Flexibilidad**: Fácil agregar/quitar operaciones
2. **Reutilización**: Operaciones independientes reutilizables
3. **Extensibilidad**: Nuevos tipos de operaciones sin modificar código existente
4. **Transparencia**: Cliente trata cadena como operación única

### Principios SOLID Aplicados

- **Single Responsibility**: Cada clase tiene una responsabilidad específica
- **Open/Closed**: Abierto para extensión, cerrado para modificación
- **Interface Segregation**: Interfaces específicas para cada tipo de operación
- **Dependency Inversion**: Dependencias en abstracciones, no implementaciones

### Características Técnicas

- **Precisión decimal**: Uso de `Decimal` para cálculos financieros precisos
- **Type hints**: Código completamente tipado para mejor mantenimiento
- **Documentación**: Docstrings completos en español
- **Testing**: Suite de pruebas unitarias comprehensiva
- **Patrón Builder**: Construcción fluida de cadenas con método encadenado

## Extensiones Futuras

El modelo puede extenderse fácilmente para incluir:

- Operaciones de almacenamiento
- Múltiples rutas de transporte
- Contratos y regulaciones
- Optimización de rutas
- Análisis de riesgos
- Integración con sistemas de mercado

## Conclusión

Este modelo proporciona una base sólida y extensible para modelar transacciones complejas de gas natural entre países, implementando patrones de diseño robustos y buenas prácticas de programación.