using System.Text.Json;

namespace SmartAdvisor.Core.Models;

// Entidades del dominio (Parcial 1). Son records inmutables: una vez creados no se modifican;
// para "cambiar" algo se crea una copia con `with` (estilo funcional).

public sealed record Alumno(
    string Id, string Matricula, string Nombre, int Semestre, decimal Promedio,
    IReadOnlyList<string> Intereses, string Estatus);

public sealed record Materia(
    string Id, string Clave, string Nombre, int Creditos, int Semestre,
    string Area, string Tipo, IReadOnlyList<string> Prerrequisitos);

public sealed record Especialidad(
    string Id, string Nombre, string Descripcion, decimal PromedioMinimo,
    IReadOnlyList<string> AreasClave, IReadOnlyList<string> MateriasIds,
    IReadOnlyList<string> CertificacionesIds, IReadOnlyList<string> CursosIds);

public sealed record Curso(string Id, string Nombre, string Plataforma, int Horas, string Nivel, string Area);

public sealed record Certificacion(string Id, string Nombre, string Emisor, string Area, string Nivel, decimal CostoMXN);

/// <summary>Una materia cursada por un alumno. Calificacion es null mientras está "cursando".</summary>
public sealed record Historial(
    string Id, string AlumnoId, string MateriaId, string Periodo, decimal? Calificacion, string Estatus);

public sealed record Condicion(string Hecho, string Operador, JsonElement Valor);

public sealed record ConclusionRegla(string Tipo, string? EspecialidadId);

public sealed record Regla(string Id, string Nombre, IReadOnlyList<Condicion> Condiciones, ConclusionRegla Conclusion, decimal Peso);

/// <summary>
/// Foto completa de las siete colecciones JSON. Es la entrada de StudentAnalyzerFunction:
/// la función no lee archivos, recibe los datos y devuelve el análisis (función pura).
/// </summary>
public sealed record DatosAcademicos(
    IReadOnlyList<Alumno> Alumnos,
    IReadOnlyList<Materia> Materias,
    IReadOnlyList<Especialidad> Especialidades,
    IReadOnlyList<Curso> Cursos,
    IReadOnlyList<Certificacion> Certificaciones,
    IReadOnlyList<Historial> Historial,
    IReadOnlyList<Regla> Reglas);
