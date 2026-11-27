using System.Text.Json;
using SmartAdvisor.Core.Models;
using SmartAdvisor.Core.Services;
using Xunit;

namespace SmartAdvisor.Tests;

public class StudentAnalyzerTests
{
    private static DatosAcademicos Datos() =>
        new RepositorioJson(Path.Combine(AppContext.BaseDirectory, "Data")).Cargar();

    private static AnalisisAlumno De(AnalisisAcademico a, string id) => a.Alumnos.Single(x => x.AlumnoId == id);

    // ---------------------------------------------------------------- carga de datos

    [Fact]
    public void Carga_las_siete_colecciones()
    {
        var d = Datos();
        Assert.Equal(4, d.Alumnos.Count);
        Assert.Equal(10, d.Materias.Count);
        Assert.Equal(3, d.Especialidades.Count);
        Assert.Equal(3, d.Cursos.Count);
        Assert.Equal(3, d.Certificaciones.Count);
        Assert.Equal(10, d.Historial.Count);
        Assert.Equal(4, d.Reglas.Count);
    }

    // ---------------------------------------------------------------- resumen e indicadores

    [Fact]
    public void Resumen_general_cuenta_y_promedia()
    {
        var r = StudentAnalyzer.Analizar(Datos()).Resumen;
        Assert.Equal(4, r.TotalAlumnos);
        Assert.Equal(10, r.TotalMaterias);
        Assert.Equal(3, r.TotalEspecialidades);
        Assert.Equal(50, r.CreditosPlan);
        Assert.Equal(81.5m, r.PromedioGeneral);
        Assert.Equal(1, r.SinRecomendacion);
    }

    [Fact]
    public void Aggregate_acumula_estadistica_en_una_pasada()
    {
        var e = StudentAnalyzer.EstadisticaDe(new[] { 95m, 82.5m, 78m, 70.5m });
        Assert.Equal(4, e.N);
        Assert.Equal(70.5m, e.Min);
        Assert.Equal(95m, e.Max);
        Assert.Equal(326m, e.Suma);
        Assert.Equal(81.5m, e.Promedio);
        Assert.Equal(0, StudentAnalyzer.EstadisticaDe(Array.Empty<decimal>()).N);
    }

    [Fact]
    public void Intereses_frecuentes_agrupa_y_ordena()
    {
        var i = StudentAnalyzer.InteresesFrecuentes(Datos().Alumnos);
        Assert.Equal("Datos", i[0].Interes);
        Assert.Equal(2, i[0].Alumnos);
        Assert.Equal(5, i.Count);
    }

    [Fact]
    public void Mejores_promedios_ordena_y_toma_tres()
    {
        var m = StudentAnalyzer.MejoresPromedios(Datos().Alumnos);
        Assert.Equal(3, m.Count);
        Assert.Equal("Ana Torres Ramírez", m[0].Nombre);
        Assert.Equal("Sofía Ramírez León", m[2].Nombre);
    }

    [Fact]
    public void Promedio_y_avance_por_area_global()
    {
        var d = Datos();
        var ia = StudentAnalyzer.IndicadoresPorArea(d.Materias, StudentAnalyzer.Aprobadas(d.Historial), d.Alumnos.Count)
            .Single(x => x.Area == "IA");
        Assert.Equal(93.0m, ia.Promedio);      // (92 + 94) / 2
        Assert.Equal(2, ia.Aprobadas);
        Assert.Equal(4, ia.MateriasPlan);
        Assert.Equal(12.5m, ia.AvancePct);     // 2 de 16 posibles (4 materias x 4 alumnos)
    }

    [Fact]
    public void Avance_de_Ana_son_25_de_50_creditos()
    {
        var ana = De(StudentAnalyzer.Analizar(Datos()), "ALU-001");
        Assert.Equal(25, ana.CreditosAprobados);
        Assert.Equal(50, ana.CreditosPlan);
        Assert.Equal(50.0m, ana.AvancePct);
        Assert.Equal(5, ana.MateriasAprobadas);
    }

