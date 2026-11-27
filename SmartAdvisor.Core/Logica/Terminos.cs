using System.Collections.Immutable;
using System.Globalization;

namespace SmartAdvisor.Core.Logica;

// ───────────────────────── Paradigma LÓGICO ─────────────────────────
// Un programa lógico es una base de HECHOS y REGLAS (cláusulas de Horn) más una CONSULTA.
// Aquí se representan como datos inmutables; el motor (Motor.cs) los resuelve por unificación
// y resolución SLD (el mismo mecanismo de Prolog).

/// <summary>Término lógico: átomo, número, variable o término compuesto.</summary>
public abstract record Termino;

/// <summary>Constante simbólica: ana, ia, mat_301.</summary>
public sealed record Atomo(string Nombre) : Termino
{
    public override string ToString() => Nombre;
}

/// <summary>Constante numérica: 95, 82.5.</summary>
public sealed record Numero(decimal Valor) : Termino
{
    public override string ToString() => Valor.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Variable lógica (en el texto empieza con mayúscula: A, E, Promedio). Id distingue las copias
/// que se crean al "renombrar" una regla cada vez que se usa.
/// </summary>
public sealed record Variable(string Nombre, int Id = 0) : Termino
{
    public override string ToString() => Nombre;
}

/// <summary>Término compuesto: functor(arg1, arg2, ...). Un hecho como promedio(ana, 95) es uno.</summary>
public sealed record Compuesto(string Functor, IReadOnlyList<Termino> Args) : Termino
{
    public int Aridad => Args.Count;

    public override string ToString() => Functor switch
    {
        ">=" or "<=" or ">" or "<" or "=" or "!=" when Args.Count == 2 => $"{Args[0]} {Functor} {Args[1]}",
        _ => $"{Functor}({string.Join(", ", Args)})"
    };
}

/// <summary>
/// Cláusula de Horn: Cabeza :- Cuerpo1, Cuerpo2, ...  Si el cuerpo está vacío es un HECHO.
/// </summary>
public sealed record Clausula(Compuesto Cabeza, IReadOnlyList<Termino> Cuerpo, string Id, string Descripcion = "")
{
    public bool EsHecho => Cuerpo.Count == 0;

    public override string ToString() =>
        EsHecho ? $"{Cabeza}." : $"{Cabeza} :- {string.Join(", ", Cuerpo)}.";
}

/// <summary>Sustitución: asigna términos a variables. Es inmutable: unificar devuelve una nueva.</summary>
public sealed class Sustitucion
{
    public static readonly Sustitucion Vacia = new(ImmutableDictionary<Variable, Termino>.Empty);

    private readonly ImmutableDictionary<Variable, Termino> _mapa;
    private Sustitucion(ImmutableDictionary<Variable, Termino> mapa) => _mapa = mapa;

    public Sustitucion Con(Variable v, Termino t) => new(_mapa.SetItem(v, t));

    /// <summary>Sigue la cadena de variables hasta un término que no sea una variable ligada.</summary>
    public Termino Caminar(Termino t)
    {
        while (t is Variable v && _mapa.TryGetValue(v, out var siguiente)) t = siguiente;
        return t;
    }

    /// <summary>Reemplaza todas las variables ligadas, también dentro de términos compuestos.</summary>
    public Termino Aplicar(Termino t) => Caminar(t) switch
    {
        Compuesto c => new Compuesto(c.Functor, c.Args.Select(Aplicar).ToList()),
        var otro => otro
    };

    public IEnumerable<KeyValuePair<Variable, Termino>> Ligaduras => _mapa;
}
