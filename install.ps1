# AniCS Windows Installer Script
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "       AniCS - Instalador Windows      " -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# 1. Comprobar dotnet
try {
    $dotnetVersion = dotnet --version
    Write-Host "[OK] dotnet-sdk detectado ($dotnetVersion)" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] dotnet-sdk no está instalado o no está en el PATH." -ForegroundColor Red
    Write-Host "Por favor instala .NET 10 SDK y vuelve a intentar." -ForegroundColor Yellow
    exit 1
}

# 2. Compilar
Write-Host "`nCompilando AniCS para Windows..." -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Falló la compilación." -ForegroundColor Red
    exit 1
}

# 3. Mover ejecutable a AppData
$InstallDir = "$env:LOCALAPPDATA\AniCS\bin"
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
}

$ExePath = "bin\Release\net10.0\win-x64\publish\AniCS.exe"
Copy-Item -Path $ExePath -Destination "$InstallDir\anics.exe" -Force
Write-Host "[OK] Copiado anics.exe a $InstallDir" -ForegroundColor Green

# 4. Añadir al PATH si no existe
$UserPath = [Environment]::GetEnvironmentVariable("PATH", "User")
if ($UserPath -notlike "*$InstallDir*") {
    $NewPath = $UserPath + ";$InstallDir"
    [Environment]::SetEnvironmentVariable("PATH", $NewPath, "User")
    Write-Host "[OK] Agregado $InstallDir a tu PATH." -ForegroundColor Green
    Write-Host "`n[!] Importante: Reinicia tu consola (CMD o PowerShell) para que los cambios surtan efecto." -ForegroundColor Yellow
}

Write-Host "`n¡Instalación completada con éxito!" -ForegroundColor Green
Write-Host "Ahora puedes abrir cualquier terminal en Windows y escribir: anics" -ForegroundColor Cyan
