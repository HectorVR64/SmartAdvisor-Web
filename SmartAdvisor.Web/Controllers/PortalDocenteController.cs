using Microsoft.AspNetCore.Mvc;
using SmartAdvisor.Core.Models;
using SmartAdvisor.Core.Services;
using SmartAdvisor.Web.Services;

namespace SmartAdvisor.Web.Controllers;

/// <summary>
/// Portal del docente: ve la lista de alumnos de cada materia que imparte, registra o corrige
/// calificaciones y consulta las estadísticas de su grupo. Cada cambio se guarda en historial.json.
/// </summary>
[Route("portal-docente")]
public class PortalDocenteController : Controller
{
    private const string Cookie = "docenteId";
    private readonly RepositorioJson _repo;

    public PortalDocenteController(RepositorioJson repo) => _repo = repo;

    private Docente? DocenteActual() =>
        _repo.CargarDocentes().FirstOrDefault(d => d.Id == Request.Cookies[Cookie]);

    [HttpGet("")]
    public IActionResult Index(string? materia)
    {
        var docente = DocenteActual();
        if (docente is null) return View("Selector", _repo.CargarDocentes());

        var datos = _repo.Cargar();
        var materias = Consultas.MateriasDeDocente(docente, datos.Materias);
        var actual = materias.FirstOrDefault(m => m.Id == materia) ?? materias.FirstOrDefault();

        var filas = actual is null
            ? Array.Empty<(Historial Registro, Alumno Alumno)>()
            : Consultas.AlumnosDeMateria(datos.Historial, datos.Alumnos, actual.Id);

        // Estadística del grupo con Aggregate: solo cuentan los registros que ya tienen calificación.
        var notas = filas.Where(f => f.Registro.Calificacion.HasValue).Select(f => f.Registro.Calificacion!.Value);
        var estadistica = StudentAnalyzer.EstadisticaDe(notas);

        return View(new Models.PortalDocenteVm(
            docente, materias, actual, filas, estadistica,
            filas.Count(f => f.Registro.Estatus == "aprobada"),
            filas.Count(f => f.Registro.Estatus == "reprobada"),
            filas.Count(f => f.Registro.Estatus == "cursando"),
            actual is null ? Array.Empty<Alumno>() : Consultas.AlumnosInscribibles(datos.Alumnos, datos.Historial, actual.Id)));
    }

    [HttpGet("entrar/{id}")]
    public IActionResult Entrar(string id)
    {
        if (!_repo.CargarDocentes().Any(d => d.Id == id)) return NotFound();
        Response.Cookies.Append(Cookie, id, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddDays(7) });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("salir")]
    public IActionResult Salir()
    {
        Response.Cookies.Delete(Cookie);
        return RedirectToAction(nameof(Index));
    }

    // POST: registra o corrige la calificación de un registro del historial.
    [HttpPost("calificar"), ValidateAntiForgeryToken]
    public IActionResult Calificar(string historialId, string materiaId, decimal? calificacion)
    {
        var docente = DocenteActual();
        if (docente is null) return RedirectToAction(nameof(Index));
        if (!docente.MateriasIds.Contains(materiaId)) return Forbid();

        var errores = Gestion.ValidarCalificacion(calificacion);
        if (errores.Count > 0)
        {
            TempData["Error"] = string.Join(" ", errores);
            return RedirectToAction(nameof(Index), new { materia = materiaId });
        }

        var datos = _repo.Cargar();
        var registro = datos.Historial.FirstOrDefault(h => h.Id == historialId && h.MateriaId == materiaId);
        if (registro is null) return NotFound();

        var nuevo = Gestion.Calificar(registro, calificacion!.Value);
        _repo.Guardar("historial", Gestion.Reemplazar(datos.Historial, h => h.Id == historialId, nuevo));
        TempData["Mensaje"] = $"Calificación guardada: {calificacion:0.#} ({nuevo.Estatus}).";
        return RedirectToAction(nameof(Index), new { materia = materiaId });
    }

    // POST: inscribe a un alumno en la materia (queda "cursando", sin calificación).
    [HttpPost("inscribir"), ValidateAntiForgeryToken]
    public IActionResult Inscribir(string alumnoId, string materiaId)
    {
        var docente = DocenteActual();
        if (docente is null) return RedirectToAction(nameof(Index));
        if (!docente.MateriasIds.Contains(materiaId)) return Forbid();

        var datos = _repo.Cargar();
        var nuevo = new Historial(
            Gestion.SiguienteId("HIS", datos.Historial.Select(h => h.Id)),
            alumnoId, materiaId, "2026-2", null, "cursando");

        var errores = Gestion.ValidarHistorial(nuevo, datos);
        if (errores.Count > 0)
        {
            TempData["Error"] = string.Join(" ", errores);
        }
        else
        {
            _repo.Guardar("historial", Gestion.Agregar(datos.Historial, nuevo));
            TempData["Mensaje"] = "Alumno inscrito en la materia.";
        }
        return RedirectToAction(nameof(Index), new { materia = materiaId });
    }
}
