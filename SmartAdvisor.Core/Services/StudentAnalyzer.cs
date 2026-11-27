using SmartAdvisor.Core.Models;

namespace SmartAdvisor.Core.Services;

/// <summary>
/// Núcleo FUNCIONAL del proyecto. Todas las funciones son puras: no leen archivos, no usan
/// variables globales, no modifican sus argumentos y devuelven listas/records nuevos.
/// Los datos se procesan con LINQ (Select, Where, GroupBy, OrderBy, Join, Aggregate).
/// </summary>
public static class StudentAnalyzer
{
    public const decimal PesoIntereses = 0.40m;
    public const decimal PesoHistorial = 0.35m;
    public const decimal PesoPromedio = 0.25m;

    public static decimal Redondear(decimal x) => Math.Round(x, 1, MidpointRounding.AwayFromZero);

    // ------------------------------------------------------------------ historial

    /// <summary>[LINQ Where] Materias aprobadas (con calificación) del historial.</summary>
    public static IReadOnlyList<Historial> Aprobadas(IEnumerable<Historial> historial) =>
        historial.Where(h => h.Estatus == "aprobada" && h.Calificacion.HasValue).ToList();

    /// <summary>[LINQ Where] Historial de un alumno.</summary>
    public static IReadOnlyList<Historial> DeAlumno(IEnumerable<Historial> historial, string alumnoId) =>
        historial.Where(h => h.AlumnoId == alumnoId).ToList();

    // ------------------------------------------------------------------ indicadores

    /// <summary>
    /// [LINQ GroupBy + Join + Average] Promedio y avance por área.
    /// Parte de las materias del plan (GroupBy área) y cruza con las aprobadas (Join).
    /// <paramref name="alumnos"/> es cuántos alumnos representa el historial (1 = un alumno).
    /// </summary>
    public static IReadOnlyList<IndicadorArea> IndicadoresPorArea(
        IEnumerable<Materia> materias, IEnumerable<Historial> aprobadas, int alumnos)
    {
        var porMateria = aprobadas
            .Join(materias, h => h.MateriaId, m => m.Id, (h, m) => (m.Area, Nota: h.Calificacion!.Value))
            .ToList();

        return materias
            .GroupBy(m => m.Area)
            .Select(g =>
            {
                var notas = porMateria.Where(t => t.Area == g.Key).Select(t => t.Nota).ToList();
                var posibles = g.Count() * Math.Max(alumnos, 1);
                return new IndicadorArea(
                    g.Key,
                    notas.Count == 0 ? null : Redondear(notas.Average()),
                    notas.Count,
                    g.Count(),
                    Redondear(100m * notas.Count / posibles));
            })
            .OrderBy(i => i.Area)
            .ToList();
    }

    /// <summary>[LINQ Join + Aggregate] Créditos aprobados: suma acumulada con semilla 0.</summary>
    public static int CreditosAprobados(IEnumerable<Materia> materias, IEnumerable<Historial> aprobadas) =>
        aprobadas
            .Join(materias, h => h.MateriaId, m => m.Id, (_, m) => m.Creditos)
            .Aggregate(0, (acumulado, creditos) => acumulado + creditos);

    /// <summary>[LINQ Aggregate] Estadística (n, mínimo, máximo, suma) en una sola pasada.</summary>
    public static Estadistica EstadisticaDe(IEnumerable<decimal> valores) =>
        valores.Aggregate(Estadistica.Vacia, (acc, x) => acc.Agregar(x));

    /// <summary>[LINQ SelectMany + GroupBy + OrderBy] Cuántos alumnos tienen cada interés.</summary>
    public static IReadOnlyList<ConteoInteres> InteresesFrecuentes(IEnumerable<Alumno> alumnos) =>
        alumnos
            .SelectMany(a => a.Intereses.Distinct(StringComparer.OrdinalIgnoreCase))
            .GroupBy(i => i, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ConteoInteres(g.Key, g.Count()))
            .OrderByDescending(c => c.Alumnos).ThenBy(c => c.Interes)
            .ToList();

    /// <summary>[LINQ GroupBy] Alumnos por estatus.</summary>
    public static IReadOnlyList<ConteoEstatus> PorEstatus(IEnumerable<Alumno> alumnos) =>
        alumnos.GroupBy(a => a.Estatus)
               .Select(g => new ConteoEstatus(g.Key, g.Count()))
               .OrderBy(c => c.Estatus)
               .ToList();

    /// <summary>[LINQ OrderByDescending + Take] Los mejores promedios.</summary>
    public static IReadOnlyList<MejorPromedio> MejoresPromedios(IEnumerable<Alumno> alumnos, int cuantos = 3) =>
        alumnos
            .OrderByDescending(a => a.Promedio).ThenBy(a => a.Nombre)
            .Take(cuantos)
            .Select(a => new MejorPromedio(a.Id, a.Nombre, a.Semestre, a.Promedio,
                a.Intereses.FirstOrDefault() ?? "—"))
            .ToList();

