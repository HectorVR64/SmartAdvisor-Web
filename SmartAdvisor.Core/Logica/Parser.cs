using System.Globalization;
using System.Text.RegularExpressions;

namespace SmartAdvisor.Core.Logica;

/// <summary>
/// Convierte el texto de los JSON en términos y cláusulas.
///   hecho:    promedio(ana, 95)
///   regla:    recomienda(A, ia) :- promedio(A, P), P >= 85, interes(A, ia)
///   consulta: recomienda(ana, E)
/// Convención (igual que Prolog): Mayúscula inicial = variable; minúscula = átomo/functor.
/// Operadores admitidos en el cuerpo: >=  <=  >  <  =  !=   y   not(Meta).
/// </summary>
public static class Parser
{
    private static readonly Regex Fichas = new(
        @"\s*(?:(?<op>:-|>=|<=|!=|>|<|=)|(?<num>\d+(?:\.\d+)?)|(?<id>[A-Za-z_][A-Za-z0-9_]*)|(?<sim>[(),.]))",
        RegexOptions.Compiled);

    private sealed class Flujo
    {
        private readonly List<string> _f = new();
        private int _i;

        public Flujo(string texto)
        {
            var pos = 0;
            texto = texto.Trim();
            while (pos < texto.Length)
            {
                var m = Fichas.Match(texto, pos);
                if (!m.Success || m.Index != pos)
                    throw new FormatException($"Carácter no válido en «{texto}», posición {pos}.");
                _f.Add(m.Value.Trim());
                pos = m.Index + m.Length;
            }
        }

        public string? Actual => _i < _f.Count ? _f[_i] : null;
        public string Siguiente() => _i < _f.Count ? _f[_i++] : throw new FormatException("El texto termina antes de tiempo.");
        public bool Fin => _i >= _f.Count;

        public void Esperar(string esperado)
        {
            var f = Siguiente();
            if (f != esperado) throw new FormatException($"Se esperaba «{esperado}» y se encontró «{f}».");
        }
    }

    public static Termino Termino(string texto)
    {
        var f = new Flujo(texto);
        var t = LeerMeta(f);
        if (f.Actual == ".") f.Siguiente();
        if (!f.Fin) throw new FormatException($"Sobra texto después de «{t}».");
        return t;
    }

    public static Compuesto Meta(string texto) =>
        Termino(texto) as Compuesto ?? throw new FormatException($"«{texto}» no es una consulta con forma functor(args).");

    public static Clausula Clausula(string texto, string id, string descripcion = "")
    {
        var f = new Flujo(texto);
        var cabeza = LeerTermino(f) as Compuesto ?? throw new FormatException($"La cabeza de «{texto}» debe ser functor(args).");
        var cuerpo = new List<Termino>();
        if (f.Actual == ":-")
        {
            f.Siguiente();
            cuerpo.Add(LeerMeta(f));
            while (f.Actual == ",") { f.Siguiente(); cuerpo.Add(LeerMeta(f)); }
        }
        if (f.Actual == ".") f.Siguiente();
        if (!f.Fin) throw new FormatException($"Sobra texto en «{texto}».");
        return new Clausula(cabeza, cuerpo, id, descripcion);
    }

    // meta := término [operador término]
    private static Termino LeerMeta(Flujo f)
    {
        var izq = LeerTermino(f);
        if (f.Actual is ">=" or "<=" or ">" or "<" or "=" or "!=")
        {
            var op = f.Siguiente();
            var der = LeerTermino(f);
            return new Compuesto(op, new[] { izq, der });
        }
        return izq;
    }

    // término := número | Variable | átomo | functor ( meta , ... )
    private static Termino LeerTermino(Flujo f)
    {
        var f0 = f.Siguiente();
        if (char.IsDigit(f0[0])) return new Numero(decimal.Parse(f0, CultureInfo.InvariantCulture));
        if (!(char.IsLetter(f0[0]) || f0[0] == '_')) throw new FormatException($"Término inesperado «{f0}».");
        if (f.Actual != "(")
            return char.IsUpper(f0[0]) || f0[0] == '_' ? new Variable(f0) : new Atomo(f0);

        f.Siguiente(); // (
        var args = new List<Termino> { LeerMeta(f) };
        while (f.Actual == ",") { f.Siguiente(); args.Add(LeerMeta(f)); }
        f.Esperar(")");
        return new Compuesto(f0, args);
    }
}
