using SmartAdvisor.Core.Logica;
using SmartAdvisor.Core.Models;
using SmartAdvisor.Core.Services;
using Xunit;

namespace SmartAdvisor.Tests;

/// <summary>Pruebas del paradigma lógico: unificación, resolución, base de conocimiento e inferencia.</summary>
public class LogicaTests
{
    private static string Carpeta => Path.Combine(AppContext.BaseDirectory, "Data");
    private static BaseConocimiento Kb() => BaseConocimiento.DesdeCarpeta(Carpeta);
    private static DatosAcademicos Datos() => new RepositorioJson(Carpeta).Cargar();

    // ---------------------------------------------------------------- unificación

    [Fact]
    public void Unifica_variable_con_constante()
    {
        var s = Unificacion.Unificar(Parser.Termino("promedio(ana, P)"), Parser.Termino("promedio(ana, 95)"), Sustitucion.Vacia);
        Assert.NotNull(s);
        Assert.Equal("95", s!.Aplicar(new Variable("P")).ToString());
    }

    [Fact]
    public void No_unifica_constantes_distintas()
    {
        Assert.Null(Unificacion.Unificar(Parser.Termino("promedio(ana, P)"), Parser.Termino("promedio(luis, 82)"), Sustitucion.Vacia));
    }

    [Fact]
    public void No_unifica_con_distinta_aridad_ni_functor()
    {
        Assert.Null(Unificacion.Unificar(Parser.Termino("a(x)"), Parser.Termino("a(x, y)"), Sustitucion.Vacia));
        Assert.Null(Unificacion.Unificar(Parser.Termino("a(x)"), Parser.Termino("b(x)"), Sustitucion.Vacia));
    }

    [Fact]
    public void Comprobacion_de_ocurrencia_evita_terminos_infinitos()
    {
        Assert.Null(Unificacion.Unificar(new Variable("X"), Parser.Termino("f(X)"), Sustitucion.Vacia));
    }

    // ---------------------------------------------------------------- parser y base de conocimiento

    [Fact]
    public void Parser_lee_hechos_y_reglas()
    {
        var hecho = Parser.Clausula("promedio(ana, 95)", "H1");
        Assert.True(hecho.EsHecho);

        var regla = Parser.Clausula("recomienda(A, ia) :- promedio(A, P), P >= 85, interes(A, ia)", "R1");
        Assert.Equal(3, regla.Cuerpo.Count);
        Assert.Equal("recomienda(A, ia) :- promedio(A, P), P >= 85, interes(A, ia).", regla.ToString());
    }

    [Fact]
    public void Parser_rechaza_texto_invalido()
    {
        Assert.Throws<FormatException>(() => Parser.Clausula("promedio(ana, 95", "H1"));
        Assert.Throws<FormatException>(() => Parser.Meta("ana"));
    }

    [Fact]
    public void Carga_hechos_y_reglas_de_los_json()
    {
        var kb = Kb();
        Assert.Equal(55, kb.Hechos.Count);
        Assert.Equal(8, kb.Reglas.Count);
        Assert.Contains(kb.Reglas, r => r.Id == "EspecialidadIA");
        Assert.Contains(kb.Reglas, r => r.Id == "EspecialidadDesarrollo");
        Assert.Contains(kb.Reglas, r => r.Id == "EspecialidadDatos");
    }

    // ---------------------------------------------------------------- resolución

    [Fact]
    public void Consulta_de_un_hecho_se_demuestra()
    {
        var r = Inferencia.Consultar(Kb(), "promedio(ana, P)");
        Assert.True(r.Demostrada);
        Assert.Equal("95", r.Soluciones[0].Valores["P"]);
    }

    [Fact]
    public void Consulta_falsa_no_tiene_soluciones()
    {
        Assert.False(Inferencia.Consultar(Kb(), "recomienda(diego, E)").Demostrada);
        Assert.False(Inferencia.Consultar(Kb(), "interes(ana, redes)").Demostrada);
    }

