using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartAdvisor.Core.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(servicios =>
    {
        // En Azure los archivos publicados quedan junto al ejecutable (AppContext.BaseDirectory).
        // Solo se usan como datos de ejemplo cuando la petición no trae sus propios datos (GET).
        var carpeta = Path.Combine(AppContext.BaseDirectory, "Data");
        servicios.AddSingleton(new RepositorioJson(carpeta));
    })
    .Build();

host.Run();
