namespace SmartAdvisor.Core.Models;

// Resultados del análisis (salida de StudentAnalyzer). También inmutables.

/// <summary>Estadística acumulada con Aggregate: cuenta, mínimo, máximo y suma.</summary>
public sealed record Estadistica(int N, decimal Min, decimal Max, decimal Suma)
{
    public static readonly Estadistica Vacia = new(0, 0, 0, 0);

    public decimal Promedio => N == 0 ? 0 : Math.Round(Suma / N, 1, MidpointRounding.AwayFromZero);

    /// <summary>Paso del Aggregate: devuelve una estadística NUEVA que incluye el valor x.</summary>
    public Estadistica Agregar(decimal x) => N == 0
        ? new Estadistica(1, x, x, x)
        : new Estadistica(N + 1, Math.Min(Min, x), Math.Max(Max, x), Suma + x);
}

public sealed record ResumenGeneral(
    int TotalAlumnos, int AlumnosActivos, int TotalMaterias, int TotalEspecialidades,
    int TotalCursos, int TotalCertificaciones, int CreditosPlan, decimal PromedioGeneral, int SinRecomendacion);

/// <summary>Indicador por área: promedio de calificaciones aprobadas y avance sobre las materias del plan.</summary>
public sealed record IndicadorArea(string Area, decimal? Promedio, int Aprobadas, int MateriasPlan, decimal AvancePct);

public sealed record ConteoInteres(string Interes, int Alumnos);

public sealed record ConteoEstatus(string Estatus, int Alumnos);

public sealed record MejorPromedio(string AlumnoId, string Nombre, int Semestre, decimal Promedio, string InteresPrincipal);

public sealed record AfinidadEspecialidad(
    string EspecialidadId, string Nombre, decimal Afinidad, bool CoincideInteres, bool CumpleRegla);

/// <summary>Estado: "recomendada", "condicionada" o "sin-recomendacion".</summary>
public sealed record RecomendacionEspecialidad(
    string? EspecialidadId, string? Especialidad, decimal Afinidad, string Estado,
    string? ReglaAplicada, string Motivo, IReadOnlyList<AfinidadEspecialidad> Ranking);

public sealed record MateriaSugerida(string MateriaId, string Nombre, string Area, int Creditos, int Semestre, string Motivo);

public sealed record AnalisisAlumno(
    string AlumnoId, string Matricula, string Nombre, int Semestre, decimal Promedio, string Estatus,
    IReadOnlyList<string> Intereses, int MateriasAprobadas, int MateriasCursando,
    int CreditosAprobados, int CreditosPlan, decimal AvancePct,
    IReadOnlyList<IndicadorArea> PorArea, RecomendacionEspecialidad Recomendacion,
    IReadOnlyList<MateriaSugerida> MateriasSugeridas,
    IReadOnlyList<string> Certificaciones, IReadOnlyList<string> Cursos);

public sealed record ResumenEspecialidad(
    string EspecialidadId, string Nombre, decimal PromedioMinimo,
    int AlumnosInteresados, int Recomendados, int Condicionados);

public sealed record AnalisisAcademico(
    ResumenGeneral Resumen,
    Estadistica EstadisticaPromedios,
    IReadOnlyList<IndicadorArea> PorArea,
    IReadOnlyList<ConteoInteres> Intereses,
    IReadOnlyList<ConteoEstatus> PorEstatus,
    IReadOnlyList<MejorPromedio> MejoresPromedios,
    IReadOnlyList<ResumenEspecialidad> Especialidades,
    IReadOnlyList<AnalisisAlumno> Alumnos);