    [Fact]
    public void Retroceso_encuentra_todas_las_soluciones()
    {
        var r = Inferencia.Consultar(Kb(), "interes(luis, I)");
        Assert.Equal(new[] { "desarrollo", "datos" }, r.Soluciones.Select(s => s.Valores["I"]));
    }

    [Fact]
    public void La_prueba_nombra_la_regla_y_los_hechos_usados()
    {
        var prueba = Inferencia.Consultar(Kb(), "recomienda(ana, E)").Soluciones[0].Prueba;
        Assert.Contains("regla EspecialidadIA", prueba);
        Assert.Contains("promedio(ana, 95)", prueba);
        Assert.Contains("95 >= 85", prueba);
    }

    [Fact]
    public void Negacion_por_fallo_descarta_materias_aprobadas_y_sin_prerrequisitos()
    {
        var sugeridas = Inferencia.Consultar(Kb(), "sugerida(ana, M)").Soluciones.Select(s => s.Valores["M"]).ToList();
        Assert.Equal(new[] { "mat_401", "mat_402" }, sugeridas.Distinct().OrderBy(x => x));
        Assert.DoesNotContain("mat_301", sugeridas); // ya aprobada
    }

    // ---------------------------------------------------------------- inferencia completa

    [Fact]
    public void Ana_se_recomienda_IA_con_dos_materias()
    {
        var r = Inferencia.Inferir(Kb(), "ana");
        Assert.Equal("esp_ia", r.Recomendada!.Id);
        Assert.Equal("EspecialidadIA", r.Recomendada.Regla);
        Assert.Equal(new[] { "mat_401", "mat_402" }, r.MateriasSugeridas.Select(m => m.Id));
    }

    [Fact]
    public void Luis_cumple_dos_reglas_y_la_afinidad_elige_Desarrollo()
    {
        var datos = Datos();
        var luis = datos.Alumnos.Single(a => a.Id == "ALU-002");
        var r = Inferencia.Inferir(Kb(), "luis", Inferencia.AfinidadDesde(luis, datos));
        Assert.Equal(2, r.Recomendables.Count);
        Assert.Equal("esp_des", r.Recomendada!.Id);
        Assert.Equal(69.5m, r.Recomendada.Afinidad);
        Assert.Empty(r.MateriasSugeridas); // 101 y 110 aprobadas, 340 en curso
    }

    [Fact]
    public void Sofia_queda_condicionada_en_Datos()
    {
        var r = Inferencia.Inferir(Kb(), "sofia");
        Assert.Null(r.Recomendada);
        Assert.Equal("esp_dat", r.Condicionadas.Single().Id);
        Assert.Equal(new[] { "mat_330" }, r.MateriasSugeridas.Select(m => m.Id));
    }

    [Fact]
    public void Diego_no_tiene_ninguna_inferencia()
    {
        var r = Inferencia.Inferir(Kb(), "diego");
        Assert.Null(r.Recomendada);
        Assert.Empty(r.Condicionadas);
        Assert.Empty(r.MateriasSugeridas);
    }

    // ---------------------------------------------------------------- coherencia con el análisis funcional

    [Fact]
    public void La_inferencia_logica_coincide_con_el_analisis_funcional()
    {
        var kb = Kb();
        var datos = Datos();
        foreach (var alumno in datos.Alumnos)
        {
            var funcional = StudentAnalyzer.AnalizarAlumno(alumno, datos);
            var logico = Inferencia.Inferir(kb, kb.IdsAlumno[alumno.Id], Inferencia.AfinidadDesde(alumno, datos));

            Assert.Equal(funcional.Recomendacion.Estado == "recomendada", logico.Recomendada is not null);
            Assert.Equal(funcional.Recomendacion.Estado == "condicionada", logico.Recomendada is null && logico.Condicionadas.Count > 0);
            if (logico.Recomendada is not null)
                Assert.Equal("esp_" + funcional.Recomendacion.EspecialidadId![4..].ToLowerInvariant(), logico.Recomendada.Id);
            Assert.Equal(
                funcional.MateriasSugeridas.Select(m => "mat_" + m.MateriaId[4..]).OrderBy(x => x),
                logico.MateriasSugeridas.Select(m => m.Id).OrderBy(x => x));
        }
    }
}
