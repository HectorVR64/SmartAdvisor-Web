using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartAdvisor.Core.Models;
using SmartAdvisor.Core.Services;

namespace SmartAdvisor.Functions;

/// <summary>
/// StudentAnalyzerFunction: agrega el historial académico con LINQ y devuelve estadísticas y
/// recomendaciones básicas. La lógica NO está aquí: vive en SmartAdvisor.Core.StudentAnalyzer
/// (funciones puras). La Function solo recibe la petición, llama a la función pura y responde JSON.
///
///   GET  /api/health                      -> comprobación rápida
///   GET  /api/analisis                    -> análisis con los JSON de ejemplo incluidos en el despliegue
///   POST /api/analisis                    -> análisis de los datos que se envían en el cuerpo (DatosAcademicos)
///   GET  /api/alumnos/{id}/analisis       -> análisis de un alumno (con los JSON de ejemplo)
/// </summary>
public class StudentAnalyzerFunction
{
    private readonly RepositorioJson _repositorio;

    public StudentAnalyzerFunction(RepositorioJson repositorio) => _repositorio = repositorio;

    [Function("Health")]
    public IActionResult Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req) =>
        Json(new { estado = "ok", servicio = "StudentAnalyzerFunction" });

    [Function("StudentAnalyzerFunction")]
    public async Task<IActionResult> Analizar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "analisis")] HttpRequest req)
    {
        DatosAcademicos? datos;

        if (HttpMethods.IsPost(req.Method))
        {
            try
            {
                datos = await JsonSerializer.DeserializeAsync<DatosAcademicos>(
                    req.Body, JsonConfig.Opciones, req.HttpContext.RequestAborted);
            }
            catch (JsonException ex)
            {
                return Json(new { error = "El cuerpo no es un JSON válido de DatosAcademicos.", detalle = ex.Message }, 400);
            }

            if (datos is null)
            {
                return Json(new { error = "El cuerpo de la petición está vacío." }, 400);
            }
        }
        else
        {
            datos = _repositorio.Cargar();
        }

        return Json(StudentAnalyzer.Analizar(datos));
    }

    [Function("AnalisisDeAlumno")]
    public IActionResult AnalizarAlumno(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "alumnos/{id}/analisis")] HttpRequest req,
        string id)
    {
        var datos = _repositorio.Cargar();
        var alumno = datos.Alumnos.FirstOrDefault(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        return alumno is null
            ? Json(new { error = $"No existe el alumno {id}." }, 404)
            : Json(StudentAnalyzer.AnalizarAlumno(alumno, datos));
    }

    // JSON con las mismas opciones (camelCase) que usa el resto de la solución.
    private static ContentResult Json(object valor, int estado = 200) => new()
    {
        Content = JsonConfig.Serializar(valor),
        ContentType = "application/json; charset=utf-8",
        StatusCode = estado
    };
}
