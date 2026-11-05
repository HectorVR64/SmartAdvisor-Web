using System.Text.Json;
using SmartAdvisor.Core.Models;

namespace SmartAdvisor.Core.Services;

/// <summary>Hechos conocidos de un alumno: lo que las reglas pueden consultar.</summary>
public sealed record Hechos(decimal Promedio, IReadOnlyList<string> Intereses);

/// <summary>
/// Evaluación de expresiones del paradigma funcional: cada condición de reglas.json
/// (por ejemplo { "hecho": "Promedio", "operador": ">=", "valor": 85 }) se convierte en una
/// función Hechos -> bool, y las funciones se componen con Aggregate para formar la regla completa.
/// Todo es puro: la misma regla y los mismos hechos siempre dan el mismo resultado.
/// </summary>
public static class EvaluadorReglas
{
    private static readonly Func<Hechos, bool> Siempre = _ => true;
    private static readonly Func<Hechos, bool> Nunca = _ => false;

    /// <summary>Operador textual -> función de comparación (función de orden superior).</summary>
    public static Func<decimal, decimal, bool> Comparador(string operador) => operador switch
    {
        ">=" => (a, b) => a >= b,
        ">" => (a, b) => a > b,
        "<=" => (a, b) => a <= b,
        "<" => (a, b) => a < b,
        "=" or "==" => (a, b) => a == b,
        "!=" => (a, b) => a != b,
        _ => (_, _) => false
    };

    /// <summary>Convierte UNA condición en una función evaluable.</summary>
    public static Func<Hechos, bool> Compilar(Condicion c)
    {
        switch (c.Hecho)
        {
            case "Promedio" when c.Valor.ValueKind == JsonValueKind.Number:
                var limite = c.Valor.GetDecimal();
                var comparar = Comparador(c.Operador);
                return h => comparar(h.Promedio, limite);

            case "Interes" when c.Valor.ValueKind == JsonValueKind.String:
                var interes = c.Valor.GetString() ?? string.Empty;
                return c.Operador == "!="
                    ? h => !h.Intereses.Contains(interes, StringComparer.OrdinalIgnoreCase)
                    : h => h.Intereses.Contains(interes, StringComparer.OrdinalIgnoreCase);

            default:
                // Hecho desconocido (p. ej. los derivados de REG-004): no se puede afirmar => falso.
                return Nunca;
        }
    }

    /// <summary>Compone todas las condiciones de la regla con "Y" usando Aggregate.</summary>
    public static Func<Hechos, bool> Compilar(Regla regla) =>
        regla.Condiciones
            .Select(Compilar)
            .Aggregate(Siempre, (acumulada, siguiente) => h => acumulada(h) && siguiente(h));

    public static bool Cumple(Regla regla, Hechos hechos) => Compilar(regla)(hechos);

    /// <summary>Solo las reglas que concluyen una especialidad (REG-001..003).</summary>
    public static bool EsReglaDeEspecialidad(Regla r) =>
        r.Conclusion.Tipo == "EspecialidadRecomendada" && !string.IsNullOrEmpty(r.Conclusion.EspecialidadId);
}
