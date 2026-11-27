using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartAdvisor.Core.Logica;
using SmartAdvisor.Core.Models;
using SmartAdvisor.Core.Services;

namespace SmartAdvisor.Functions;

/// <summary>
/// AcademicInferenceFunction: razonamiento LÓGICO sobre el perfil del alumno.
/// Carga la base de hechos (hechos.json) y la base de reglas (reglas_logicas.json) y las resuelve
/// con unificación y resolución de cláusulas de Horn (SmartAdvisor.Core.Logica).
/// La Function solo recibe la petición y responde JSON; el razonamiento vive en el Core.
///
///   GET /api/inferencia                  -> inferencia de todos los alumnos
///   GET /api/inferencia/{alumno}         -> "¿qué especialidad se recomienda?" y "¿qué materias debería cursar?"
///                                           ({alumno} puede ser ana o ALU-001)
///   GET /api/logica/consulta?q=...       -> consulta libre, p. ej. q=recomienda(ana,E)
///   GET /api/logica/base                 -> hechos y reglas cargados
/// </summary>
public class AcademicInferenceFunction
{
    private readonly BaseConocimiento _kb;
    private readonly RepositorioJson _repositorio;

    public AcademicInferenceFunction(BaseConocimiento kb, RepositorioJson repositorio)
    {
        _kb = kb;
        _repositorio = repositorio;
    }

    [Function("AcademicInferenceFunction")]
    public IActionResult Inferir(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "inferencia/{alumno?}")] HttpRequest req,
        string? alumno)
    {
        // La afinidad (Core funcional) solo ordena cuando varias reglas recomiendan a la vez.
        var datos = _repositorio.Cargar();

        if (string.IsNullOrWhiteSpace(alumno))
            return Json(_kb.IdsAlumno.Values.Select(a => InferirDe(a, datos)).ToList());

        var atomo = _kb.AtomoDeAlumno(alumno);
        return atomo is null
            ? Json(new { error = $"No existe el alumno {alumno} en la base de hechos." }, 404)
            : Json(InferirDe(atomo, datos));
    }

    [Function("ConsultaLogica")]
    public IActionResult Consultar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "logica/consulta")] HttpRequest req)
    {
        var q = req.Query["q"].ToString();
        if (string.IsNullOrWhiteSpace(q))
            return Json(new { error = "Falta el parámetro q. Ejemplo: /api/logica/consulta?q=recomienda(ana,E)" }, 400);

        try
        {
            return Json(Inferencia.Consultar(_kb, q));
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            return Json(new { error = "Consulta no válida.", detalle = ex.Message }, 400);
        }
    }

    [Function("BaseConocimiento")]
    public IActionResult Base(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "logica/base")] HttpRequest req) =>
        Json(new
        {
            hechos = _kb.Hechos.Select(h => new { h.Id, clausula = h.ToString() }),
            reglas = _kb.Reglas.Select(r => new { r.Id, r.Descripcion, clausula = r.ToString() })
        });

    private ResultadoInferencia InferirDe(string atomo, DatosAcademicos datos)
    {
        var alumno = datos.Alumnos.FirstOrDefault(a =>
            _kb.IdsAlumno.TryGetValue(a.Id, out var at) && at == atomo);
        return Inferencia.Inferir(_kb, atomo, alumno is null ? null : Inferencia.AfinidadDesde(alumno, datos));
    }

    private static ContentResult Json(object valor, int estado = 200) => new()
    {
        Content = JsonConfig.Serializar(valor),
        ContentType = "application/json; charset=utf-8",
        StatusCode = estado
    };
}
