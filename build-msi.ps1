$ErrorActionPreference = "Stop"

Write-Host "=== AniCS MSI Builder ===" -ForegroundColor Cyan
Write-Host "1. Compilando AniCS.Desktop (Release - win-x64)..." -ForegroundColor Yellow

# Compilar el proyecto tal y como lo espera el archivo Components.xml de WiX
dotnet clean src\AniCS.Desktop\AniCS.Desktop.csproj -c Release
dotnet publish src\AniCS.Desktop\AniCS.Desktop.csproj -c Release -r win-x64

Write-Host "`n1b. Generando componentes dinámicos de empaquetado (AppComponents.g.wxs)..." -ForegroundColor Yellow

$publishDir = (Get-Item "src\AniCS.Desktop\bin\Release\net10.0\win-x64\publish").FullName
$files = Get-ChildItem -Path $publishDir -Recurse -File
$directories = Get-ChildItem -Path $publishDir -Recurse -Directory | Sort-Object FullName

$dirMap = @{}
$dirMap[$publishDir] = "INSTALLFOLDER"

foreach ($dir in $directories) {
    $relPath = $dir.FullName.Substring($publishDir.Length).TrimStart('\', '/')
    $safeDirId = "dir_" + ($relPath -replace '[^a-zA-Z0-9_]', '_')
    $dirMap[$dir.FullName] = $safeDirId
}

function Build-DirTreeXml($parentPath) {
    $subDirs = Get-ChildItem -Path $parentPath -Directory
    $xml = ""
    foreach ($sub in $subDirs) {
        $id = $dirMap[$sub.FullName]
        $name = [System.Security.SecurityElement]::Escape($sub.Name)
        $childXml = Build-DirTreeXml $sub.FullName
        if ($childXml) {
            $xml += "      <Directory Id=`"$id`" Name=`"$name`">`n$childXml      </Directory>`n"
        } else {
            $xml += "      <Directory Id=`"$id`" Name=`"$name`" />`n"
        }
    }
    return $xml
}

$topLevelDirsXml = Build-DirTreeXml $publishDir

$compXml = [System.Text.StringBuilder]::new()
$fileCounter = 0
foreach ($file in $files) {
    $fileCounter++
    $dirId = $dirMap[$file.Directory.FullName]
    $safeFileId = "f_$fileCounter"
    
    if ($file.Name -eq "AniCS.Desktop.exe" -and $dirId -eq "INSTALLFOLDER") {
        $safeFileId = "AniCSEXE"
    }

    $sourcePath = [System.Security.SecurityElement]::Escape($file.FullName)
    [void]$compXml.AppendLine("      <Component Directory=`"$dirId`" Guid=`"*`">")
    [void]$compXml.AppendLine("        <File Id=`"$safeFileId`" Source=`"$sourcePath`" KeyPath=`"yes`" />")
    [void]$compXml.AppendLine("      </Component>")
}

$wixHarvestXml = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
    <DirectoryRef Id="INSTALLFOLDER">
$topLevelDirsXml    </DirectoryRef>
    <ComponentGroup Id="AppComponents">
$($compXml.ToString())    </ComponentGroup>
  </Fragment>
</Wix>
"@

Set-Content -Path "Installer\AppComponents.g.wxs" -Value $wixHarvestXml -Encoding UTF8

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
# Empaquetar mpv/yt-dlp solo si están presentes en InstallerDependencies (local).
# En CI/GitHub Actions se omite la compilación al no existir, evitando errores.
$bundleExternals = "0"
if (Test-Path "Installer\InstallerDependencies\yt-dlp.exe") {
    $bundleExternals = "1"
    Write-Host "    (-) Se empaquetarán mpv.exe y yt-dlp.exe junto al instalador." -ForegroundColor Cyan
} else {
    Write-Host "    (-) InstallerDependencies vacío: se omiten mpv/yt-dlp del MSI." -ForegroundColor DarkGray
}

# Usamos -ext para incluir la extensión de interfaz gráfica (WixUI_InstallDir)
wix build Installer\AniCS-Installer.wxs Installer\AppComponents.g.wxs -ext WixToolset.UI.wixext -d BundleExternals="$bundleExternals" -o Installer\AniCS-Installer.msi

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n===============================================" -ForegroundColor Green
    Write-Host "¡Éxito! El instalador se ha creado correctamente." -ForegroundColor Green
    Write-Host "Ubicación: Installer\AniCS-Installer.msi" -ForegroundColor Green
    Write-Host "===============================================" -ForegroundColor Green
} else {
    Write-Host "`nHubo un error en la creación del MSI. Revisa los logs de arriba." -ForegroundColor Red
}