    // ------------------------------------------------------------------ recomendaciones

    /// <summary>
    /// Afinidad(e) = 100 × (0.40·I + 0.35·H + 0.25·P)
    /// I: proporción de intereses del alumno que coinciden con las áreas clave de la especialidad.
    /// H: promedio de sus materias aprobadas de esas áreas, entre 100 (0 si no tiene).
    /// P: promedio general entre 100.
    /// </summary>
    public static decimal Afinidad(Alumno alumno, Especialidad esp, IEnumerable<Materia> materias, IEnumerable<Historial> aprobadas)
    {
        var i = ProporcionIntereses(alumno, esp);

        var notasDelArea = aprobadas
            .Join(materias, h => h.MateriaId, m => m.Id, (h, m) => (m.Area, Nota: h.Calificacion!.Value))
            .Where(t => esp.AreasClave.Contains(t.Area, StringComparer.OrdinalIgnoreCase))
            .Select(t => t.Nota)
            .ToList();
        var h2 = notasDelArea.Count == 0 ? 0m : notasDelArea.Average() / 100m;

        var p = alumno.Promedio / 100m;
        return Redondear(100m * (PesoIntereses * i + PesoHistorial * h2 + PesoPromedio * p));
    }

    private static decimal ProporcionIntereses(Alumno alumno, Especialidad esp) =>
        alumno.Intereses.Count == 0
            ? 0m
            : (decimal)alumno.Intereses.Count(x => esp.AreasClave.Contains(x, StringComparer.OrdinalIgnoreCase))
              / alumno.Intereses.Count;

    /// <summary>
    /// Recomendación básica: las reglas filtran (evaluadas con EvaluadorReglas) y la afinidad ordena.
    /// Si ninguna regla se cumple, se propone la mejor afinidad con interés coincidente como "condicionada".
    /// </summary>
    public static RecomendacionEspecialidad Recomendar(Alumno alumno, DatosAcademicos d, IReadOnlyList<Historial> aprobadasDelAlumno)
    {
        var hechos = new Hechos(alumno.Promedio, alumno.Intereses);
        var reglasDeEspecialidad = d.Reglas.Where(EvaluadorReglas.EsReglaDeEspecialidad).ToList();

        var ranking = d.Especialidades
            .Select(e =>
            {
                var regla = reglasDeEspecialidad.FirstOrDefault(r => r.Conclusion.EspecialidadId == e.Id && EvaluadorReglas.Cumple(r, hechos));
                return new AfinidadEspecialidad(
                    e.Id, e.Nombre,
                    Afinidad(alumno, e, d.Materias, aprobadasDelAlumno),
                    ProporcionIntereses(alumno, e) > 0,
                    regla is not null);
            })
            .OrderByDescending(a => a.Afinidad).ThenBy(a => a.Nombre)
            .ToList();

        var elegida = ranking.FirstOrDefault(a => a.CumpleRegla);
        if (elegida is not null)
        {
            var regla = reglasDeEspecialidad.First(r => r.Conclusion.EspecialidadId == elegida.EspecialidadId && EvaluadorReglas.Cumple(r, hechos));
            return new RecomendacionEspecialidad(elegida.EspecialidadId, elegida.Nombre, elegida.Afinidad, "recomendada",
                regla.Nombre, $"Cumple la regla {regla.Nombre} con promedio {alumno.Promedio:0.0}.", ranking);
        }

        var candidata = ranking.FirstOrDefault(a => a.CoincideInteres);
        if (candidata is not null)
        {
            var esp = d.Especialidades.First(e => e.Id == candidata.EspecialidadId);
            var faltan = esp.PromedioMinimo - alumno.Promedio;
            var motivo = faltan > 0
                ? $"Le faltan {faltan:0.0} puntos de promedio (mínimo {esp.PromedioMinimo:0.#})."
                : "No cumple todas las condiciones de la regla.";
            return new RecomendacionEspecialidad(candidata.EspecialidadId, candidata.Nombre, candidata.Afinidad, "condicionada", null, motivo, ranking);
        }

        return new RecomendacionEspecialidad(null, null, ranking.Count == 0 ? 0 : ranking[0].Afinidad, "sin-recomendacion", null,
            "Sus intereses no coinciden con ninguna especialidad.", ranking);
    }

