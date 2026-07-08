#!/bin/bash

# AniCS Dynamic Installer for Linux
# Supports Arch Linux, CachyOS, Ubuntu, Debian, Fedora, etc.

BIN_PATH="/usr/local/bin/anics"
DEFAULT_REPO_DIR="$HOME/.local/share/anics-source"
REPO_URL="https://github.com/SteveenR-A/ani-cli-dotnet.git"

# Detectar si estamos ejecutando desde el repositorio local
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
IS_LOCAL_REPO=false
if [ -f "$SCRIPT_DIR/AniCS.slnx" ]; then
    IS_LOCAL_REPO=true
    SOURCE_DIR="$SCRIPT_DIR"
else
    SOURCE_DIR="$DEFAULT_REPO_DIR"
fi

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}======================================${NC}"
echo -e "${BLUE}       AniCS - Instalador Linux       ${NC}"
echo -e "${BLUE}======================================${NC}"

# Detect Package Manager
PM=""
if command -v pacman &> /dev/null; then
    PM="sudo pacman -S --needed --noconfirm"
    PM_REMOVE="sudo pacman -Rns"
elif command -v apt &> /dev/null; then
    PM="sudo apt install -y"
    PM_REMOVE="sudo apt remove -y"
elif command -v dnf &> /dev/null; then
    PM="sudo dnf install -y"
    PM_REMOVE="sudo dnf remove -y"
else
    echo -e "${YELLOW}Advertencia: Gestor de paquetes no soportado nativamente. Tendrás que instalar dependencias a mano.${NC}"
fi

