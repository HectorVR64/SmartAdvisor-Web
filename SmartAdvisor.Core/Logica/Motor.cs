using System.Collections.Immutable;
using System.Text;

namespace SmartAdvisor.Core.Logica;

/// <summary>
/// Árbol de prueba: explica CÓMO se demostró una meta (qué hecho o regla se usó y qué submetas
/// hubo que demostrar). Es lo que se muestra como "caso de inferencia documentado".
/// </summary>
public sealed record Prueba(Termino Meta, string Origen, IReadOnlyList<Prueba> Hijos)
{
    public string Texto(Sustitucion s, int nivel = 0)
    {
        var sb = new StringBuilder();
        Escribir(sb, s, nivel);
        return sb.ToString().TrimEnd();
    }

    private void Escribir(StringBuilder sb, Sustitucion s, int nivel)
    {
        sb.Append(' ', nivel * 3).Append(s.Aplicar(Meta)).Append("   ← ").AppendLine(Origen);
        foreach (var h in Hijos) h.Escribir(sb, s, nivel + 1);
    }
}

/// <summary>Una solución de una consulta: la sustitución final y la prueba de cada meta.</summary>
public sealed record Solucion(Sustitucion Sustitucion, IReadOnlyList<Prueba> Pruebas)
{
    public Termino Valor(Variable v) => Sustitucion.Aplicar(v);
    public string TextoPrueba() => string.Join(Environment.NewLine, Pruebas.Select(p => p.Texto(Sustitucion)));
}

/// <summary>
/// Motor de inferencia: resolución SLD con búsqueda en profundidad y retroceso (backtracking),
/// igual que Prolog. Para cada meta busca cláusulas cuya cabeza UNIFIQUE con ella; si es una regla,
/// las metas de su cuerpo pasan a ser nuevas submetas. Incluye comparaciones numéricas y
/// negación por fallo (not): not(M) se cumple si M NO se puede demostrar.
///
/// El resultado es perezoso (IEnumerable): las soluciones se generan una a una, solo cuando se piden.
/// </summary>
public sealed class Motor
{
    private const int ProfundidadMaxima = 200;
    private readonly ILookup<(string, int), Clausula> _indice;
    private int _contador; // para crear variables frescas al renombrar una regla

    public Motor(IEnumerable<Clausula> clausulas) =>
        _indice = clausulas.ToLookup(c => (c.Cabeza.Functor, c.Cabeza.Aridad));

    /// <summary>Todas las soluciones de una consulta (lista de metas separadas por coma).</summary>
    public IEnumerable<Solucion> Resolver(IReadOnlyList<Termino> metas)
    {
        foreach (var (s, pruebas) in Resolver(metas, Sustitucion.Vacia, 0))
            yield return new Solucion(s, pruebas);
    }

    public IEnumerable<Solucion> Resolver(string consulta) => Resolver(new[] { (Termino)Parser.Meta(consulta) });

    private IEnumerable<(Sustitucion, ImmutableList<Prueba>)> Resolver(
        IReadOnlyList<Termino> metas, Sustitucion s, int profundidad)
    {
        if (metas.Count == 0)
        {
            yield return (s, ImmutableList<Prueba>.Empty);
            yield break;
        }

        var resto = metas.Skip(1).ToList();
        foreach (var (s1, prueba) in ResolverUna(metas[0], s, profundidad))
            foreach (var (s2, pruebas) in Resolver(resto, s1, profundidad))
                yield return (s2, pruebas.Insert(0, prueba));
    }

    private IEnumerable<(Sustitucion, Prueba)> ResolverUna(Termino meta, Sustitucion s, int profundidad)
    {
        if (profundidad > ProfundidadMaxima)
            throw new InvalidOperationException("Se superó la profundidad máxima de resolución (posible recursión infinita).");

        var objetivo = s.Caminar(meta);
        if (objetivo is not Compuesto g)
            throw new InvalidOperationException($"La meta «{s.Aplicar(meta)}» no es válida: debe ser functor(args).");

        // Negación por fallo: not(M) se cumple si M no tiene ninguna solución.
        if (g.Functor == "not" && g.Aridad == 1)
        {
            var interna = g.Args[0];
            if (!Resolver(new[] { interna }, s, profundidad + 1).Any())
                yield return (s, new Prueba(s.Aplicar(g), "negación por fallo (no se puede demostrar)", Array.Empty<Prueba>()));
            yield break;
        }

        // Comparaciones numéricas y unificación explícita.
        if (g.Aridad == 2 && g.Functor is ">=" or "<=" or ">" or "<" or "=" or "!=")
        {
            foreach (var r in ResolverOperador(g, s)) yield return r;
            yield break;
        }

        // Cláusulas cuya cabeza puede unificar con la meta.
        foreach (var original in _indice[(g.Functor, g.Aridad)])
        {
            var c = Renombrar(original);
            var s1 = Unificacion.Unificar(g, c.Cabeza, s);
            if (s1 is null) continue;

            if (c.EsHecho)
            {
                yield return (s1, new Prueba(g, $"hecho {original.Id}", Array.Empty<Prueba>()));
                continue;
            }

            foreach (var (s2, hijos) in Resolver(c.Cuerpo, s1, profundidad + 1))
                yield return (s2, new Prueba(g, $"regla {original.Id}", hijos));
        }
    }

    private static IEnumerable<(Sustitucion, Prueba)> ResolverOperador(Compuesto g, Sustitucion s)
    {
        if (g.Functor == "=")
        {
            var u = Unificacion.Unificar(g.Args[0], g.Args[1], s);
            if (u is not null) yield return (u, new Prueba(g, "unificación", Array.Empty<Prueba>()));
            yield break;
        }

        var a = s.Aplicar(g.Args[0]);
        var b = s.Aplicar(g.Args[1]);

        if (g.Functor == "!=")
        {
            if (!EsIgual(a, b)) yield return (s, new Prueba(g, "comparación", Array.Empty<Prueba>()));
            yield break;
        }

        if (a is not Numero x || b is not Numero y)
            throw new InvalidOperationException($"No se puede comparar «{a} {g.Functor} {b}»: ambos lados deben ser números ya conocidos.");

        var cumple = g.Functor switch
        {
            ">=" => x.Valor >= y.Valor,
            "<=" => x.Valor <= y.Valor,
            ">" => x.Valor > y.Valor,
            _ => x.Valor < y.Valor
        };
        if (cumple) yield return (s, new Prueba(g, "comparación numérica", Array.Empty<Prueba>()));
    }

    private static bool EsIgual(Termino a, Termino b) => (a, b) switch
    {
        (Atomo x, Atomo y) => x.Nombre == y.Nombre,
        (Numero x, Numero y) => x.Valor == y.Valor,
        _ => false
    };

    /// <summary>Cada vez que se usa una regla se le dan variables nuevas para que no choquen entre usos.</summary>
    private Clausula Renombrar(Clausula c)
    {
        if (c.EsHecho && !TieneVariables(c.Cabeza)) return c;
        var id = ++_contador;
        Termino R(Termino t) => t switch
        {
            Variable v => new Variable(v.Nombre, id),
            Compuesto k => new Compuesto(k.Functor, k.Args.Select(R).ToList()),
            _ => t
        };
        return c with { Cabeza = (Compuesto)R(c.Cabeza), Cuerpo = c.Cuerpo.Select(R).ToList() };
    }

    private static bool TieneVariables(Termino t) => t switch
    {
        Variable => true,
        Compuesto c => c.Args.Any(TieneVariables),
        _ => false
    };
}
