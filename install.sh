#!/bin/bash

# AniCS Dynamic Installer for Linux
# Supports Arch Linux, CachyOS, Ubuntu, Debian, Fedora, etc.

BIN_PATH="/usr/local/bin/anics"
REPO_DIR="$HOME/.local/share/anics-source"
REPO_URL="https://github.com/SteveenR-A/ani-cli-dotnet.git"

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
        missing+=("dotnet-sdk")
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
    if [ -d "$REPO_DIR" ]; then
        echo "Actualizando repositorio..."
        cd "$REPO_DIR"
        git pull
    else
        echo "Clonando repositorio..."
        git clone "$REPO_URL" "$REPO_DIR"
        cd "$REPO_DIR"
    fi

    echo -e "\n${BLUE}--- Paso 3: Compilando AniCS ---${NC}"
    echo "Esto tomará unos segundos..."
    dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true

    echo -e "\n${BLUE}--- Paso 4: Instalando en el sistema ---${NC}"
    sudo cp "bin/Release/net10.0/linux-x64/publish/AniCS" "$BIN_PATH"
    sudo chmod +x "$BIN_PATH"

    echo -e "${GREEN}¡Instalación Completada!${NC}"
    echo -e "Escribe ${YELLOW}anics${NC} en la terminal para empezar."
}

update() {
    if [ ! -d "$REPO_DIR" ]; then
        echo -e "${RED}AniCS no parece estar instalado mediante este script.${NC}"
        read -p "¿Deseas hacer una instalación limpia? [S/n]: " resp
        if [[ "$resp" == "S" || "$resp" == "s" || "$resp" == "" ]]; then
            install
        fi
        exit 0
    fi

    echo -e "${BLUE}Actualizando AniCS...${NC}"
    cd "$REPO_DIR"
    git pull
    
    echo -e "Compilando nueva versión..."
    dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
    
    sudo cp "bin/Release/net10.0/linux-x64/publish/AniCS" "$BIN_PATH"
    sudo chmod +x "$BIN_PATH"
    echo -e "${GREEN}¡AniCS actualizado correctamente!${NC}"
}

uninstall() {
    echo -e "${RED}--- Desinstalando AniCS ---${NC}"
    if [ -f "$BIN_PATH" ]; then
        sudo rm "$BIN_PATH"
        echo "Ejecutable eliminado."
    fi

    if [ -d "$REPO_DIR" ]; then
        rm -rf "$REPO_DIR"
        echo "Código fuente eliminado."
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