check_and_install_deps() {
    deps=("mpv" "yt-dlp" "kitty")
    missing=()
    
    # Check dotnet SDK separately
    if ! command -v dotnet &> /dev/null; then
        echo -e "${RED}x dotnet-sdk no encontrado.${NC}"
        if command -v pacman &> /dev/null; then
            missing+=("dotnet-sdk")
        elif command -v apt &> /dev/null; then
            if apt-cache show dotnet-sdk-10.0 &> /dev/null; then
                missing+=("dotnet-sdk-10.0")
            elif apt-cache show dotnet-sdk-9.0 &> /dev/null; then
                missing+=("dotnet-sdk-9.0")
            elif apt-cache show dotnet-sdk-8.0 &> /dev/null; then
                missing+=("dotnet-sdk-8.0")
            else
                missing+=("dotnet-sdk-10.0")
            fi
        elif command -v dnf &> /dev/null; then
            if dnf list dotnet-sdk-10.0 &> /dev/null; then
                missing+=("dotnet-sdk-10.0")
            elif dnf list dotnet-sdk-9.0 &> /dev/null; then
                missing+=("dotnet-sdk-9.0")
            elif dnf list dotnet-sdk-8.0 &> /dev/null; then
                missing+=("dotnet-sdk-8.0")
            else
                missing+=("dotnet-sdk-10.0")
            fi
        else
            missing+=("dotnet-sdk")
        fi
    else
        echo -e "${GREEN}✓ dotnet-sdk detectado.${NC}"
    fi

    for dep in "${deps[@]}"; do
        if ! command -v $dep &> /dev/null; then
            echo -e "${RED}x $dep no encontrado.${NC}"
            missing+=("$dep")
        else
            echo -e "${GREEN}✓ $dep detectado.${NC}"
        fi
    done

    if [ ${#missing[@]} -ne 0 ]; then
        if [ -n "$PM" ]; then
            read -p "¿Deseas instalar las dependencias faltantes ahora? (${missing[*]}) [S/n]: " resp
            if [[ "$resp" == "S" || "$resp" == "s" || "$resp" == "" ]]; then
                echo -e "${BLUE}Instalando dependencias...${NC}"
                $PM "${missing[@]}"
            else
                echo -e "${YELLOW}Continuando sin dependencias (AniCS podría fallar al reproducir).${NC}"
            fi
        else
            echo -e "${YELLOW}Por favor, instala manualmente: ${missing[*]}${NC}"
        fi
    fi
}

install() {
    echo -e "${BLUE}--- Paso 1: Verificando Dependencias ---${NC}"
    check_and_install_deps

    echo -e "\n${BLUE}--- Paso 2: Obteniendo Código Fuente ---${NC}"
    if [ "$IS_LOCAL_REPO" = true ]; then
        echo "Usando código fuente local desde $SOURCE_DIR"
        cd "$SOURCE_DIR"
    else
        if [ -d "$SOURCE_DIR" ]; then
            echo "Actualizando repositorio..."
            cd "$SOURCE_DIR"
            git pull
        else
            echo "Clonando repositorio..."
            git clone "$REPO_URL" "$SOURCE_DIR"
            cd "$SOURCE_DIR"
        fi
    fi

    echo -e "\n${BLUE}--- Paso 3: Compilando AniCS CLI ---${NC}"
    echo "Esto tomará unos segundos..."
    dotnet publish src/AniCS.CLI/AniCS.CLI.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true

    echo -e "\n${BLUE}--- Paso 4: Instalando en el sistema ---${NC}"
    if [ -f "src/AniCS.CLI/bin/Release/net10.0/linux-x64/publish/AniCS.CLI" ]; then
        sudo cp "src/AniCS.CLI/bin/Release/net10.0/linux-x64/publish/AniCS.CLI" "$BIN_PATH"
    else
        sudo cp "src/AniCS.CLI/bin/Release/net10.0/linux-x64/publish/AniCS" "$BIN_PATH"
    fi
    sudo chmod +x "$BIN_PATH"

    echo -e "${GREEN}¡Instalación Completada!${NC}"
    echo -e "Escribe ${YELLOW}anics${NC} en la terminal para empezar."
}

update() {
    echo -e "${BLUE}--- Paso 1: Verificando Dependencias ---${NC}"
    check_and_install_deps

    echo -e "\n${BLUE}--- Paso 2: Actualizando Código Fuente desde GitHub ---${NC}"
    TEMP_REPO_DIR="$DEFAULT_REPO_DIR"
    
    if [ -d "$TEMP_REPO_DIR" ]; then
        rm -rf "$TEMP_REPO_DIR"
    fi

    echo "Clonando repositorio en $TEMP_REPO_DIR..."
    git clone "$REPO_URL" "$TEMP_REPO_DIR"
    cd "$TEMP_REPO_DIR"
    
    echo -e "\n${BLUE}--- Paso 3: Compilando AniCS CLI ---${NC}"
    echo -e "Compilando nueva versión..."
    dotnet publish src/AniCS.CLI/AniCS.CLI.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
    
    echo -e "\n${BLUE}--- Paso 4: Instalando en el sistema ---${NC}"
    if [ -f "src/AniCS.CLI/bin/Release/net10.0/linux-x64/publish/AniCS.CLI" ]; then
        sudo cp "src/AniCS.CLI/bin/Release/net10.0/linux-x64/publish/AniCS.CLI" "$BIN_PATH"
    else
        sudo cp "src/AniCS.CLI/bin/Release/net10.0/linux-x64/publish/AniCS" "$BIN_PATH"
    fi
    sudo chmod +x "$BIN_PATH"
    
    echo -e "${YELLOW}Limpiando código fuente temporal...${NC}"
    cd ~
    rm -rf "$TEMP_REPO_DIR"

    echo -e "${GREEN}¡AniCS CLI actualizado correctamente!${NC}"
}

uninstall() {
    echo -e "${RED}--- Desinstalando AniCS ---${NC}"
    if [ -f "$BIN_PATH" ]; then
        sudo rm "$BIN_PATH"
        echo "Ejecutable eliminado."
    fi

    if [ "$IS_LOCAL_REPO" = false ] && [ -d "$SOURCE_DIR" ] && [ "$SOURCE_DIR" = "$DEFAULT_REPO_DIR" ]; then
        rm -rf "$SOURCE_DIR"
        echo "Código fuente descargado eliminado."
    fi

    if [ -n "$PM_REMOVE" ]; then
        read -p "¿Deseas eliminar también las dependencias (mpv, yt-dlp, kitty)? [s/N]: " resp
        if [[ "$resp" == "S" || "$resp" == "s" ]]; then
            $PM_REMOVE mpv yt-dlp kitty
            echo -e "${GREEN}Dependencias eliminadas.${NC}"
        fi
    fi

    echo -e "${GREEN}¡Desinstalación completa!${NC} (Tu historial en ~/.config/anics se ha conservado)."
}

# Menu
echo "1) Instalar"
echo "2) Actualizar"
echo "3) Desinstalar"
echo "4) Salir"
read -p "Selecciona una opción [1-4]: " opc

case $opc in
    1) install ;;
    2) update ;;
    3) uninstall ;;
    *) echo "Saliendo..." ;;
esac
