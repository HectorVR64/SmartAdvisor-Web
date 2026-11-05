FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia toda la solución o el contenido del directorio raíz configurado
COPY . .

# Restaura las dependencias del proyecto web
RUN dotnet restore "SmartAdvisor.Web.csproj"

# Publica la aplicación
RUN dotnet publish "SmartAdvisor.Web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "SmartAdvisor.Web.dll"]
