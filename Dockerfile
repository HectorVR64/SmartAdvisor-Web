FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia todo el repositorio al contenedor
COPY . .

# Entra a la carpeta del proyecto web para ejecutar la restauración y publicación ahí
WORKDIR /src/SmartAdvisor.Web

RUN dotnet restore "SmartAdvisor.Web.csproj"
RUN dotnet publish "SmartAdvisor.Web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "SmartAdvisor.Web.dll"]
