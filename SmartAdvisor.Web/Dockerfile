FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia toda la solución
COPY . .

# Restaura y publica apuntando correctamente a la subcarpeta
RUN dotnet restore "SmartAdvisor.Web/SmartAdvisor.Web.csproj"
RUN dotnet publish "SmartAdvisor.Web/SmartAdvisor.Web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "SmartAdvisor.Web.dll"]
