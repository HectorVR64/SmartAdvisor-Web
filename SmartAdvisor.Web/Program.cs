using System.Globalization;
using Microsoft.AspNetCore.Localization;
using SmartAdvisor.Core.Services;
using SmartAdvisor.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// ---- Carpeta de trabajo de los JSON -------------------------------------------------------
//  1) Si existe la configuración DataPath, se usa esa carpeta.
//  2) En Azure App Service (existe WEBSITE_SITE_NAME) se usa %HOME%\smart-advisor-data, que es
//     persistente y con permiso de escritura (las altas/bajas/cambios se guardan ahí).
//  3) En local se usa la carpeta Data/ del proyecto.
var semilla = Path.Combine(builder.Environment.ContentRootPath, "Data");
var home = Environment.GetEnvironmentVariable("HOME");
var trabajo = builder.Configuration["DataPath"]
    ?? (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")) && !string.IsNullOrEmpty(home)
        ? Path.Combine(home, "smart-advisor-data")
        : semilla);

if (!string.Equals(Path.GetFullPath(trabajo), Path.GetFullPath(semilla), StringComparison.OrdinalIgnoreCase))
{
    RepositorioJson.Sembrar(semilla, trabajo);
}

builder.Services.AddSingleton(new RepositorioJson(trabajo));

// ---- Dónde se ejecuta el análisis ---------------------------------------------------------
//  - Con StudentAnalyzer:BaseUrl  -> se llama a la Azure Function (StudentAnalyzerFunction).
//  - Sin ella                     -> se calcula en el propio sitio con el mismo código (SmartAdvisor.Core).
var urlFuncion = builder.Configuration["StudentAnalyzer:BaseUrl"];
builder.Services.AddHttpClient<AnalizadorAcademico>(cliente =>
{
    cliente.Timeout = TimeSpan.FromSeconds(15);
    if (!string.IsNullOrWhiteSpace(urlFuncion))
    {
        cliente.BaseAddress = new Uri(urlFuncion.TrimEnd('/') + "/");
    }
});

var app = builder.Build();

// Cultura fija: el punto decimal es siempre "." (evita errores al guardar promedios como 82.5).
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
    SupportedCultures = new[] { CultureInfo.InvariantCulture },
    SupportedUICultures = new[] { CultureInfo.InvariantCulture }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
