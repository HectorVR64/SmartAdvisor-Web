using Microsoft.AspNetCore.Mvc;
using SmartAdvisor.Core.Services;
using SmartAdvisor.Web.Models;
using SmartAdvisor.Web.Services;

namespace SmartAdvisor.Web.Controllers;

/// <summary>Dashboard académico: pide el análisis (Azure Function o local) y lo entrega a la vista.</summary>
public class HomeController : Controller
{
    private readonly RepositorioJson _repo;
    private readonly AnalizadorAcademico _analizador;

    public HomeController(RepositorioJson repo, AnalizadorAcademico analizador)
    {
        _repo = repo;
        _analizador = analizador;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var datos = _repo.Cargar();
        var resultado = await _analizador.AnalizarAsync(datos, ct);
        return View(new DashboardVm(resultado, datos));
    }

    // GET /Home/Analisis -> el análisis completo en JSON (útil para revisar lo que produce el LINQ)
    [HttpGet]
    public async Task<IActionResult> Analisis(CancellationToken ct)
    {
        var resultado = await _analizador.AnalizarAsync(_repo.Cargar(), ct);
        return Content(JsonConfig.Serializar(resultado.Analisis), "application/json; charset=utf-8");
    }

    public IActionResult Error() => Content("Ocurrió un error al procesar la solicitud.");
}
