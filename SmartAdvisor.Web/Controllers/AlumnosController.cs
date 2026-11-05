using Microsoft.AspNetCore.Mvc;
using SmartAdvisor.Core.Models;
using SmartAdvisor.Core.Services;
using SmartAdvisor.Web.Models;
using SmartAdvisor.Web.Services;

namespace SmartAdvisor.Web.Controllers;

public class AlumnosController : Controller
{
    private readonly RepositorioJson _repo;
    private readonly AnalizadorAcademico _analizador;

    public AlumnosController(RepositorioJson repo, AnalizadorAcademico analizador)
    {
        _repo = repo;
        _analizador = analizador;
    }

    // GET /Alumnos?texto=ana&interes=IA&estatus=activo
    public async Task<IActionResult> Index(string? texto, string? interes, string? estatus, CancellationToken ct)
    {
        var datos = _repo.Cargar();
        var resultado = await _analizador.AnalizarAsync(datos, ct);
        var filtrados = Consultas.FiltrarAlumnos(datos.Alumnos, texto, interes, estatus);
        var recomendaciones = resultado.Analisis.Alumnos.ToDictionary(a => a.AlumnoId, a => a.Recomendacion);

        return View(new AlumnosIndexVm(filtrados, recomendaciones, texto, interes, estatus, datos.Alumnos.Count, resultado));
    }

    // GET /Alumnos/Detalle/ALU-001
    public async Task<IActionResult> Detalle(string id, CancellationToken ct)
    {
        var datos = _repo.Cargar();
        if (!datos.Alumnos.Any(a => a.Id == id)) return NotFound();

        var resultado = await _analizador.AnalizarAsync(datos, ct);
        var analisis = resultado.Analisis.Alumnos.First(a => a.AlumnoId == id);
        var historial = Consultas.HistorialConMateria(datos.Historial, datos.Materias, id);

        return View(new DetalleAlumnoVm(analisis, historial, datos.Materias, new HistorialForm { AlumnoId = id }, resultado));
    }

    // ---------------------------------------------------------------- alta y edición

    public IActionResult Crear() => View("Form", new AlumnoForm());

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Crear(AlumnoForm f)
    {
        var datos = _repo.Cargar();
        var nuevo = Convertir(f.ConId(Gestion.SiguienteId("ALU", datos.Alumnos.Select(a => a.Id))));
        var errores = Gestion.ValidarAlumno(nuevo, datos.Alumnos);
        if (errores.Count > 0) return ConErrores("Form", f, errores);

        _repo.Guardar("alumnos", Gestion.Agregar(datos.Alumnos, nuevo));
        TempData["Mensaje"] = $"Alumno {nuevo.Nombre} registrado.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Editar(string id)
    {
        var a = _repo.Cargar().Alumnos.FirstOrDefault(x => x.Id == id);
        if (a is null) return NotFound();

        return View("Form", new AlumnoForm
        {
            Id = a.Id, Matricula = a.Matricula, Nombre = a.Nombre, Semestre = a.Semestre,
            Promedio = a.Promedio, Estatus = a.Estatus, Intereses = a.Intereses.ToList(), Editando = true
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Editar(string id, AlumnoForm f)
    {
        var datos = _repo.Cargar();
        if (!datos.Alumnos.Any(a => a.Id == id)) return NotFound();

        var cambiado = Convertir(f.ConId(id));
        var errores = Gestion.ValidarAlumno(cambiado, datos.Alumnos);
        if (errores.Count > 0) { f.Editando = true; return ConErrores("Form", f, errores); }

        _repo.Guardar("alumnos", Gestion.Reemplazar(datos.Alumnos, a => a.Id == id, cambiado));
        TempData["Mensaje"] = $"Cambios guardados para {cambiado.Nombre}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Eliminar(string id)
    {
        var datos = _repo.Cargar();
        var alumno = datos.Alumnos.FirstOrDefault(a => a.Id == id);
        if (alumno is null) return NotFound();

        // Baja en cascada: también se quita su historial.
        _repo.Guardar("historial", Gestion.Quitar(datos.Historial, h => h.AlumnoId == id));
        _repo.Guardar("alumnos", Gestion.Quitar(datos.Alumnos, a => a.Id == id));
        TempData["Mensaje"] = $"Se eliminó a {alumno.Nombre} y su historial.";
        return RedirectToAction(nameof(Index));
    }

    // ---------------------------------------------------------------- historial

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult AgregarHistorial(HistorialForm f)
    {
        var datos = _repo.Cargar();
        var nuevo = new Historial(
            Gestion.SiguienteId("HIS", datos.Historial.Select(h => h.Id)),
            f.AlumnoId, f.MateriaId, f.Periodo?.Trim() ?? "", f.Estatus == "cursando" ? null : f.Calificacion, f.Estatus);

        var errores = Gestion.ValidarHistorial(nuevo, datos);
        TempData[errores.Count > 0 ? "Error" : "Mensaje"] = errores.Count > 0
            ? string.Join(" ", errores)
            : "Materia registrada en el historial.";

        if (errores.Count == 0) _repo.Guardar("historial", Gestion.Agregar(datos.Historial, nuevo));
        return RedirectToAction(nameof(Detalle), new { id = f.AlumnoId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult QuitarHistorial(string alumnoId, string historialId)
    {
        var datos = _repo.Cargar();
        _repo.Guardar("historial", Gestion.Quitar(datos.Historial, h => h.Id == historialId && h.AlumnoId == alumnoId));
        TempData["Mensaje"] = "Registro de historial eliminado.";
        return RedirectToAction(nameof(Detalle), new { id = alumnoId });
    }

    // ---------------------------------------------------------------- utilidades

    private static Alumno Convertir(AlumnoForm f) => new(
        f.Id, (f.Matricula ?? "").Trim(), (f.Nombre ?? "").Trim(), f.Semestre, f.Promedio,
        (f.Intereses ?? new List<string>()).Where(i => Consultas.Areas.Contains(i)).Distinct().ToList(),
        f.Estatus ?? "");

    private IActionResult ConErrores(string vista, AlumnoForm f, IEnumerable<string> errores)
    {
        foreach (var e in errores) ModelState.AddModelError(string.Empty, e);
        return View(vista, f);
    }
}

internal static class AlumnoFormExtensiones
{
    // Copia del formulario con otro Id (sin mutar el original).
    public static AlumnoForm ConId(this AlumnoForm f, string id) => new()
    {
        Id = id, Matricula = f.Matricula, Nombre = f.Nombre, Semestre = f.Semestre, Promedio = f.Promedio,
        Estatus = f.Estatus, Intereses = f.Intereses, Editando = f.Editando
    };
}
