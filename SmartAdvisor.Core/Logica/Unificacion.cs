namespace SmartAdvisor.Core.Logica;

/// <summary>
/// Unificación: busca una sustitución que haga IGUALES dos términos.
///   promedio(ana, P)  con  promedio(ana, 95)   →  { P = 95 }
///   promedio(ana, P)  con  promedio(luis, 82)  →  falla (ana ≠ luis)
/// Es una función pura: devuelve una sustitución nueva o null si no unifican.
/// </summary>
public static class Unificacion
{
    public static Sustitucion? Unificar(Termino a, Termino b, Sustitucion s)
    {
        a = s.Caminar(a);
        b = s.Caminar(b);

        if (a is Variable va && b is Variable vb && va == vb) return s;
        if (a is Variable v1) return Ocurre(v1, b, s) ? null : s.Con(v1, b);
        if (b is Variable v2) return Ocurre(v2, a, s) ? null : s.Con(v2, a);

        if (a is Atomo x && b is Atomo y) return x.Nombre == y.Nombre ? s : null;
        if (a is Numero n && b is Numero m) return n.Valor == m.Valor ? s : null;

        if (a is Compuesto c && b is Compuesto d && c.Functor == d.Functor && c.Aridad == d.Aridad)
        {
            Sustitucion? actual = s;
            for (var i = 0; i < c.Aridad && actual is not null; i++)
                actual = Unificar(c.Args[i], d.Args[i], actual);
            return actual;
        }

        return null;
    }

    /// <summary>Comprobación de ocurrencia: una variable no puede unificarse con un término que la contiene.</summary>
    private static bool Ocurre(Variable v, Termino t, Sustitucion s) => s.Caminar(t) switch
    {
        Variable w => w == v,
        Compuesto c => c.Args.Any(arg => Ocurre(v, arg, s)),
        _ => false
    };
}
