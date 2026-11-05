using SmartAdvisor.Core.Models;

namespace SmartAdvisor.Core.Services;

/// <summary>Consultas LINQ de las pantallas de gestión (filtros y ordenamientos). Todas son puras.</summary>
public static class Consultas
{
    public static readonly IReadOnlyList<string> Areas = new[] { "IA", "Desarrollo", "Datos", "Redes", "Seguridad" };
    public static readonly IReadOnlyList<string> EstatusAlumno = new[] { "activo", "baja", "egresado" };
    public static readonly IReadOnlyList<string> TiposMateria = new[] { "obligatoria", "optativa" };
    public static readonly IReadOnlyList<string> EstatusHistorial = new[] { "aprobada", "reprobada", "cursando" };

    /// <summary>[LINQ Where + OrderBy] Búsqueda de alumnos por texto, interés y estatus.</summary>
    public static IReadOnlyList<Alumno> FiltrarAlumnos(IEnumerable<Alumno> alumnos, string? texto, string? interes, string? estatus) =>
        alumnos
            .Where(a => string.IsNullOrWhiteSpace(texto)
                        || a.Nombre.Contains(texto.Trim(), StringComparison.OrdinalIgnoreCase)
                        || a.Matricula.Contains(texto.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(a => string.IsNullOrWhiteSpace(interes)
                        || a.Intereses.Contains(interes, StringComparer.OrdinalIgnoreCase))
            .Where(a => string.IsNullOrWhiteSpace(estatus) || a.Estatus == estatus)
            .OrderBy(a => a.Nombre)
            .ToList();

    /// <summary>[LINQ Where + OrderBy] Materias por área y texto, ordenadas por semestre.</summary>
    public static IReadOnlyList<Materia> FiltrarMaterias(IEnumerable<Materia> materias, string? texto, string? area) =>
        materias
            .Where(m => string.IsNullOrWhiteSpace(area) || m.Area == area)
            .Where(m => string.IsNullOrWhiteSpace(texto)
                        || m.Nombre.Contains(texto.Trim(), StringComparison.OrdinalIgnoreCase)
                        || m.Clave.Contains(texto.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.Semestre).ThenBy(m => m.Nombre)
            .ToList();

    /// <summary>[LINQ Join + OrderBy + Select] Historial de un alumno con el nombre de cada materia.</summary>
    public static IReadOnlyList<(Historial Registro, string Materia, string Area)> HistorialConMateria(
        IEnumerable<Historial> historial, IEnumerable<Materia> materias, string alumnoId) =>
        historial
            .Where(h => h.AlumnoId == alumnoId)
            .Join(materias, h => h.MateriaId, m => m.Id, (h, m) => (Registro: h, Materia: m.Nombre, Area: m.Area))
            .OrderBy(t => t.Registro.Periodo).ThenBy(t => t.Materia)
            .ToList();
}
