using SmartAdvisor.Core.Models;
using SmartAdvisor.Web.Services;

namespace SmartAdvisor.Web.Models;

public sealed record DashboardVm(ResultadoAnalisis Resultado, DatosAcademicos Datos);

public sealed record AlumnosIndexVm(
    IReadOnlyList<Alumno> Alumnos,
    IReadOnlyDictionary<string, RecomendacionEspecialidad> Recomendaciones,
    string? Texto, string? Interes, string? Estatus, int Total, ResultadoAnalisis Resultado);

public sealed class AlumnoForm
{
    public string Id { get; set; } = "";
    public string Matricula { get; set; } = "";
    public string Nombre { get; set; } = "";
    public int Semestre { get; set; } = 1;
    public decimal Promedio { get; set; }
    public string Estatus { get; set; } = "activo";
    public List<string> Intereses { get; set; } = new();
    public bool Editando { get; set; }
}

public sealed class HistorialForm
{
    public string AlumnoId { get; set; } = "";
    public string MateriaId { get; set; } = "";
    public string Periodo { get; set; } = "2026-2";
    public decimal? Calificacion { get; set; }
    public string Estatus { get; set; } = "aprobada";
}

public sealed record DetalleAlumnoVm(
    AnalisisAlumno Analisis,
    IReadOnlyList<(Historial Registro, string Materia, string Area)> Historial,
    IReadOnlyList<Materia> Materias,
    HistorialForm Form,
    ResultadoAnalisis Resultado);

public sealed record MateriasIndexVm(IReadOnlyList<Materia> Materias, string? Texto, string? Area, int Total, int CreditosPlan);

public sealed class MateriaForm
{
    public string Id { get; set; } = "";
    public string Clave { get; set; } = "";
    public string Nombre { get; set; } = "";
    public int Creditos { get; set; } = 5;
    public int Semestre { get; set; } = 1;
    public string Area { get; set; } = "Desarrollo";
    public string Tipo { get; set; } = "obligatoria";
    public List<string> Prerrequisitos { get; set; } = new();
    public bool Editando { get; set; }
    public List<Materia> Opciones { get; set; } = new();
}

public sealed record EspecialidadesIndexVm(DatosAcademicos Datos, IReadOnlyList<ResumenEspecialidad> Resumen, ResultadoAnalisis Resultado);

public sealed class EspecialidadForm
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal PromedioMinimo { get; set; } = 80;
    public List<string> AreasClave { get; set; } = new();
    public List<string> MateriasIds { get; set; } = new();
    public List<string> CertificacionesIds { get; set; } = new();
    public List<string> CursosIds { get; set; } = new();
    public bool Editando { get; set; }
    public List<Materia> OpcionesMaterias { get; set; } = new();
    public List<Certificacion> OpcionesCertificaciones { get; set; } = new();
    public List<Curso> OpcionesCursos { get; set; } = new();
}
