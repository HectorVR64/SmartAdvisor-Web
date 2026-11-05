using System.Globalization;

namespace SmartAdvisor.Web.Services;

/// <summary>Formato numérico fijo (punto decimal) para vistas y estilos CSS.</summary>
public static class Fmt
{
    public static string Dec(decimal x) => x.ToString("0.0", CultureInfo.InvariantCulture);
    public static string Dec(decimal? x) => x.HasValue ? Dec(x.Value) : "—";
    public static string Pct(decimal x) => Math.Clamp(x, 0, 100).ToString("0.##", CultureInfo.InvariantCulture);
    public static string Ancho(decimal valor, decimal maximo) =>
        maximo <= 0 ? "0" : Math.Clamp(100m * valor / maximo, 0, 100).ToString("0.##", CultureInfo.InvariantCulture);
    public static string Estado(string estado) => estado switch
    {
        "recomendada" => "Recomendada",
        "condicionada" => "Condicionada",
        _ => "Sin recomendación"
    };
}