    [Fact]
    public void Luis_cuenta_materia_en_curso_sin_calificacion()
    {
        var luis = De(StudentAnalyzer.Analizar(Datos()), "ALU-002");
        Assert.Equal(2, luis.MateriasAprobadas);
        Assert.Equal(1, luis.MateriasCursando);
        Assert.Equal(82.5m, luis.PorArea.Single(x => x.Area == "Desarrollo").Promedio);
    }

    // ---------------------------------------------------------------- recomendaciones (resultados del Parcial 1)

    [Fact]
    public void Ana_IA_recomendada_con_afinidad_96_3()
    {
        var r = De(StudentAnalyzer.Analizar(Datos()), "ALU-001").Recomendacion;
        Assert.Equal("recomendada", r.Estado);
        Assert.Equal("ESP-IA", r.EspecialidadId);
        Assert.Equal(96.3m, r.Afinidad);
        Assert.Equal("EspecialidadIA", r.ReglaAplicada);
    }

    [Fact]
    public void Luis_cumple_dos_reglas_y_gana_Desarrollo()
    {
        var r = De(StudentAnalyzer.Analizar(Datos()), "ALU-002").Recomendacion;
        Assert.Equal("ESP-DES", r.EspecialidadId);
        Assert.Equal(69.5m, r.Afinidad);
        Assert.Equal(40.6m, r.Ranking.Single(x => x.EspecialidadId == "ESP-DAT").Afinidad);
        Assert.True(r.Ranking.Single(x => x.EspecialidadId == "ESP-DAT").CumpleRegla);
    }

    [Fact]
    public void Sofia_queda_condicionada_por_dos_puntos()
    {
        var r = De(StudentAnalyzer.Analizar(Datos()), "ALU-003").Recomendacion;
        Assert.Equal("condicionada", r.Estado);
        Assert.Equal("ESP-DAT", r.EspecialidadId);
        Assert.Equal(86.8m, r.Afinidad);
        Assert.Contains("2.0 puntos", r.Motivo);
    }

    [Fact]
    public void Diego_no_tiene_recomendacion()
    {
        var r = De(StudentAnalyzer.Analizar(Datos()), "ALU-004").Recomendacion;
        Assert.Equal("sin-recomendacion", r.Estado);
        Assert.Null(r.EspecialidadId);
        Assert.Equal(17.6m, r.Afinidad);
    }

    // ---------------------------------------------------------------- materias sugeridas

