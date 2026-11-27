using Microsoft.AspNetCore.Mvc;

namespace SmartAdvisor.Web.Controllers;

/// <summary>Sección "Nosotros": el equipo, la materia y cómo está construido el sistema.</summary>
[Route("nosotros")]
public class NosotrosController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
