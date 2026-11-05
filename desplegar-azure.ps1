<#
  Smart Academic Advisor - Despliegue en Azure (PowerShell)
  Crea y publica: 1) Azure Function (StudentAnalyzerFunction)  2) Sitio web MVC (dashboard)

  Requisitos (ya los usaste en Series Insights): Azure CLI (az), .NET 8 SDK y Azure Functions Core Tools v4 (func).
  Ejecuta este archivo DESDE la carpeta que contiene SmartAdvisor.sln:
      cd "C:\...\PROYECTO 2\SmartAdvisor"
      .\desplegar-azure.ps1
  Si PowerShell bloquea el script:  Set-ExecutionPolicy -Scope Process Bypass
#>

# ---- 1) Nombres (los de Storage, Function y Web deben ser ÚNICOS en todo Azure; cambia $sufijo si ya existen)
$sufijo    = "jcperez"
$grupo     = "rg-smart-advisor"
$ubicacion = "northcentralus"
$storage   = "stadvisor$sufijo"          # solo minúsculas y números, 3 a 24 caracteres
$funcApp   = "func-advisor-$sufijo"
$plan      = "asp-advisor-f1"
$webApp    = "web-advisor-$sufijo"

$ErrorActionPreference = "Stop"
function Paso($t) { Write-Host "`n=== $t ===" -ForegroundColor Cyan }

Paso "Iniciar sesión en Azure"
az login | Out-Null
az account show --query "{suscripcion:name, usuario:user.name}" -o table

Paso "Grupo de recursos"
az group create --name $grupo --location $ubicacion -o table

Paso "Cuenta de almacenamiento (la pide Azure Functions)"
az storage account create --name $storage --resource-group $grupo --location $ubicacion --sku Standard_LRS -o table

Paso "Function App (plan de consumo, .NET 8 aislado)"
az functionapp create --name $funcApp --resource-group $grupo --storage-account $storage `
    --consumption-plan-location $ubicacion --runtime dotnet-isolated --runtime-version 8 `
    --functions-version 4 --os-type Windows -o table

Paso "Publicar StudentAnalyzerFunction"
Push-Location SmartAdvisor.Functions
func azure functionapp publish $funcApp
Pop-Location

$urlFuncion = "https://$funcApp.azurewebsites.net"
Paso "Probar la Function: $urlFuncion/api/health"
Start-Sleep -Seconds 10
Invoke-RestMethod "$urlFuncion/api/health" | ConvertTo-Json

Paso "Plan gratuito (F1) y Web App"
az appservice plan create --name $plan --resource-group $grupo --location $ubicacion --sku F1 -o table
az webapp create --name $webApp --resource-group $grupo --plan $plan --runtime "dotnet:8" -o table

Paso "Conectar el sitio con la Function"
az webapp config appsettings set --name $webApp --resource-group $grupo `
    --settings "StudentAnalyzer__BaseUrl=$urlFuncion" -o table

Paso "Compilar y publicar el sitio"
if (Test-Path publish) { Remove-Item publish -Recurse -Force }
dotnet publish SmartAdvisor.Web -c Release -o publish\web
Compress-Archive -Path publish\web\* -DestinationPath publish\web.zip -Force
az webapp deploy --name $webApp --resource-group $grupo --src-path publish\web.zip --type zip

Paso "Listo"
Write-Host "Function : $urlFuncion/api/analisis"
Write-Host "Sitio    : https://$webApp.azurewebsites.net"
Write-Host "En el dashboard debe verse el chip 'Procesado por: Azure Function'."
Start-Process "https://$webApp.azurewebsites.net"
