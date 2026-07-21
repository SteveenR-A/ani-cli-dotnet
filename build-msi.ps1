$ErrorActionPreference = "Stop"

Write-Host "=== AniCS MSI Builder ===" -ForegroundColor Cyan
Write-Host "1. Compilando AniCS.Desktop (Release - win-x64)..." -ForegroundColor Yellow

# Compilar el proyecto tal y como lo espera el archivo Components.xml de WiX
dotnet clean src\AniCS.Desktop\AniCS.Desktop.csproj -c Release
dotnet publish src\AniCS.Desktop\AniCS.Desktop.csproj -c Release -r win-x64

Write-Host "`n2. Comprobando herramientas de WiX Toolset..." -ForegroundColor Yellow
try {
    # Check if wix is installed
    $wixInstalled = dotnet tool list -g | Select-String -Pattern "wix "
    if (-not $wixInstalled) {
        Write-Host "WiX Toolset no encontrado. Instalando herramienta global..." -ForegroundColor Magenta
        dotnet tool install --global wix --version 4.*
    }
} catch {
    Write-Host "Error al verificar/instalar WiX. Asegúrate de tener conexión." -ForegroundColor Red
}

Write-Host "`n3. Verificando extensión WixToolset.UI.wixext..." -ForegroundColor Yellow
try {
    wix extension add -g WixToolset.UI.wixext/4.0.5 > $null 2>&1
} catch { }

Write-Host "`n4. Construyendo el archivo .msi..." -ForegroundColor Yellow
# Usamos -ext para incluir la extensión de interfaz gráfica (WixUI_InstallDir)
wix build Installer\AniCS-Installer.wxs -ext WixToolset.UI.wixext -o Installer\AniCS-Installer.msi

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n===============================================" -ForegroundColor Green
    Write-Host "¡Éxito! El instalador se ha creado correctamente." -ForegroundColor Green
    Write-Host "Ubicación: Installer\AniCS-Installer.msi" -ForegroundColor Green
    Write-Host "===============================================" -ForegroundColor Green
} else {
    Write-Host "`nHubo un error en la creación del MSI. Revisa los logs de arriba." -ForegroundColor Red
}
