#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

DATA_DIR="$ROOT_DIR/Data"
STEAMCMD_DIR="$DATA_DIR/steamcmd"
SERVER_DIR="$DATA_DIR/pz-server"

require_root() {
    if [[ $EUID -eq 0 ]]; then
        SUDO=""
    elif command -v sudo >/dev/null; then
        SUDO="sudo"
    else
        echo "This script requires root privileges."
        exit 1
    fi
}

install_dependencies() {

    if [[ -f /lib/ld-linux.so.2 ]] || [[ -f /lib32/ld-linux.so.2 ]]; then
        echo "32-bit runtime already installed."
        return
    fi

    echo "Installing system dependencies..."

    if command -v apt-get >/dev/null; then

        $SUDO dpkg --add-architecture i386
        $SUDO apt-get update

        $SUDO apt-get install -y \
            curl \
            tar \
            ca-certificates \
            libc6:i386 \
            libstdc++6:i386 \
            lib32gcc-s1

        return
    fi

    if command -v dnf >/dev/null; then

        $SUDO dnf install -y \
            curl \
            tar \
            glibc.i686 \
            libstdc++.i686

        return
    fi

    if command -v yum >/dev/null; then

        $SUDO yum install -y \
            curl \
            tar \
            glibc.i686 \
            libstdc++.i686

        return
    fi

    if command -v pacman >/dev/null; then

        $SUDO pacman -Sy --noconfirm \
            curl \
            tar \
            lib32-glibc \
            lib32-gcc-libs

        return
    fi

    echo "Unsupported Linux distribution."
    exit 1
}

install_steamcmd() {

    mkdir -p "$STEAMCMD_DIR"

    if [[ -f "$STEAMCMD_DIR/steamcmd.sh" ]]; then
        echo "SteamCMD already installed."
        return
    fi

    echo "Downloading SteamCMD..."

    TMP="$(mktemp)"

    curl -fsSL \
        https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz \
        -o "$TMP"

    tar -xzf "$TMP" -C "$STEAMCMD_DIR"

    rm "$TMP"
}

require_root
install_dependencies
install_steamcmd

echo
echo "Bootstrap completed."
echo
echo "Run Scripts/install-server.sh to install Project Zomboid."