# Smart Academic Advisor — Parcial 2 (Equipo 6, Grupo 5701)

Núcleo funcional: dashboard académico, gestión de alumnos/materias/especialidades y estadísticas,
procesados con LINQ desde **StudentAnalyzerFunction** (Azure Function).

| Proyecto | Qué es |
|---|---|
| `SmartAdvisor.Core` | Modelos inmutables, `StudentAnalyzer` (LINQ), `EvaluadorReglas`, `Gestion`, `RepositorioJson` |
| `SmartAdvisor.Functions` | Azure Functions (aislado, .NET 8): `/api/health`, `/api/analisis`, `/api/alumnos/{id}/analisis` |
| `SmartAdvisor.Web` | Dashboard y CRUD (ASP.NET Core MVC + Razor) |
| `SmartAdvisor.Tests` | 24 pruebas xUnit |

## Secciones del sitio
| Sección | Qué hace |
|---|---|
| Dashboard y Estadísticas | Panorama del grupo y gráficas con las consultas LINQ |
| Alumnos, Materias, Especialidades | Administración (alta, cambio y baja) |
| Portal del alumno (`/portal-alumno`) | Cada alumno ve sus calificaciones, avance y recomendación |
| Portal del docente (`/portal-docente`) | El docente registra calificaciones, inscribe alumnos y ve las estadísticas de su grupo |
| Nosotros (`/nosotros`) | El equipo y cómo está construido |

El acceso a los portales es de demostración: se elige el nombre, sin contraseña. Los docentes están en `SmartAdvisor.Web/Data/docentes.json` (datos ficticios).

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

## Paradigma lógico (inferencia)

Además del análisis funcional, el sistema razona con **hechos y reglas** (cláusulas de Horn):

- `SmartAdvisor.Functions/Data/hechos.json`: base de hechos del perfil académico (`promedio(ana, 95)`, `interes(ana, ia)`, ...).
- `SmartAdvisor.Functions/Data/reglas_logicas.json`: base de reglas (`EspecialidadIA`, `EspecialidadDesarrollo`, `EspecialidadDatos`, ...).
- `SmartAdvisor.Core/Logica/`: unificación, resolución con retroceso, negación por fallo y pruebas.
- `AcademicInferenceFunction` (Azure Function):
  - `GET /api/inferencia/{alumno}`: ¿qué especialidad se recomienda? ¿qué materias debería cursar?
  - `GET /api/logica/consulta?q=recomienda(ana,E)`: consulta libre.
  - `GET /api/logica/base`: hechos y reglas cargados.
