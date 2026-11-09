namespace NpiManagement.IntegrationTests.Sispro;

/// <summary>
/// Respuestas reales de GET /api/cotizacion/{n} de SISPRO (2026-10-27), recortadas a los campos que
/// usa el mapeo. La referencia se cambió por "REF-n" (traía vendedor y cliente) y no se guardan
/// costos ni utilidades, sólo el precio de venta. Las tres cubren los casos que se ven en SISPRO:
///  678 — pouch 2 sellos laminado PET/PE con zipper, 8 tintas por el reverso.
///  677 — bobina laminada PET/PE impresa, con core y tapón.
///  680 — bobina monocapa sin impresión: la película viene en "CORTE" y SIN espesor.
/// </summary>
public static class SisproFixtures
{
    public const string Cotizacion678 = """
    {"Codigo":678,"CodigoSolicitud":711,"Referencia":"REF-678","FechaCotizacion":"2026-10-20T17:43:55.133Z",
     "EstadoSolicitud":{"Descripcion":"Cotizada"},"UnidadVenta":{"Descripcion":"Millar"},"Cliente":{"Descripcion":"Cliente"},
     "CotizacionCantidad":[{"Cantidad":50,"PrecioUnidadVenta":2742.327,"PrecioTotal":137116.16345455},{"Cantidad":100,"PrecioUnidadVenta":2566.368,"PrecioTotal":256633.10286207}],
     "CotizacionElemento":[{"MedidaEje":764,"MedidaDesarrollo":244,"EtiquetasEje":1,"EtiquetasDesarrollo":2,"AnchoMaterial":794,"CantidadColoresFrente":0,"CantidadColoresLiner":8,
      "CotizacionElementoProceso":[
       {"ItemProceso":1,"Proceso":{"Descripcion":"IMPRESION FLEXO"},"CotizacionElementoProcesoMateriales":[{"Espesor":12,"Densidad":1.4,"Gramaje":null,"Ancho":794,"Largo":null,"Material":{"DescripcionInterna":"PET NAT"}}]},
       {"ItemProceso":2,"Proceso":{"Descripcion":"LAMINADO"},"CotizacionElementoProcesoMateriales":[{"Espesor":75,"Densidad":0.93,"Gramaje":null,"Ancho":794,"Largo":null,"Material":{"DescripcionInterna":"LDPE RESPALDO LAM / GENERICO"}},{"Espesor":null,"Densidad":null,"Gramaje":2,"Ancho":null,"Largo":null,"Material":{"DescripcionInterna":"ADHESIVO"}}]},
       {"ItemProceso":3,"Proceso":{"Descripcion":"CORTE"},"CotizacionElementoProcesoMateriales":[]},
       {"ItemProceso":4,"Proceso":{"Descripcion":"POUCH 2 SELLOS"},"CotizacionElementoProcesoMateriales":[{"Espesor":null,"Densidad":null,"Gramaje":null,"Ancho":null,"Largo":244,"Material":{"DescripcionInterna":"ZIPPER POUCH"}},{"Espesor":null,"Densidad":null,"Gramaje":null,"Ancho":null,"Largo":null,"Material":{"DescripcionInterna":"POUCH 2 SELLOS"}}]},
       {"ItemProceso":5,"Proceso":{"Descripcion":"PRESENTACION CAJA"},"CotizacionElementoProcesoMateriales":[{"Espesor":null,"Densidad":null,"Gramaje":null,"Ancho":null,"Largo":null,"Material":{"DescripcionInterna":"Caja Kraft"}}]},
       {"ItemProceso":6,"Proceso":{"Descripcion":"PLACAS"},"CotizacionElementoProcesoMateriales":[{"Espesor":null,"Densidad":null,"Gramaje":null,"Ancho":null,"Largo":null,"Material":{"DescripcionInterna":"Polímero"}}]},
       {"ItemProceso":7,"Proceso":{"Descripcion":"TARIMAS"},"CotizacionElementoProcesoMateriales":[{"Espesor":null,"Densidad":null,"Gramaje":null,"Ancho":null,"Largo":null,"Material":{"DescripcionInterna":"Tarimas"}}]},
       {"ItemProceso":8,"Proceso":{"Descripcion":"FLETE"},"CotizacionElementoProcesoMateriales":[{"Espesor":null,"Densidad":null,"Gramaje":null,"Ancho":null,"Largo":null,"Material":{"DescripcionInterna":"600 KM TAMAULIPAS, COLIMA, DURANGO, PUEBLA"}}]}]}]}
    """;

