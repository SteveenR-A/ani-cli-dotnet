# AniCS Windows Installer Script
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "       AniCS - Instalador Windows      " -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

$InstallDir = "$env:LOCALAPPDATA\AniCS\bin"
$DefaultRepoDir = "$env:LOCALAPPDATA\AniCS\source"
$RepoUrl = "https://github.com/SteveenR-A/ani-cli-dotnet.git"

# Detectar si estamos ejecutando desde el repositorio local con el código fuente
$IsLocalRepo = $false
if ($PSScriptRoot -and (Test-Path (Join-Path $PSScriptRoot "AniCS.slnx"))) {
    $IsLocalRepo = $true
    $SourceDir = $PSScriptRoot
} else {
    $SourceDir = $DefaultRepoDir
}

function Check-Dotnet {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Host "[ERROR] dotnet-sdk no está instalado o no está en el PATH." -ForegroundColor Red
        Write-Host "Por favor instala .NET 10 SDK y vuelve a intentar." -ForegroundColor Yellow
        exit 1
    }

    try {
        $dotnetVersion = & dotnet --version 2>&1
        if ($LASTEXITCODE -ne 0) { throw "dotnet --version falló" }
        Write-Host "[OK] dotnet-sdk detectado ($dotnetVersion)" -ForegroundColor Green
    } catch {
        Write-Host "[ERROR] No se pudo verificar la versión de dotnet." -ForegroundColor Red
        exit 1
    }
}

function Check-Msvc {
    # NativeAOT en Windows requiere las Build Tools de C++ (cl.exe / link.exe).
    # Detectamos Visual Studio / Build Tools vía vswhere.
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2>$null
        if ($LASTEXITCODE -eq 0 -and $vsPath) {
            Write-Host "[OK] MSVC (Visual Studio Build Tools) detectado." -ForegroundColor Green
            return
        }
    }

    # Fallback: buscar cl.exe directamente en el PATH
    if (Get-Command cl.exe -ErrorAction SilentlyContinue) {
        Write-Host "[OK] cl.exe detectado en el PATH." -ForegroundColor Green
        return
    }

    Write-Host "[ERROR] No se detectó MSVC (Visual Studio Build Tools con la carga de trabajo 'Desarrollo para escritorio con C++')." -ForegroundColor Red
    Write-Host "AniCS.CLI usa NativeAOT y necesita cl.exe/link.exe para compilar." -ForegroundColor Yellow
    Write-Host "Instala Visual Studio Build Tools (o Visual Studio 2022) y marca el componente 'Desarrollo para escritorio con C++', luego reintenta." -ForegroundColor Yellow
    exit 1
}

function Install-AniCS {
    Check-Dotnet
    Check-Msvc

    Write-Host "`n--- Paso 1: Obteniendo Código Fuente ---" -ForegroundColor Cyan
    if ($IsLocalRepo) {
        Write-Host "Usando código fuente local desde $SourceDir"
        Set-Location $SourceDir
    } else {
        if (Test-Path $SourceDir) {
            Write-Host "Actualizando repositorio local..."
            Set-Location $SourceDir
            git pull | Out-Null
        } else {
            Write-Host "Clonando repositorio..."
            git clone $RepoUrl $SourceDir | Out-Null
            Set-Location $SourceDir
        }
    }

    Write-Host "`n--- Paso 2: Compilando AniCS CLI ---" -ForegroundColor Cyan
    Write-Host "Esto tomará unos segundos..."
    dotnet publish src/AniCS.CLI/AniCS.CLI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Falló la compilación." -ForegroundColor Red
        exit 1
    }

    Write-Host "`n--- Paso 3: Instalando en el sistema ---" -ForegroundColor Cyan
    if (-not (Test-Path $InstallDir)) {
        New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    }

    $ExePath = "src\AniCS.CLI\bin\Release\net10.0\win-x64\publish\AniCS.CLI.exe"
    if (-not (Test-Path $ExePath)) {
        $ExePath = "src\AniCS.CLI\bin\Release\net10.0\win-x64\publish\AniCS.exe"
    }
    Copy-Item -Path $ExePath -Destination "$InstallDir\anics.exe" -Force
    Write-Host "[OK] Copiado anics.exe a $InstallDir" -ForegroundColor Green

    # Añadir al PATH
    $UserPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ($UserPath -notlike "*$InstallDir*") {
        $NewPath = $UserPath + ";$InstallDir"
        [Environment]::SetEnvironmentVariable("PATH", $NewPath, "User")
        Write-Host "[OK] Agregado $InstallDir a tu PATH." -ForegroundColor Green
        Write-Host "`n[!] Importante: Reinicia tu consola (CMD o PowerShell) para que los cambios surtan efecto globalmente." -ForegroundColor Yellow
    }
    
    # Añadirlo también a la sesión actual para que funcione de inmediato sin reiniciar
    if ($env:PATH -notlike "*$InstallDir*") {
        $env:PATH += ";$InstallDir"
    }

    Write-Host "`n¡Instalación completada con éxito!" -ForegroundColor Green
    Write-Host "Ahora puedes abrir cualquier terminal en Windows y escribir: anics" -ForegroundColor Cyan
}

function Update-AniCS {
    Check-Dotnet
    Check-Msvc
    Write-Host "`nActualizando AniCS CLI desde GitHub..." -ForegroundColor Cyan

    $TempRepoDir = $DefaultRepoDir
    
    if (Test-Path $TempRepoDir) {
        Remove-Item -Path $TempRepoDir -Recurse -Force
    }

    Write-Host "Clonando repositorio en $TempRepoDir..."
    git clone $RepoUrl $TempRepoDir | Out-Null
    
    Set-Location $TempRepoDir

    Write-Host "Compilando nueva versión de la CLI..."
    dotnet publish src/AniCS.CLI/AniCS.CLI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Falló la compilación." -ForegroundColor Red
        exit 1
    }

    $ExePath = "src\AniCS.CLI\bin\Release\net10.0\win-x64\publish\AniCS.CLI.exe"
    if (-not (Test-Path $ExePath)) {
        $ExePath = "src\AniCS.CLI\bin\Release\net10.0\win-x64\publish\AniCS.exe"
    }
    
    if (-not (Test-Path $InstallDir)) {
        New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    }
    Copy-Item -Path $ExePath -Destination "$InstallDir\anics.exe" -Force
    
    Write-Host "Limpiando código fuente temporal..." -ForegroundColor Yellow
    Set-Location $env:LOCALAPPDATA # Salir de la carpeta antes de borrarla
    Remove-Item -Path $TempRepoDir -Recurse -Force

    Write-Host "¡AniCS CLI actualizado correctamente!" -ForegroundColor Green
}

function Uninstall-AniCS {
    Write-Host "`n--- Desinstalando AniCS ---" -ForegroundColor Red
    
    if (Test-Path $InstallDir) {
        Remove-Item -Path $InstallDir -Recurse -Force
        Write-Host "Ejecutable y carpeta binaria eliminados." -ForegroundColor Green
    }

    # Solo eliminamos la carpeta de código fuente descargada por el script (evita borrar el repo si es local)
    if (-not $IsLocalRepo -and (Test-Path $SourceDir) -and ($SourceDir -eq $DefaultRepoDir)) {
        Remove-Item -Path $SourceDir -Recurse -Force
        Write-Host "Código fuente descargado eliminado." -ForegroundColor Green
    } else {
        Write-Host "Código fuente local conservado." -ForegroundColor Yellow
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
