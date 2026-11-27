using Microsoft.AspNetCore.Mvc;
using SmartAdvisor.Core.Models;
using SmartAdvisor.Core.Services;
using SmartAdvisor.Web.Models;
using SmartAdvisor.Web.Services;

namespace SmartAdvisor.Web.Controllers;

public class EspecialidadesController : Controller
{
    private readonly RepositorioJson _repo;
    private readonly AnalizadorAcademico _analizador;

    public EspecialidadesController(RepositorioJson repo, AnalizadorAcademico analizador)
    {
        _repo = repo;
        _analizador = analizador;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var datos = _repo.Cargar();
        var resultado = await _analizador.AnalizarAsync(datos, ct);
        return View(new EspecialidadesIndexVm(datos, resultado.Analisis.Especialidades, resultado));
    }

    public IActionResult Crear() => View("Form", ConOpciones(new EspecialidadForm()));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Crear(EspecialidadForm f)
    {
        var datos = _repo.Cargar();
        var nueva = Convertir(f, SiguienteIdEspecialidad(datos.Especialidades));
        var errores = Gestion.ValidarEspecialidad(nueva, datos.Especialidades);
        if (errores.Count > 0) return ConErrores(f, errores);

        _repo.Guardar("especialidades", Gestion.Agregar(datos.Especialidades, nueva));
        TempData["Mensaje"] = $"Especialidad {nueva.Nombre} registrada.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Editar(string id)
    {
        var e = _repo.Cargar().Especialidades.FirstOrDefault(x => x.Id == id);
        if (e is null) return NotFound();

        return View("Form", ConOpciones(new EspecialidadForm
        {
            Id = e.Id, Nombre = e.Nombre, Descripcion = e.Descripcion, PromedioMinimo = e.PromedioMinimo,
            AreasClave = e.AreasClave.ToList(), MateriasIds = e.MateriasIds.ToList(),
            CertificacionesIds = e.CertificacionesIds.ToList(), CursosIds = e.CursosIds.ToList(), Editando = true
        }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Editar(string id, EspecialidadForm f)
    {
        var datos = _repo.Cargar();
        if (!datos.Especialidades.Any(e => e.Id == id)) return NotFound();

        var cambiada = Convertir(f, id);
        var errores = Gestion.ValidarEspecialidad(cambiada, datos.Especialidades);
        if (errores.Count > 0) { f.Editando = true; f.Id = id; return ConErrores(f, errores); }

        _repo.Guardar("especialidades", Gestion.Reemplazar(datos.Especialidades, e => e.Id == id, cambiada));
        TempData["Mensaje"] = $"Cambios guardados en {cambiada.Nombre}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Eliminar(string id)
    {
        var datos = _repo.Cargar();
        var esp = datos.Especialidades.FirstOrDefault(e => e.Id == id);
        if (esp is null) return NotFound();

        var motivo = Gestion.MotivoParaNoEliminarEspecialidad(datos, id);
        if (motivo is not null)
        {
            TempData["Error"] = $"No se puede eliminar {esp.Nombre}: {motivo}";
            return RedirectToAction(nameof(Index));
        }

        _repo.Guardar("especialidades", Gestion.Quitar(datos.Especialidades, e => e.Id == id));
        TempData["Mensaje"] = $"Se eliminó {esp.Nombre}.";
        return RedirectToAction(nameof(Index));
    }

    // Los ids de especialidad llevan siglas (ESP-IA, ESP-DES); las nuevas usan ESP-001, ESP-002...
    private static string SiguienteIdEspecialidad(IEnumerable<Especialidad> existentes) =>
        Gestion.SiguienteId("ESP", existentes.Select(e => e.Id));

    private static Especialidad Convertir(EspecialidadForm f, string id) => new(
        id, (f.Nombre ?? "").Trim(), (f.Descripcion ?? "").Trim(), f.PromedioMinimo,
        (f.AreasClave ?? new List<string>()).Where(a => Consultas.Areas.Contains(a)).Distinct().ToList(),
        (f.MateriasIds ?? new List<string>()).Distinct().ToList(),
        (f.CertificacionesIds ?? new List<string>()).Distinct().ToList(),
        (f.CursosIds ?? new List<string>()).Distinct().ToList());

    private EspecialidadForm ConOpciones(EspecialidadForm f)
    {
        var d = _repo.Cargar();
        f.OpcionesMaterias = d.Materias.OrderBy(m => m.Semestre).ThenBy(m => m.Nombre).ToList();
        f.OpcionesCertificaciones = d.Certificaciones.OrderBy(c => c.Nombre).ToList();
        f.OpcionesCursos = d.Cursos.OrderBy(c => c.Nombre).ToList();
        return f;
    }

    private IActionResult ConErrores(EspecialidadForm f, IEnumerable<string> errores)
    {
        foreach (var e in errores) ModelState.AddModelError(string.Empty, e);
        return View("Form", ConOpciones(f));
    }
}