    public const string Cotizacion677 = """
    {"Codigo":677,"CodigoSolicitud":710,"Referencia":"REF-677","FechaCotizacion":"2026-10-16T17:46:47.790Z",
     "EstadoSolicitud":{"Descripcion":"Cotizada"},"UnidadVenta":{"Descripcion":"Kilo"},
     "CotizacionCantidad":[{"Cantidad":1000,"PrecioUnidadVenta":138.51,"PrecioTotal":138521.50363636},{"Cantidad":1200,"PrecioUnidadVenta":133.038,"PrecioTotal":159721.368}],
     "CotizacionElemento":[{"MedidaEje":240,"MedidaDesarrollo":145,"EtiquetasEje":1,"EtiquetasDesarrollo":4,"AnchoMaterial":270,"CantidadColoresFrente":0,"CantidadColoresLiner":5,
      "CotizacionElementoProceso":[
       {"ItemProceso":1,"Proceso":{"Descripcion":"IMPRESION FLEXO"},"CotizacionElementoProcesoMateriales":[{"Espesor":12,"Densidad":1.4,"Gramaje":null,"Ancho":270,"Largo":null,"Material":{"DescripcionInterna":"PET NAT"}}]},
       {"ItemProceso":2,"Proceso":{"Descripcion":"LAMINADO"},"CotizacionElementoProcesoMateriales":[{"Espesor":65,"Densidad":0.93,"Gramaje":null,"Ancho":270,"Largo":null,"Material":{"DescripcionInterna":"LDPE RESPALDO LAM / GENERICO"}},{"Espesor":null,"Densidad":null,"Gramaje":2,"Ancho":null,"Largo":null,"Material":{"DescripcionInterna":"ADHESIVO"}}]},
       {"ItemProceso":3,"Proceso":{"Descripcion":"CORTE"},"CotizacionElementoProcesoMateriales":[{"Espesor":null,"Densidad":null,"Gramaje":null,"Ancho":null,"Largo":null,"Material":{"DescripcionInterna":"Centro 3´´"}},{"Espesor":null,"Densidad":null,"Gramaje":null,"Ancho":null,"Largo":null,"Material":{"DescripcionInterna":"Tapon Core 3\""}}]},
       {"ItemProceso":4,"Proceso":{"Descripcion":"PRESENTACION CAJA"},"CotizacionElementoProcesoMateriales":[]},
       {"ItemProceso":5,"Proceso":{"Descripcion":"TARIMAS"},"CotizacionElementoProcesoMateriales":[{"Espesor":null,"Densidad":null,"Gramaje":null,"Ancho":null,"Largo":null,"Material":{"DescripcionInterna":"Tarimas"}}]},
       {"ItemProceso":6,"Proceso":{"Descripcion":"FLETE"},"CotizacionElementoProcesoMateriales":[{"Espesor":null,"Densidad":null,"Gramaje":null,"Ancho":null,"Largo":null,"Material":{"DescripcionInterna":"800 KM VERACRUZ, CHIHUAHUA, GUATEMALA, NL MONTERREY OAXACA"}}]}]}]}
    """;

