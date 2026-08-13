$ErrorActionPreference = "Stop"

Write-Host "=== Compilando APK (Debug/Local) ===" -ForegroundColor Cyan
Write-Host "Esto generará un APK firmado con la llave de desarrollo local, instalable en cualquier dispositivo." -ForegroundColor Yellow

# Evitar error CS2012 matando procesos residuales
Stop-Process -Name dotnet, msbuild, VBCSCompiler, AniCS -Force -ErrorAction SilentlyContinue

# Compilar el proyecto con ensamblados incrustados para funcionamiento autónomo sin Fast Deployment
dotnet build src\AniCS.Android\AniCS.Android.csproj -c Debug -f net10.0-android -p:UseSharedCompilation=false -p:EmbedAssembliesIntoApk=true -p:AndroidEnableFastDeployment=false

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n    (-) Copiando APK a la carpeta Installer..." -ForegroundColor Cyan
    $apkPath = Get-ChildItem -Path "src\AniCS.Android\bin\Debug\net10.0-android" -Filter "*-Signed.apk" -Recurse | Select-Object -First 1
    if (-not $apkPath) {
        $apkPath = Get-ChildItem -Path "src\AniCS.Android\bin\Debug\net10.0-android" -Filter "*.apk" -Recurse | Select-Object -First 1
    }

    if ($apkPath) {
        Copy-Item -Path $apkPath.FullName -Destination "Installer\AniCS-Android.apk" -Force
        Write-Host "`n===============================================" -ForegroundColor Green
        Write-Host "¡Éxito! APK local guardado en: Installer\AniCS-Android.apk" -ForegroundColor Green
        Write-Host "Recuerda: DESINSTALAR versiones anteriores en tu celular antes de instalar esta." -ForegroundColor Yellow
        Write-Host "===============================================" -ForegroundColor Green
    } else {
        Write-Host "No se encontró el archivo APK después de compilar." -ForegroundColor Red
    }
} else {
    Write-Host "La compilación falló." -ForegroundColor Red
}
