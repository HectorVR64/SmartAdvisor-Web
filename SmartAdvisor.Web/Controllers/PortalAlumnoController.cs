using Microsoft.AspNetCore.Mvc;
using SmartAdvisor.Core.Services;
using SmartAdvisor.Web.Models;
using SmartAdvisor.Web.Services;

namespace SmartAdvisor.Web.Controllers;

/// <summary>
/// Portal del alumno (solo lectura): sus calificaciones, avance y recomendación.
/// El alumno se elige en una pantalla de acceso (sin contraseña) y se recuerda en una cookie.
/// </summary>
[Route("portal-alumno")]
public class PortalAlumnoController : Controller
{
    private const string Cookie = "alumnoId";
    private readonly RepositorioJson _repo;
    private readonly AnalizadorAcademico _analizador;

    public PortalAlumnoController(RepositorioJson repo, AnalizadorAcademico analizador)
    {
        _repo = repo;
        _analizador = analizador;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var datos = _repo.Cargar();
        var id = Request.Cookies[Cookie];
        var alumno = datos.Alumnos.FirstOrDefault(a => a.Id == id);
        if (alumno is null) return View("Selector", datos.Alumnos.OrderBy(a => a.Nombre).ToList());

        var resultado = await _analizador.AnalizarAsync(datos, ct);
        var analisis = resultado.Analisis.Alumnos.First(a => a.AlumnoId == alumno.Id);

        // Quién imparte cada materia (docentes.json): MateriaId -> nombre del docente.
        var docentes = _repo.CargarDocentes();
        string DocenteDe(string materiaId) =>
            docentes.FirstOrDefault(d => d.MateriasIds.Contains(materiaId))?.Nombre ?? "—";

        var filas = Consultas.HistorialConMateria(datos.Historial, datos.Materias, alumno.Id)
            .Select(t => new FilaCalificacion(t.Registro, t.Materia, t.Area, DocenteDe(t.Registro.MateriaId)))
            .ToList();

        return View(new PortalAlumnoVm(analisis, filas, resultado));
    }

    [HttpGet("entrar/{id}")]
    public IActionResult Entrar(string id)
    {
        if (!_repo.Cargar().Alumnos.Any(a => a.Id == id)) return NotFound();
        Response.Cookies.Append(Cookie, id, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddDays(7) });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("salir")]
    public IActionResult Salir()
    {
        Response.Cookies.Delete(Cookie);
        return RedirectToAction(nameof(Index));
    }
}
