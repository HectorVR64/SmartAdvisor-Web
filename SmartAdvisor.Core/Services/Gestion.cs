using SmartAdvisor.Core.Models;

namespace SmartAdvisor.Core.Services;

/// <summary>
/// Altas, cambios y bajas SIN mutar listas: cada operación devuelve una lista nueva.
/// También las validaciones: devuelven la lista de errores (vacía = válido).
/// </summary>
public static class Gestion
{
    // ---------- operaciones genéricas sobre listas (funciones de orden superior)

    public static IReadOnlyList<T> Agregar<T>(IReadOnlyList<T> lista, T nuevo) => lista.Append(nuevo).ToList();

    public static IReadOnlyList<T> Reemplazar<T>(IReadOnlyList<T> lista, Func<T, bool> esElQueCambia, T nuevo) =>
        lista.Select(x => esElQueCambia(x) ? nuevo : x).ToList();

    public static IReadOnlyList<T> Quitar<T>(IReadOnlyList<T> lista, Func<T, bool> sobra) =>
        lista.Where(x => !sobra(x)).ToList();

    /// <summary>Siguiente id con prefijo: ("ALU", ["ALU-001","ALU-004"]) => "ALU-005".</summary>
    public static string SiguienteId(string prefijo, IEnumerable<string> ids)
    {
        var maximo = ids
            .Select(id => id.Split('-').LastOrDefault())
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefijo}-{maximo + 1:000}";
    }

    // ---------- validaciones

    public static IReadOnlyList<string> ValidarAlumno(Alumno a, IEnumerable<Alumno> otros)
    {
        var errores = new List<string>();
        if (string.IsNullOrWhiteSpace(a.Nombre)) errores.Add("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(a.Matricula)) errores.Add("La matrícula es obligatoria.");
        else if (otros.Any(o => o.Id != a.Id && o.Matricula == a.Matricula)) errores.Add("Ya existe un alumno con esa matrícula.");
        if (a.Semestre is < 1 or > 9) errores.Add("El semestre debe estar entre 1 y 9.");
        if (a.Promedio is < 0 or > 100) errores.Add("El promedio debe estar entre 0 y 100.");
        if (a.Intereses.Count == 0) errores.Add("Elige al menos un interés.");
        if (!Consultas.EstatusAlumno.Contains(a.Estatus)) errores.Add("Estatus no válido.");
        return errores;
    }

    public static IReadOnlyList<string> ValidarMateria(Materia m, IEnumerable<Materia> otras)
    {
        var errores = new List<string>();
        if (string.IsNullOrWhiteSpace(m.Nombre)) errores.Add("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(m.Clave)) errores.Add("La clave es obligatoria.");
        else if (otras.Any(o => o.Id != m.Id && o.Clave == m.Clave)) errores.Add("Ya existe una materia con esa clave.");
        if (m.Creditos <= 0) errores.Add("Los créditos deben ser mayores que 0.");
        if (m.Semestre is < 1 or > 9) errores.Add("El semestre debe estar entre 1 y 9.");
        if (!Consultas.Areas.Contains(m.Area)) errores.Add("Área no válida.");
        if (!Consultas.TiposMateria.Contains(m.Tipo)) errores.Add("Tipo no válido.");
        if (m.Prerrequisitos.Contains(m.Id)) errores.Add("Una materia no puede ser prerrequisito de sí misma.");
        return errores;
    }

    public static IReadOnlyList<string> ValidarEspecialidad(Especialidad e, IEnumerable<Especialidad> otras)
    {
        var errores = new List<string>();
        if (string.IsNullOrWhiteSpace(e.Nombre)) errores.Add("El nombre es obligatorio.");
        else if (otras.Any(o => o.Id != e.Id && string.Equals(o.Nombre, e.Nombre, StringComparison.OrdinalIgnoreCase))) errores.Add("Ya existe una especialidad con ese nombre.");
        if (e.PromedioMinimo is < 0 or > 100) errores.Add("El promedio mínimo debe estar entre 0 y 100.");
        if (e.AreasClave.Count == 0) errores.Add("Elige al menos un área clave.");
        return errores;
    }

    /// <summary>Valida un registro de historial nuevo. Aprobada exige calificación >= 70; cursando no lleva calificación.</summary>
    public static IReadOnlyList<string> ValidarHistorial(Historial h, DatosAcademicos d)
    {
        var errores = new List<string>();
        if (!d.Alumnos.Any(a => a.Id == h.AlumnoId)) errores.Add("El alumno no existe.");
        if (!d.Materias.Any(m => m.Id == h.MateriaId)) errores.Add("La materia no existe.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(h.Periodo ?? "", @"^\d{4}-[12]$")) errores.Add("El periodo debe tener el formato AAAA-1 o AAAA-2.");
        if (!Consultas.EstatusHistorial.Contains(h.Estatus)) errores.Add("Estatus no válido.");
        if (h.Estatus == "cursando" && h.Calificacion is not null) errores.Add("Una materia en curso no lleva calificación.");
        if (h.Estatus != "cursando")
        {
            if (h.Calificacion is null or < 0 or > 100) errores.Add("La calificación debe estar entre 0 y 100.");
            else if (h.Estatus == "aprobada" && h.Calificacion < 70) errores.Add("Una materia aprobada requiere calificación de 70 o más.");
            else if (h.Estatus == "reprobada" && h.Calificacion >= 70) errores.Add("Una materia reprobada debe tener menos de 70.");
        }
        if (d.Historial.Any(x => x.AlumnoId == h.AlumnoId && x.MateriaId == h.MateriaId && x.Estatus is "aprobada" or "cursando"))
            errores.Add("Esa materia ya está aprobada o en curso para el alumno.");
        return errores;
    }

    // ---------- integridad referencial al eliminar

    /// <summary>Una materia no se elimina si es prerrequisito, está en una especialidad o tiene historial.</summary>
    public static string? MotivoParaNoEliminarMateria(DatosAcademicos d, string materiaId)
    {
        if (d.Materias.Any(m => m.Prerrequisitos.Contains(materiaId))) return "Es prerrequisito de otra materia.";
        if (d.Especialidades.Any(e => e.MateriasIds.Contains(materiaId))) return "Pertenece a una especialidad.";
        if (d.Historial.Any(h => h.MateriaId == materiaId)) return "Tiene historial de alumnos.";
        return null;
    }

    /// <summary>Una especialidad no se elimina si una regla concluye con ella.</summary>
    public static string? MotivoParaNoEliminarEspecialidad(DatosAcademicos d, string especialidadId) =>
        d.Reglas.Any(r => r.Conclusion.EspecialidadId == especialidadId) ? "Una regla de reglas.json la utiliza." : null;
}
