# AniCS Windows Installer Script
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "       AniCS - Instalador Windows      " -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

$InstallDir = "$env:LOCALAPPDATA\AniCS\bin"
$RepoDir = "$env:LOCALAPPDATA\AniCS\source"
$RepoUrl = "https://github.com/SteveenR-A/ani-cli-dotnet.git"

function Check-Dotnet {
    try {
        $dotnetVersion = dotnet --version
        Write-Host "[OK] dotnet-sdk detectado ($dotnetVersion)" -ForegroundColor Green
    } catch {
        Write-Host "[ERROR] dotnet-sdk no está instalado o no está en el PATH." -ForegroundColor Red
        Write-Host "Por favor instala .NET 10 SDK y vuelve a intentar." -ForegroundColor Yellow
        exit 1
    }
}

function Install-AniCS {
    Check-Dotnet

    Write-Host "`n--- Paso 1: Obteniendo Código Fuente ---" -ForegroundColor Cyan
    if (Test-Path $RepoDir) {
        Write-Host "Actualizando repositorio local..."
        Set-Location $RepoDir
        git pull | Out-Null
    } else {
        Write-Host "Clonando repositorio..."
        git clone $RepoUrl $RepoDir | Out-Null
        Set-Location $RepoDir
    }

    Write-Host "`n--- Paso 2: Compilando AniCS ---" -ForegroundColor Cyan
    Write-Host "Esto tomará unos segundos..."
    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Falló la compilación." -ForegroundColor Red
        exit 1
    }

    Write-Host "`n--- Paso 3: Instalando en el sistema ---" -ForegroundColor Cyan
    if (-not (Test-Path $InstallDir)) {
        New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    }

    $ExePath = "bin\Release\net10.0\win-x64\publish\AniCS.exe"
    Copy-Item -Path $ExePath -Destination "$InstallDir\anics.exe" -Force
    Write-Host "[OK] Copiado anics.exe a $InstallDir" -ForegroundColor Green

    # Añadir al PATH
    $UserPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ($UserPath -notlike "*$InstallDir*") {
        $NewPath = $UserPath + ";$InstallDir"
        [Environment]::SetEnvironmentVariable("PATH", $NewPath, "User")
        Write-Host "[OK] Agregado $InstallDir a tu PATH." -ForegroundColor Green
        Write-Host "`n[!] Importante: Reinicia tu consola (CMD o PowerShell) para que los cambios surtan efecto." -ForegroundColor Yellow
    }

    Write-Host "`n¡Instalación completada con éxito!" -ForegroundColor Green
    Write-Host "Ahora puedes abrir cualquier terminal en Windows y escribir: anics" -ForegroundColor Cyan
}

function Update-AniCS {
    if (-not (Test-Path $RepoDir)) {
        Write-Host "[ERROR] AniCS no parece estar instalado mediante este script." -ForegroundColor Red
        $resp = Read-Host "¿Deseas hacer una instalación limpia? [S/n]"
        if ($resp -eq "" -or $resp -match "^[Ss]") {
            Install-AniCS
        }
        return
    }

    Check-Dotnet
    Write-Host "`nActualizando AniCS..." -ForegroundColor Cyan
    Set-Location $RepoDir
    git pull | Out-Null

    Write-Host "Compilando nueva versión..."
    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Falló la compilación." -ForegroundColor Red
        exit 1
    }

    $ExePath = "bin\Release\net10.0\win-x64\publish\AniCS.exe"
    Copy-Item -Path $ExePath -Destination "$InstallDir\anics.exe" -Force
    Write-Host "¡AniCS actualizado correctamente!" -ForegroundColor Green
}

function Uninstall-AniCS {
    Write-Host "`n--- Desinstalando AniCS ---" -ForegroundColor Red
    
    if (Test-Path $InstallDir) {
        Remove-Item -Path $InstallDir -Recurse -Force
        Write-Host "Ejecutable y carpeta binaria eliminados." -ForegroundColor Green
    }

    if (Test-Path $RepoDir) {
        Remove-Item -Path $RepoDir -Recurse -Force
        Write-Host "Código fuente eliminado." -ForegroundColor Green
    }

    # Quitar del PATH
    $UserPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ($UserPath -like "*$InstallDir*") {
        $NewPath = ($UserPath -split ';' | Where-Object { $_ -ne $InstallDir }) -join ';'
        [Environment]::SetEnvironmentVariable("PATH", $NewPath, "User")
        Write-Host "Carpeta eliminada de la variable PATH del sistema." -ForegroundColor Green
        Write-Host "[!] Importante: Reinicia tu consola para que los cambios surtan efecto." -ForegroundColor Yellow
    }

    Write-Host "`n¡Desinstalación completa!" -ForegroundColor Green
    Write-Host "(Tu historial local de anime visto no se ha borrado)."
}

# Mostrar Menú
Write-Host "1) Instalar"
Write-Host "2) Actualizar"
Write-Host "3) Desinstalar"
Write-Host "4) Salir"
$opc = Read-Host "Selecciona una opción [1-4]"

switch ($opc) {
    '1' { Install-AniCS }
    '2' { Update-AniCS }
    '3' { Uninstall-AniCS }
    default { Write-Host "Saliendo..." }
}
