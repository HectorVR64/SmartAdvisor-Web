using System.Text.Json;
using SmartAdvisor.Core.Services;

namespace SmartAdvisor.Core.Logica;

/// <summary>
/// Base de conocimiento = base de HECHOS + base de REGLAS, cargadas de hechos.json y
/// reglas_logicas.json. Los nombres legibles (Ana Torres, Inteligencia Artificial) solo sirven
/// para presentar resultados; el razonamiento usa únicamente los átomos.
/// </summary>
public sealed record BaseConocimiento(
    IReadOnlyList<Clausula> Hechos,
    IReadOnlyList<Clausula> Reglas,
    IReadOnlyDictionary<string, string> Nombres,
    IReadOnlyDictionary<string, string> IdsAlumno)
{
    public IEnumerable<Clausula> Clausulas => Hechos.Concat(Reglas);

    public string NombreDe(string atomo) => Nombres.TryGetValue(atomo, out var n) ? n : atomo;

    /// <summary>Acepta el átomo (ana) o el Id del alumno (ALU-001) y devuelve el átomo, o null.</summary>
    public string? AtomoDeAlumno(string texto)
    {
        var t = texto.Trim();
        if (IdsAlumno.TryGetValue(t.ToUpperInvariant(), out var porId)) return porId;
        return IdsAlumno.Values.FirstOrDefault(v => v.Equals(t, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record HechoJson(string Id, string Hecho);
    private sealed record ArchivoHechos(
        Dictionary<string, string>? Ids, Dictionary<string, string>? Nombres, List<HechoJson> Hechos);
    private sealed record ReglaJson(string Id, string Descripcion, string Regla);
    private sealed record ArchivoReglas(List<ReglaJson> Reglas);

    /// <summary>Convierte los dos textos JSON en cláusulas. Función pura: no toca el disco.</summary>
    public static BaseConocimiento Desde(string hechosJson, string reglasJson)
    {
        var h = JsonConfig.Deserializar<ArchivoHechos>(hechosJson)
                ?? throw new FormatException("hechos.json está vacío.");
        var r = JsonConfig.Deserializar<ArchivoReglas>(reglasJson)
                ?? throw new FormatException("reglas_logicas.json está vacío.");

        var hechos = h.Hechos.Select(x => Parser.Clausula(x.Hecho, x.Id)).ToList();
        var reglas = r.Reglas.Select(x => Parser.Clausula(x.Regla, x.Id, x.Descripcion)).ToList();
        return new BaseConocimiento(hechos, reglas, h.Nombres ?? new(), h.Ids ?? new());
    }

    /// <summary>Lectura de disco (único efecto secundario): carpeta con hechos.json y reglas_logicas.json.</summary>
    public static BaseConocimiento DesdeCarpeta(string carpeta) =>
        Desde(File.ReadAllText(Path.Combine(carpeta, "hechos.json")),
              File.ReadAllText(Path.Combine(carpeta, "reglas_logicas.json")));
}
