using Microsoft.AspNetCore.Mvc;
using SmartAdvisor.Core.Models;
using SmartAdvisor.Core.Services;
using SmartAdvisor.Web.Models;

namespace SmartAdvisor.Web.Controllers;

public class MateriasController : Controller
{
    private readonly RepositorioJson _repo;

    public MateriasController(RepositorioJson repo) => _repo = repo;

    // GET /Materias?texto=datos&area=Datos
    public IActionResult Index(string? texto, string? area)
    {
        var datos = _repo.Cargar();
        var lista = Consultas.FiltrarMaterias(datos.Materias, texto, area);
        return View(new MateriasIndexVm(lista, texto, area, datos.Materias.Count, datos.Materias.Sum(m => m.Creditos)));
    }

    public IActionResult Crear() => View("Form", ConOpciones(new MateriaForm(), null));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Crear(MateriaForm f)
    {
        var datos = _repo.Cargar();
        var nueva = Convertir(f, Gestion.SiguienteId("MAT", datos.Materias.Select(m => m.Id)));
        var errores = Gestion.ValidarMateria(nueva, datos.Materias);
        if (errores.Count > 0) return ConErrores(f, null, errores);

        _repo.Guardar("materias", Gestion.Agregar(datos.Materias, nueva));
        TempData["Mensaje"] = $"Materia {nueva.Nombre} registrada.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Editar(string id)
    {
        var m = _repo.Cargar().Materias.FirstOrDefault(x => x.Id == id);
        if (m is null) return NotFound();

        var f = new MateriaForm
        {
            Id = m.Id, Clave = m.Clave, Nombre = m.Nombre, Creditos = m.Creditos, Semestre = m.Semestre,
            Area = m.Area, Tipo = m.Tipo, Prerrequisitos = m.Prerrequisitos.ToList(), Editando = true
        };
        return View("Form", ConOpciones(f, id));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Editar(string id, MateriaForm f)
    {
        var datos = _repo.Cargar();
        if (!datos.Materias.Any(m => m.Id == id)) return NotFound();

        var cambiada = Convertir(f, id);
        var errores = Gestion.ValidarMateria(cambiada, datos.Materias);
        if (errores.Count > 0) { f.Editando = true; f.Id = id; return ConErrores(f, id, errores); }

        _repo.Guardar("materias", Gestion.Reemplazar(datos.Materias, m => m.Id == id, cambiada));
        TempData["Mensaje"] = $"Cambios guardados en {cambiada.Nombre}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Eliminar(string id)
    {
        var datos = _repo.Cargar();
        var materia = datos.Materias.FirstOrDefault(m => m.Id == id);
        if (materia is null) return NotFound();

        var motivo = Gestion.MotivoParaNoEliminarMateria(datos, id);
        if (motivo is not null)
        {
            TempData["Error"] = $"No se puede eliminar {materia.Nombre}: {motivo}";
            return RedirectToAction(nameof(Index));
        }

        _repo.Guardar("materias", Gestion.Quitar(datos.Materias, m => m.Id == id));
        TempData["Mensaje"] = $"Se eliminó {materia.Nombre}.";
        return RedirectToAction(nameof(Index));
    }

    private static Materia Convertir(MateriaForm f, string id) => new(
        id, (f.Clave ?? "").Trim(), (f.Nombre ?? "").Trim(), f.Creditos, f.Semestre,
        f.Area ?? "", f.Tipo ?? "", (f.Prerrequisitos ?? new List<string>()).Distinct().ToList());

    private MateriaForm ConOpciones(MateriaForm f, string? excluirId)
    {
        f.Opciones = _repo.Cargar().Materias.Where(m => m.Id != excluirId).OrderBy(m => m.Semestre).ThenBy(m => m.Nombre).ToList();
        return f;
    }

    private IActionResult ConErrores(MateriaForm f, string? excluirId, IEnumerable<string> errores)
    {
        foreach (var e in errores) ModelState.AddModelError(string.Empty, e);
        return View("Form", ConOpciones(f, excluirId));
    }
}