    /// <summary>
    /// [LINQ Join + Where + OrderBy] Materias de la especialidad que el alumno aún no aprueba ni cursa
    /// y cuyos prerrequisitos YA aprobó.
    /// </summary>
    public static IReadOnlyList<MateriaSugerida> MateriasSugeridas(
        Especialidad? esp, IEnumerable<Materia> materias, IEnumerable<Historial> historialAlumno)
    {
        if (esp is null) return Array.Empty<MateriaSugerida>();

        var lista = historialAlumno.ToList();
        var aprobadas = lista.Where(h => h.Estatus == "aprobada").Select(h => h.MateriaId).ToHashSet();
        var cursando = lista.Where(h => h.Estatus == "cursando").Select(h => h.MateriaId).ToHashSet();

        return esp.MateriasIds
            .Join(materias, id => id, m => m.Id, (_, m) => m)
            .Where(m => !aprobadas.Contains(m.Id) && !cursando.Contains(m.Id))
            .Where(m => m.Prerrequisitos.All(aprobadas.Contains))
            .OrderBy(m => m.Semestre).ThenBy(m => m.Nombre)
            .Select(m => new MateriaSugerida(m.Id, m.Nombre, m.Area, m.Creditos, m.Semestre,
                m.Prerrequisitos.Count == 0
                    ? "No tiene prerrequisitos."
                    : "Prerrequisitos aprobados: " + string.Join(", ", m.Prerrequisitos) + "."))
            .ToList();
    }

    // ------------------------------------------------------------------ análisis completo

    /// <summary>Análisis de un alumno: indicadores, avance, recomendación y materias sugeridas.</summary>
    public static AnalisisAlumno AnalizarAlumno(Alumno alumno, DatosAcademicos d)
    {
        var historial = DeAlumno(d.Historial, alumno.Id);
        var aprobadas = Aprobadas(historial);
        var creditosPlan = d.Materias.Sum(m => m.Creditos);
        var creditos = CreditosAprobados(d.Materias, aprobadas);

        var recomendacion = Recomendar(alumno, d, aprobadas);
        var esp = d.Especialidades.FirstOrDefault(e => e.Id == recomendacion.EspecialidadId);

        return new AnalisisAlumno(
            alumno.Id, alumno.Matricula, alumno.Nombre, alumno.Semestre, alumno.Promedio, alumno.Estatus, alumno.Intereses,
            aprobadas.Count, historial.Count(h => h.Estatus == "cursando"),
            creditos, creditosPlan, creditosPlan == 0 ? 0 : Redondear(100m * creditos / creditosPlan),
            IndicadoresPorArea(d.Materias, aprobadas, 1),
            recomendacion,
            MateriasSugeridas(esp, d.Materias, historial),
            esp is null ? Array.Empty<string>() : d.Certificaciones.Where(c => esp.CertificacionesIds.Contains(c.Id)).Select(c => c.Nombre).ToList(),
            esp is null ? Array.Empty<string>() : d.Cursos.Where(c => esp.CursosIds.Contains(c.Id)).Select(c => c.Nombre).ToList());
    }

    /// <summary>Análisis académico completo: lo que devuelve StudentAnalyzerFunction.</summary>
    public static AnalisisAcademico Analizar(DatosAcademicos d)
    {
        var alumnos = d.Alumnos.Select(a => AnalizarAlumno(a, d)).ToList();
        var estadistica = EstadisticaDe(d.Alumnos.Select(a => a.Promedio));

        var resumen = new ResumenGeneral(
            d.Alumnos.Count,
            d.Alumnos.Count(a => a.Estatus == "activo"),
            d.Materias.Count,
            d.Especialidades.Count,
            d.Cursos.Count,
            d.Certificaciones.Count,
            d.Materias.Sum(m => m.Creditos),
            estadistica.Promedio,
            alumnos.Count(a => a.Recomendacion.Estado == "sin-recomendacion"));

        var porEspecialidad = d.Especialidades
            .Select(e => new ResumenEspecialidad(
                e.Id, e.Nombre, e.PromedioMinimo,
                d.Alumnos.Count(a => a.Intereses.Any(i => e.AreasClave.Contains(i, StringComparer.OrdinalIgnoreCase))),
                alumnos.Count(a => a.Recomendacion.EspecialidadId == e.Id && a.Recomendacion.Estado == "recomendada"),
                alumnos.Count(a => a.Recomendacion.EspecialidadId == e.Id && a.Recomendacion.Estado == "condicionada")))
            .OrderBy(e => e.Nombre)
            .ToList();

        return new AnalisisAcademico(
            resumen,
            estadistica,
            IndicadoresPorArea(d.Materias, Aprobadas(d.Historial), d.Alumnos.Count),
            InteresesFrecuentes(d.Alumnos),
            PorEstatus(d.Alumnos),
            MejoresPromedios(d.Alumnos),
            porEspecialidad,
            alumnos);
    }
}