    [Fact]
    public void Ana_recibe_las_dos_materias_de_IA_por_prerrequisito()
    {
        var s = De(StudentAnalyzer.Analizar(Datos()), "ALU-001").MateriasSugeridas;
        Assert.Equal(new[] { "MAT-401", "MAT-402" }, s.Select(x => x.MateriaId).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Luis_no_recibe_la_materia_que_ya_esta_cursando()
    {
        Assert.Empty(De(StudentAnalyzer.Analizar(Datos()), "ALU-002").MateriasSugeridas);
    }

    [Fact]
    public void Sofia_recibe_Mineria_de_Datos()
    {
        var s = De(StudentAnalyzer.Analizar(Datos()), "ALU-003").MateriasSugeridas;
        Assert.Equal("MAT-330", Assert.Single(s).MateriaId);
    }

    // ---------------------------------------------------------------- evaluación de expresiones

    [Fact]
    public void Evaluador_convierte_las_condiciones_en_funciones()
    {
        var regla = Datos().Reglas.Single(r => r.Id == "REG-001");
        Assert.True(EvaluadorReglas.Cumple(regla, new Hechos(95m, new[] { "IA" })));
        Assert.False(EvaluadorReglas.Cumple(regla, new Hechos(84.9m, new[] { "IA" })));
        Assert.False(EvaluadorReglas.Cumple(regla, new Hechos(95m, new[] { "Datos" })));
    }

    [Fact]
    public void Comparador_es_una_funcion_de_orden_superior()
    {
        Assert.True(EvaluadorReglas.Comparador(">=")(85m, 85m));
        Assert.False(EvaluadorReglas.Comparador(">")(85m, 85m));
        Assert.True(EvaluadorReglas.Comparador("!=")(1m, 2m));
        Assert.False(EvaluadorReglas.Comparador("??")(1m, 1m));
    }

    [Fact]
    public void Hecho_desconocido_hace_falsa_la_regla()
    {
        var c = new Condicion("Clima", "=", JsonDocument.Parse("\"soleado\"").RootElement);
        Assert.False(EvaluadorReglas.Compilar(c)(new Hechos(100m, new[] { "IA" })));
    }

    // ---------------------------------------------------------------- pureza y gestión

    [Fact]
    public void Analizar_es_puro_no_modifica_los_datos_y_es_repetible()
    {
        var d = Datos();
        var antes = JsonConfig.Serializar(d);
        var a1 = JsonConfig.Serializar(StudentAnalyzer.Analizar(d));
        var a2 = JsonConfig.Serializar(StudentAnalyzer.Analizar(d));
        Assert.Equal(antes, JsonConfig.Serializar(d));
        Assert.Equal(a1, a2);
    }

    [Fact]
    public void Agregar_y_quitar_devuelven_listas_nuevas()
    {
        var d = Datos();
        var nuevo = d.Alumnos[0] with { Id = "ALU-005", Matricula = "202610005" };
        var mas = Gestion.Agregar(d.Alumnos, nuevo);
        Assert.Equal(4, d.Alumnos.Count);
        Assert.Equal(5, mas.Count);
        Assert.Equal(4, Gestion.Quitar(mas, a => a.Id == "ALU-005").Count);
    }

    [Fact]
    public void Siguiente_id_continua_la_numeracion()
    {
        Assert.Equal("ALU-005", Gestion.SiguienteId("ALU", Datos().Alumnos.Select(a => a.Id)));
        Assert.Equal("MAT-001", Gestion.SiguienteId("MAT", Array.Empty<string>()));
    }

    [Fact]
    public void Validaciones_detectan_datos_incorrectos()
    {
        var d = Datos();
        var malo = d.Alumnos[0] with { Id = "ALU-009", Promedio = 120m, Semestre = 12 };
        var errores = Gestion.ValidarAlumno(malo, d.Alumnos);
        Assert.Contains("El promedio debe estar entre 0 y 100.", errores);
        Assert.Contains("El semestre debe estar entre 1 y 9.", errores);
        Assert.Contains("Ya existe un alumno con esa matrícula.", errores);

        var h = new Historial("HIS-011", "ALU-004", "MAT-101", "2026-1", 60m, "aprobada");
        Assert.Contains("Una materia aprobada requiere calificación de 70 o más.", Gestion.ValidarHistorial(h, d));
    }

    [Fact]
    public void No_se_elimina_una_materia_con_dependencias()
    {
        var d = Datos();
        Assert.NotNull(Gestion.MotivoParaNoEliminarMateria(d, "MAT-101"));
        Assert.Null(Gestion.MotivoParaNoEliminarEspecialidad(d with { Reglas = Array.Empty<Regla>() }, "ESP-IA"));
    }

    [Fact]
    public void Filtros_de_alumnos_combinan_texto_interes_y_estatus()
    {
        var d = Datos();
        Assert.Equal(2, Consultas.FiltrarAlumnos(d.Alumnos, null, "Datos", null).Count);
        Assert.Equal("Ana Torres Ramírez", Assert.Single(Consultas.FiltrarAlumnos(d.Alumnos, "ana", null, "activo")).Nombre);
        Assert.Empty(Consultas.FiltrarAlumnos(d.Alumnos, null, null, "baja"));
    }
}
