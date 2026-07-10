#!/bin/bash

# AniCS Desktop Installer for Linux
# Supports Arch Linux, CachyOS, Ubuntu, Debian, Fedora, etc.

APP_NAME="AniCS Desktop"
BIN_NAME="anics-desktop"
BIN_DIR="/opt/anics-desktop"
BIN_PATH="$BIN_DIR/$BIN_NAME"
DESKTOP_FILE="/usr/share/applications/anics-desktop.desktop"
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
echo -e "${BLUE}   AniCS Desktop - Instalador Linux   ${NC}"
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
    deps=("mpv" "yt-dlp")
    missing=()

    # Check dotnet SDK separately
    if ! command -v dotnet &> /dev/null; then
        echo -e "${RED}x dotnet-sdk no encontrado.${NC}"
        if command -v pacman &> /dev/null; then
            missing+=("dotnet-sdk")
        elif command -v apt &> /dev/null; then
            missing+=("dotnet-sdk-10.0") # Adjust version if needed
        elif command -v dnf &> /dev/null; then
            missing+=("dotnet-sdk-10.0")
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

    echo -e "\n${BLUE}--- Paso 3: Compilando AniCS Desktop ---${NC}"
    echo "Esto tomará unos segundos..."
    dotnet publish src/AniCS.Desktop/AniCS.Desktop.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

    echo -e "\n${BLUE}--- Paso 4: Instalando en el sistema ---${NC}"
    sudo mkdir -p "$BIN_DIR"
    
    PUBLISH_DIR="src/AniCS.Desktop/bin/Release/net10.0/linux-x64/publish"
    
    if [ -f "$PUBLISH_DIR/AniCS.Desktop" ]; then
        sudo cp "$PUBLISH_DIR/AniCS.Desktop" "$BIN_PATH"
        sudo cp "$PUBLISH_DIR/"*.so "$BIN_DIR/" 2>/dev/null || true
        if [ -f "src/AniCS.Desktop/Assets/Incono/favicon.png" ]; then
            sudo cp "src/AniCS.Desktop/Assets/Incono/favicon.png" "$BIN_DIR/icon.png"
        fi
    else
        echo -e "${RED}Error: No se encontró el ejecutable compilado.${NC}"
        exit 1
    fi
    
    sudo chmod +x "$BIN_PATH"
    
    # Crear archivo .desktop para el menú de aplicaciones
    echo -e "${BLUE}Creando acceso directo en el menú de aplicaciones...${NC}"
    sudo bash -c "cat > $DESKTOP_FILE" <<EOF
[Desktop Entry]
Name=AniCS
Comment=Cliente de Anime de Escritorio
Exec=env AVALONIA_USE_WAYLAND=0 $BIN_PATH
Icon=$BIN_DIR/icon.png
Terminal=false
Type=Application
Categories=AudioVideo;Player;Video;
EOF
    
    sudo chmod +x "$DESKTOP_FILE"

    echo -e "${GREEN}¡Instalación Completada!${NC}"
    echo -e "Puedes buscar '${YELLOW}AniCS${NC}' en el menú de aplicaciones de tu sistema."
}

update() {
    echo -e "${BLUE}--- Paso 1: Verificando Dependencias ---${NC}"
    check_and_install_deps

    echo -e "\n${BLUE}--- Paso 2: Actualizando Código Fuente desde GitHub ---${NC}"
    TEMP_REPO_DIR="$DEFAULT_REPO_DIR"

    if [ -d "$TEMP_REPO_DIR" ]; then
        echo "Actualizando repositorio en $TEMP_REPO_DIR..."
        cd "$TEMP_REPO_DIR"
        git fetch origin
        git reset --hard origin/main
    else
        echo "Clonando repositorio en $TEMP_REPO_DIR..."
        git clone "$REPO_URL" "$TEMP_REPO_DIR"
        cd "$TEMP_REPO_DIR"
    fi

    echo -e "\n${BLUE}--- Paso 3: Compilando AniCS Desktop ---${NC}"
    echo -e "Compilando nueva versión..."
    dotnet publish src/AniCS.Desktop/AniCS.Desktop.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

    echo -e "\n${BLUE}--- Paso 4: Instalando en el sistema ---${NC}"
    sudo mkdir -p "$BIN_DIR"
    PUBLISH_DIR="src/AniCS.Desktop/bin/Release/net10.0/linux-x64/publish"
    
    if [ -f "$PUBLISH_DIR/AniCS.Desktop" ]; then
        sudo cp "$PUBLISH_DIR/AniCS.Desktop" "$BIN_PATH"
        sudo cp "$PUBLISH_DIR/"*.so "$BIN_DIR/" 2>/dev/null || true
        sudo chmod +x "$BIN_PATH"
        if [ -f "src/AniCS.Desktop/Assets/Incono/favicon.png" ]; then
            sudo cp "src/AniCS.Desktop/Assets/Incono/favicon.png" "$BIN_DIR/icon.png"
        fi
    else
        echo -e "${RED}Error: No se encontró el ejecutable compilado.${NC}"
    fi

    echo -e "${GREEN}¡AniCS Desktop actualizado correctamente!${NC}"
}

uninstall() {
    echo -e "${RED}--- Desinstalando AniCS Desktop ---${NC}"
    if [ -d "$BIN_DIR" ]; then
        sudo rm -rf "$BIN_DIR"
        echo "Ejecutable y directorio eliminados."
    fi
    
    if [ -f "$DESKTOP_FILE" ]; then
        sudo rm "$DESKTOP_FILE"
        echo "Acceso directo del menú eliminado."
    fi

    if [ -d "$DEFAULT_REPO_DIR" ]; then
        rm -rf "$DEFAULT_REPO_DIR"
        echo "Código fuente descargado eliminado."
    fi

    read -p "¿Deseas eliminar también tus datos de usuario (configuración, historial y caché de portadas en ~/.local/share/AniCS)? [s/N]: " resp
    if [[ "$resp" == "S" || "$resp" == "s" ]]; then
        rm -rf ~/.local/share/AniCS
        echo "Datos de usuario eliminados."
    else
        echo "Tu historial y configuración en ~/.local/share/AniCS se han conservado."
    fi

    echo -e "${GREEN}¡Desinstalación completa!${NC}"
}

# Menu
echo "1) Instalar AniCS Desktop"
echo "2) Actualizar AniCS Desktop"
echo "3) Desinstalar AniCS Desktop"
echo "4) Salir"
read -p "Selecciona una opción [1-4]: " opc

case $opc in
    1) install ;;
    2) update ;;
    3) uninstall ;;
    *) echo "Saliendo..." ;;
esac
