using System;
using System.Collections.Generic;

namespace ModeloSaavi.Units;

public static class EnergyUnits
{
    public const decimal GjPerMmbtu = 1.055056m;
    public static decimal ToGJ(decimal mmbtu) => mmbtu * GjPerMmbtu;
    public static decimal ToMMBtu(decimal gj) => gj / GjPerMmbtu;
}
