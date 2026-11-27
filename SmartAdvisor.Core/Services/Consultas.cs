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

    /// <summary>[LINQ Where + OrderBy] Materias que imparte un docente, ordenadas por semestre.</summary>
    public static IReadOnlyList<Materia> MateriasDeDocente(Docente docente, IEnumerable<Materia> materias) =>
        materias
            .Where(m => docente.MateriasIds.Contains(m.Id))
            .OrderBy(m => m.Semestre).ThenBy(m => m.Nombre)
            .ToList();

    /// <summary>[LINQ Join + OrderBy] Registros del historial de una materia con su alumno (lista del docente).</summary>
    public static IReadOnlyList<(Historial Registro, Alumno Alumno)> AlumnosDeMateria(
        IEnumerable<Historial> historial, IEnumerable<Alumno> alumnos, string materiaId) =>
        historial
            .Where(h => h.MateriaId == materiaId)
            .Join(alumnos, h => h.AlumnoId, a => a.Id, (h, a) => (Registro: h, Alumno: a))
            .OrderBy(t => t.Alumno.Nombre)
            .ToList();

    /// <summary>[LINQ Where + All] Alumnos activos que se pueden inscribir: no la tienen aprobada ni en curso.</summary>
    public static IReadOnlyList<Alumno> AlumnosInscribibles(
        IEnumerable<Alumno> alumnos, IEnumerable<Historial> historial, string materiaId)
    {
        var lista = historial.Where(h => h.MateriaId == materiaId).ToList();
        return alumnos
            .Where(a => a.Estatus == "activo")
            .Where(a => lista.All(h => h.AlumnoId != a.Id || h.Estatus == "reprobada"))
            .OrderBy(a => a.Nombre)
            .ToList();
    }
}
