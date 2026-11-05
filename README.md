# Smart Academic Advisor — Parcial 2 (Equipo 6, Grupo 5701)

Núcleo funcional: dashboard académico, gestión de alumnos/materias/especialidades y estadísticas,
procesados con LINQ desde **StudentAnalyzerFunction** (Azure Function).

| Proyecto | Qué es |
|---|---|
| `SmartAdvisor.Core` | Modelos inmutables, `StudentAnalyzer` (LINQ), `EvaluadorReglas`, `Gestion`, `RepositorioJson` |
| `SmartAdvisor.Functions` | Azure Functions (aislado, .NET 8): `/api/health`, `/api/analisis`, `/api/alumnos/{id}/analisis` |
| `SmartAdvisor.Web` | Dashboard y CRUD (ASP.NET Core MVC + Razor) |
| `SmartAdvisor.Tests` | 24 pruebas xUnit |

## Ejecutar en local
1. Requisitos: .NET 8 SDK y (para la Function) Azure Functions Core Tools v4.
2. Pruebas: `dotnet test`
3. Function: `cd SmartAdvisor.Functions` y `func start` (puerto 7071).
4. Web: en otra terminal `cd SmartAdvisor.Web` y `dotnet run` (http://localhost:5080).
5. Para que la Web use la Function local: `$env:StudentAnalyzer__BaseUrl="http://localhost:7071"` antes de `dotnet run`.
   Si la variable está vacía o la Function no responde, la Web calcula localmente y lo avisa.

## Desplegar en Azure
Ejecuta `.\desplegar-azure.ps1` (ver la sección 11 de `docs/Documentacion_LINQ_Equipo6.pdf`).

## Documentación
`docs/Documentacion_LINQ_Equipo6.pdf` (y `.docx`): cada consulta LINQ, funciones puras, evaluación de expresiones, contrato HTTP, pruebas y despliegue.
