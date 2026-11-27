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

    /// <summary>Iniciales para el avatar: primeras dos palabras sin títulos (Dra., Mtro., ...).</summary>
    public static string Iniciales(string nombre) =>
        string.Concat((nombre ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.EndsWith('.'))
            .Take(2)
            .Select(p => char.ToUpperInvariant(p[0])));

    /// <summary>Clase de color de una calificación: alta (90+), aprobada (70+) o baja.</summary>
    public static string ClaseNota(decimal? nota) => nota switch
    {
        null => "nota-na",
        >= 90 => "nota-alta",
        >= 70 => "nota-ok",
        _ => "nota-baja"
    };
}
