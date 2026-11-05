using System.Text.Encodings.Web;
using System.Text.Json;
using SmartAdvisor.Core.Models;

namespace SmartAdvisor.Core.Services;

/// <summary>Opciones JSON compartidas: camelCase y lectura sin distinguir mayúsculas.</summary>
public static class JsonConfig
{
    public static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        // Escribe á, é, ñ... tal cual (si no, quedarían como \u00E1 en los archivos JSON)
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serializar<T>(T valor) => JsonSerializer.Serialize(valor, Opciones);

    public static T? Deserializar<T>(string json) => JsonSerializer.Deserialize<T>(json, Opciones);
}

/// <summary>
/// Lee y escribe las siete colecciones JSON de una carpeta (alumnos.json, materias.json, ...).
/// Es la ÚNICA parte con efectos secundarios (entrada/salida); el análisis no depende de ella.
/// </summary>
public sealed class RepositorioJson
{
    private static readonly object Candado = new();
    private readonly string _directorio;

    public RepositorioJson(string directorio) => _directorio = directorio;

    public string Directorio => _directorio;

    private string Ruta(string nombre) => Path.Combine(_directorio, nombre + ".json");

    private IReadOnlyList<T> Leer<T>(string nombre)
    {
        var ruta = Ruta(nombre);
        if (!File.Exists(ruta)) return Array.Empty<T>();
        return JsonConfig.Deserializar<List<T>>(File.ReadAllText(ruta)) ?? new List<T>();
    }

    public DatosAcademicos Cargar()
    {
        lock (Candado)
        {
            return new DatosAcademicos(
                Leer<Alumno>("alumnos"), Leer<Materia>("materias"), Leer<Especialidad>("especialidades"),
                Leer<Curso>("cursos"), Leer<Certificacion>("certificaciones"),
                Leer<Historial>("historial"), Leer<Regla>("reglas"));
        }
    }

    public void Guardar<T>(string nombre, IReadOnlyList<T> elementos)
    {
        lock (Candado)
        {
            Directory.CreateDirectory(_directorio);
            File.WriteAllText(Ruta(nombre), JsonConfig.Serializar(elementos));
        }
    }

    /// <summary>Copia los JSON de ejemplo a la carpeta de trabajo si todavía no existen.</summary>
    public static void Sembrar(string origen, string destino)
    {
        Directory.CreateDirectory(destino);
        foreach (var archivo in Directory.GetFiles(origen, "*.json"))
        {
            var objetivo = Path.Combine(destino, Path.GetFileName(archivo));
            if (!File.Exists(objetivo)) File.Copy(archivo, objetivo);
        }
    }
}
