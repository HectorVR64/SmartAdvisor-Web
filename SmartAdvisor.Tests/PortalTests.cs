using SmartAdvisor.Core.Models;
using SmartAdvisor.Core.Services;
using Xunit;

namespace SmartAdvisor.Tests;

/// <summary>Pruebas de lo que agrega el portal del docente: calificar, validar y consultar por materia.</summary>
public class PortalTests
{
    private static DatosAcademicos Datos() =>
        new RepositorioJson(Path.Combine(AppContext.BaseDirectory, "Data")).Cargar();

    private static Historial EnCurso() => new("HIS-X", "ALU-002", "MAT-340", "2026-1", null, "cursando");

    [Theory]
    [InlineData(70, "aprobada")]
    [InlineData(88.5, "aprobada")]
    [InlineData(69.5, "reprobada")]
    [InlineData(0, "reprobada")]
    public void Calificar_asigna_el_estatus_segun_la_calificacion(double nota, string estatus)
    {
        var r = Gestion.Calificar(EnCurso(), (decimal)nota);
        Assert.Equal(estatus, r.Estatus);
        Assert.Equal((decimal)nota, r.Calificacion);
    }

    [Fact]
    public void Calificar_devuelve_una_copia_y_no_modifica_el_original()
    {
        var original = EnCurso();
        var copia = Gestion.Calificar(original, 90m);
        Assert.Null(original.Calificacion);
        Assert.Equal("cursando", original.Estatus);
        Assert.NotEqual(original, copia);
        Assert.Equal(original.Id, copia.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1.0)]
    [InlineData(100.5)]
    public void ValidarCalificacion_rechaza_valores_fuera_de_0_a_100(double? nota)
    {
        Assert.Single(Gestion.ValidarCalificacion(nota is null ? null : (decimal)nota.Value));
    }

    [Fact]
    public void ValidarCalificacion_acepta_los_extremos()
    {
        Assert.Empty(Gestion.ValidarCalificacion(0m));
        Assert.Empty(Gestion.ValidarCalificacion(100m));
    }

    [Fact]
    public void AlumnosDeMateria_une_historial_y_alumnos()
    {
        var d = Datos();
        var lista = Consultas.AlumnosDeMateria(d.Historial, d.Alumnos, "MAT-340");
        var fila = Assert.Single(lista);
        Assert.Equal("Luis Mendoza Cruz", fila.Alumno.Nombre);
        Assert.Equal("cursando", fila.Registro.Estatus);
    }

    [Fact]
    public void AlumnosInscribibles_excluye_a_quien_ya_cursa_o_aprobo_la_materia()
    {
        var d = Datos();
        var ids = Consultas.AlumnosInscribibles(d.Alumnos, d.Historial, "MAT-340").Select(a => a.Id).ToList();
        Assert.DoesNotContain("ALU-002", ids); // Luis la está cursando
        Assert.Contains("ALU-001", ids);
    }

    [Fact]
    public void Los_docentes_se_cargan_y_cada_materia_tiene_un_docente()
    {
        var docentes = new RepositorioJson(Path.Combine(AppContext.BaseDirectory, "Data")).CargarDocentes();
        Assert.Equal(3, docentes.Count);
        var materias = Datos().Materias;
        foreach (var m in materias)
            Assert.Single(docentes.Where(x => x.MateriasIds.Contains(m.Id)));
    }

    [Fact]
    public void Al_calificar_el_analisis_del_alumno_se_recalcula()
    {
        var d = Datos();
        var luis = d.Alumnos.Single(a => a.Id == "ALU-002");
        var antes = StudentAnalyzer.AnalizarAlumno(luis, d);

        var en = d.Historial.Single(h => h.AlumnoId == "ALU-002" && h.MateriaId == "MAT-340");
        var nuevoHistorial = Gestion.Reemplazar(d.Historial, h => h.Id == en.Id, Gestion.Calificar(en, 88m));
        var despues = StudentAnalyzer.AnalizarAlumno(luis, d with { Historial = nuevoHistorial });

        Assert.Equal(2, antes.MateriasAprobadas);
        Assert.Equal(3, despues.MateriasAprobadas);
        Assert.Equal(0, despues.MateriasCursando);
        Assert.True(despues.CreditosAprobados > antes.CreditosAprobados);
    }
}
