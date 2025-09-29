# ModeloSaavi

## Modelo de Transacciones de Gas USA-México

Este proyecto modela las transacciones que participan en forma encadenada (Composición) para abastecer desde un conjunto de productores en USA a un conjunto de Centrales en México.

### Características Principales

- ✅ **Patrón de Composición**: Secuencia de operaciones encadenadas
- ✅ **Modelado completo**: Desde productores USA hasta centrales México
- ✅ **Cantidades diarias**: Manejo de DiaGas (cantidades diarias de gas)
- ✅ **Métricas de eficiencia**: Cálculo automático de pérdidas y costos
- ✅ **Extensibilidad**: Fácil agregar nuevas operaciones

### Inicio Rápido

```python
from gas_transaction_model import FabricaCadenaGas, DiaGas
from datetime import date
from decimal import Decimal

# Crear solicitud de gas
gas = DiaGas(date(2024, 1, 15), Decimal('500000'), Decimal('3.50'))

# Crear y ejecutar cadena de transacciones
cadena = FabricaCadenaGas.crear_cadena_usa_a_mexico()
resultados = cadena.procesar_transaccion(gas)

# Ver resultado final
gas_final = resultados[-1]
print(f"Gas entregado: {gas_final.cantidad_mcf:,} MCF")
print(f"Precio final: ${gas_final.precio_por_mcf}/MCF")
```

### Archivos Principales

- `gas_transaction_model.py` - Modelo principal
- `ejemplo_uso.py` - Ejemplos de uso
- `test_gas_model.py` - Pruebas unitarias
- `DOCUMENTACION.md` - Documentación completa

### Ejecutar

```bash
# Ver ejemplos funcionando
python ejemplo_uso.py

# Ejecutar pruebas
python test_gas_model.py
```

Ver [DOCUMENTACION.md](DOCUMENTACION.md) para detalles completos.