    public const string Cotizacion680 = """
    {"Codigo":680,"CodigoSolicitud":713,"Referencia":"REF-680","FechaCotizacion":"2026-10-21T19:24:22.970Z",
     "EstadoSolicitud":{"Descripcion":"Cotizada"},"UnidadVenta":{"Descripcion":"Kilo"},
     "CotizacionCantidad":[{"Cantidad":1000,"PrecioUnidadVenta":0.08,"PrecioTotal":83.17142857}],
     "CotizacionElemento":[{"MedidaEje":400,"MedidaDesarrollo":100,"EtiquetasEje":1,"EtiquetasDesarrollo":8,"AnchoMaterial":430,"CantidadColoresFrente":0,"CantidadColoresLiner":0,
      "CotizacionElementoProceso":[
       {"ItemProceso":1,"Proceso":{"Descripcion":"IMPRESION FLEXO"},"CotizacionElementoProcesoMateriales":[]},
       {"ItemProceso":2,"Proceso":{"Descripcion":"CORTE"},"CotizacionElementoProcesoMateriales":[{"Espesor":null,"Densidad":0.92,"Gramaje":null,"Ancho":430,"Largo":null,"Material":{"DescripcionInterna":"LDPE COMERCIAL NAT"}},{"Espesor":null,"Densidad":null,"Gramaje":null,"Ancho":null,"Largo":null,"Material":{"DescripcionInterna":"Centro 3´´"}},{"Espesor":null,"Densidad":null,"Gramaje":null,"Ancho":null,"Largo":null,"Material":{"DescripcionInterna":"Tapon Core 3\""}}]},
       {"ItemProceso":3,"Proceso":{"Descripcion":"PRESENTACION CAJA"},"CotizacionElementoProcesoMateriales":[]},
       {"ItemProceso":4,"Proceso":{"Descripcion":"TARIMAS"},"CotizacionElementoProcesoMateriales":[]},
       {"ItemProceso":5,"Proceso":{"Descripcion":"FLETE"},"CotizacionElementoProcesoMateriales":[{"Espesor":null,"Densidad":null,"Gramaje":null,"Ancho":null,"Largo":null,"Material":{"DescripcionInterna":"Tarimas"}}]}]}]}
    """;

    /// <summary>Catálogos como los siembra el sistema (los de producción pueden variar; por eso el mapeo valida contra ellos).</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Catalogs = new Dictionary<string, IReadOnlyList<string>>
    {
        ["TIPO DE PELICULA"] = ["BOPP", "CPP", "PET", "BOPA", "FOIL", "MDO", "PE"],
        ["TERMINADO"] = ["TRANSPARENTE", "METALIZADO", "BLANCO", "MATE", "OTRO"],
        ["TERMINADO LAMINACION"] = ["N/A", "BAJO COF", "OXODEGRADABLE", "BAJO TIS", "CAVITADO", "ALTA BARRERA", "RETORTABLE", "ANTIFOG", "PELABLE", "ALOX", "SELLABLE"],
        ["FORMULA LDPE"] = ["CONGELADO", "ALTO SELLO", "ALTA CLARIDAD", "COMERCIAL", "EMPAQUE AUTOMÁTICO", "LAMINACIÓN", "OTRO", "N/A"],
        ["TIPO DE PRODUCTO"] = ["POUCH", "BOLSA", "BOBINA", "BOLSA EN ROLLO", "BOLSA DE CAMISETA", "BOLSA BOUTIQUE", "LAMINA", "OTRO"],
        ["FORMATO"] = ["STANDUP POUCH (SELLO K)", "DOYPACK (SELLO \"U\"/SONRISA)", "POUCH 3 SELLOS", "POUCH 2 SELLOS", "BOLSA SELLO LATERAL", "BOLSA SELLO DE FONDO", "BOBINA PLANA", "BOBINA TUBULAR", "OTRO", "N/A"],
        ["CIERRE"] = ["ZIPPER", "ZIPPER MARIPOSA", "ZIPPER VELCRO", "N/A"],
        ["UNIDAD DE VENTA"] = ["MILL", "PZAS", "KG", "OTRO"],
        ["TIPO IMPRESIÓN"] = ["INTERNA (REVERSO)", "EXTERNA (FRENTE)", "N/A"],
        ["CORE"] = ["3\"", "6\""],
    };
}
