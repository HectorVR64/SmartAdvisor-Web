using SmartAdvisor.Core.Models;
using SmartAdvisor.Core.Services;

namespace SmartAdvisor.Core.Logica;

public sealed record EspecialidadInferida(string Id, string Nombre, string Regla, decimal? Afinidad, string Prueba);
public sealed record MateriaInferida(string Id, string Nombre, string Prueba);

public sealed record ResultadoInferencia(
    string AlumnoId,
    string Alumno,
    EspecialidadInferida? Recomendada,
    IReadOnlyList<EspecialidadInferida> Recomendables,
    IReadOnlyList<EspecialidadInferida> Condicionadas,
    IReadOnlyList<MateriaInferida> MateriasSugeridas,
    string Explicacion);

public sealed record RespuestaConsulta(
    string Consulta, bool Demostrada, IReadOnlyList<SolucionConsulta> Soluciones);

public sealed record SolucionConsulta(IReadOnlyDictionary<string, string> Valores, string Prueba);

/// <summary>
/// AcademicInferenceFunction (lógica): responde "¿qué especialidad se recomienda?" y
/// "¿qué materias debería cursar?" razonando sobre la base de hechos y reglas.
/// Todo son funciones puras sobre una BaseConocimiento.
/// </summary>
public static class Inferencia
{
    /// <summary>Consulta libre, p. ej. "recomienda(ana, E)". Devuelve las soluciones con su prueba.</summary>
    public static RespuestaConsulta Consultar(BaseConocimiento kb, string consulta, int maximo = 50)
    {
        var motor = new Motor(kb.Clausulas);
        var meta = Parser.Meta(consulta);
        var variables = Variables(meta).ToList();

        var soluciones = motor.Resolver(new[] { (Termino)meta })
            .Take(maximo)
            .Select(s => new SolucionConsulta(
                variables.ToDictionary(v => v.Nombre, v => s.Valor(v).ToString()),
                s.TextoPrueba()))
            .ToList();

        return new RespuestaConsulta(consulta, soluciones.Count > 0, soluciones);
    }

    /// <summary>
    /// Inferencia completa para un alumno. Las REGLAS deciden qué especialidades se pueden demostrar;
    /// si hay varias y se entrega afinidadDe (función del Core funcional), esa afinidad las ordena.
    /// </summary>
    public static ResultadoInferencia Inferir(BaseConocimiento kb, string alumno, Func<string, decimal?>? afinidadDe = null)
    {
        var motor = new Motor(kb.Clausulas);
        var nombreAlumno = kb.NombreDe(alumno);

        // ¿qué especialidad se recomienda?  recomienda(alumno, E)
        var recomendables = motor.Resolver($"recomienda({alumno}, E)")
            .Select(s => Especialidad(kb, s, afinidadDe))
            .OrderByDescending(e => e.Afinidad ?? 0m)
            .ToList();

        // especialidades que coinciden con sus intereses pero aún no cumplen  condicionada(alumno, E)
        var condicionadas = motor.Resolver($"condicionada({alumno}, E)")
            .Select(s => Especialidad(kb, s, afinidadDe))
            .OrderByDescending(e => e.Afinidad ?? 0m)
            .ToList();

        var recomendada = recomendables.FirstOrDefault();

        // ¿qué materias debería cursar?  sugerida(alumno, M), materia_de(<especialidad objetivo>, M)
        // La especialidad objetivo es la recomendada o, si no hay, la condicionada de mayor afinidad.
        var objetivo = recomendada ?? condicionadas.FirstOrDefault();
        var materias = new List<MateriaInferida>();
        if (objetivo is not null)
        {
            var esp = (Termino)new Atomo(objetivo.Id);
            var metas = new Termino[]
            {
                Parser.Meta($"sugerida({alumno}, M)"),
                new Compuesto("materia_de", new[] { esp, new Variable("M") })
            };
            var m = new Variable("M");
            materias = motor.Resolver(metas)
                .Select(s => new MateriaInferida(s.Valor(m).ToString(), kb.NombreDe(s.Valor(m).ToString()), s.TextoPrueba()))
                .DistinctBy(x => x.Id)
                .OrderBy(x => x.Id, StringComparer.Ordinal)
                .ToList();
        }

        return new ResultadoInferencia(alumno, nombreAlumno, recomendada, recomendables, condicionadas, materias,
            Explicar(nombreAlumno, recomendada, recomendables, condicionadas, materias));
    }

    private static EspecialidadInferida Especialidad(BaseConocimiento kb, Solucion s, Func<string, decimal?>? afinidadDe)
    {
        var id = s.Valor(new Variable("E")).ToString();
        // El nombre de la regla que concluyó (EspecialidadIA, ...) es la de la primera prueba.
        var regla = s.Pruebas[0].Origen.Replace("regla ", "");
        return new EspecialidadInferida(id, kb.NombreDe(id), regla, afinidadDe?.Invoke(id), s.TextoPrueba());
    }

    private static string Explicar(string alumno, EspecialidadInferida? rec,
        IReadOnlyList<EspecialidadInferida> recomendables, IReadOnlyList<EspecialidadInferida> cond,
        IReadOnlyList<MateriaInferida> materias)
    {
        if (rec is null && cond.Count == 0)
            return $"{alumno}: ninguna regla se puede demostrar con sus hechos (sus intereses no coinciden con ninguna especialidad).";

        if (rec is null)
            return $"{alumno}: ninguna especialidad se puede recomendar todavía; coincide con {string.Join(", ", cond.Select(c => c.Nombre))} pero no cumple la regla.";

        var otras = recomendables.Count > 1
            ? $" También cumple {string.Join(", ", recomendables.Skip(1).Select(r => r.Nombre))}."
            : "";
        var mats = materias.Count > 0
            ? $" Materias sugeridas: {string.Join(", ", materias.Select(m => m.Nombre))}."
            : " No tiene materias nuevas por sugerir ahora (las demás están aprobadas, en curso o le faltan prerrequisitos).";
        return $"{alumno}: se recomienda {rec.Nombre} (regla {rec.Regla}).{otras}{mats}";
    }

    private static IEnumerable<Variable> Variables(Termino t) => t switch
    {
        Variable v => new[] { v },
        Compuesto c => c.Args.SelectMany(Variables).DistinctBy(v => v.Nombre),
        _ => Array.Empty<Variable>()
    };

    /// <summary>Enlaza la inferencia con el análisis funcional: afinidad de un alumno con una especialidad.</summary>
    public static Func<string, decimal?> AfinidadDesde(Alumno alumno, DatosAcademicos datos)
    {
        var aprobadas = StudentAnalyzer.Aprobadas(StudentAnalyzer.DeAlumno(datos.Historial, alumno.Id));
        return espAtomo =>
        {
            var esp = datos.Especialidades.FirstOrDefault(e =>
                ("esp_" + e.Id.Split('-')[1]).Equals(espAtomo, StringComparison.OrdinalIgnoreCase));
            return esp is null ? null : StudentAnalyzer.Afinidad(alumno, esp, datos.Materias, aprobadas);
        };
    }
}
