using System.Net.Http.Json;
using System.Text.Json;
using SmartAdvisor.Core.Models;
using SmartAdvisor.Core.Services;

namespace SmartAdvisor.Web.Services;

public sealed record ResultadoAnalisis(AnalisisAcademico Analisis, string Origen, string? Aviso);

/// <summary>
/// Pide el análisis académico. Si hay una Azure Function configurada (StudentAnalyzer:BaseUrl) le envía los
/// datos por POST /api/analisis; si no existe o falla, calcula localmente con el MISMO código puro de Core,
/// de modo que el dashboard nunca se queda sin datos.
/// </summary>
public sealed class AnalizadorAcademico
{
    private readonly HttpClient _http;
    private readonly bool _hayFuncion;

    public AnalizadorAcademico(HttpClient http, IConfiguration configuracion)
    {
        _http = http;
        _hayFuncion = !string.IsNullOrWhiteSpace(configuracion["StudentAnalyzer:BaseUrl"]);
    }

    public async Task<ResultadoAnalisis> AnalizarAsync(DatosAcademicos datos, CancellationToken ct = default)
    {
        if (!_hayFuncion)
        {
            return new ResultadoAnalisis(StudentAnalyzer.Analizar(datos), "Cálculo local", null);
        }

        try
        {
            var respuesta = await _http.PostAsJsonAsync("api/analisis", datos, JsonConfig.Opciones, ct);
            respuesta.EnsureSuccessStatusCode();
            var analisis = await respuesta.Content.ReadFromJsonAsync<AnalisisAcademico>(JsonConfig.Opciones, ct);
            if (analisis is not null)
            {
                return new ResultadoAnalisis(analisis, "Azure Function", null);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new ResultadoAnalisis(StudentAnalyzer.Analizar(datos), "Cálculo local",
                "No se pudo llamar a la Azure Function (" + ex.Message + "). Se calculó localmente.");
        }

        return new ResultadoAnalisis(StudentAnalyzer.Analizar(datos), "Cálculo local", "La Azure Function devolvió una respuesta vacía.");
    }
}
