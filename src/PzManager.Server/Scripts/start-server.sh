#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

DATA_DIR="$ROOT_DIR/Data"

STEAMCMD_DIR="$DATA_DIR/steamcmd"
SERVER_DIR="$DATA_DIR/pz-server"
CONFIG_DIR="$DATA_DIR/Zomboid"

SERVER_NAME="${PZ_SERVER_NAME:-ZomboidServer}"
PZ_ADMIN_USERNAME="${PZ_ADMIN_USERNAME:-}"
PZ_ADMIN_PASSWORD="${PZ_ADMIN_PASSWORD:-}"

bootstrap() {
    echo "Running bootstrap..."
    bash "$ROOT_DIR/Scripts/bootstrap.sh"
}

install_server() {
    echo "Installing Project Zomboid Dedicated Server..."

    "$STEAMCMD_DIR/steamcmd.sh" \
        +force_install_dir "$SERVER_DIR" \
        +login anonymous \
        +app_update 380870 validate \
        +quit
}

if [[ ! -f "$STEAMCMD_DIR/steamcmd.sh" ]]; then
    bootstrap
fi

if [[ ! -x "$SERVER_DIR/start-server.sh" ]]; then
    install_server
fi

mkdir -p "$CONFIG_DIR"

mkdir -p "$CONFIG_DIR/mods"

cd "$SERVER_DIR"

export PZ_ADMIN_USERNAME
export PZ_ADMIN_PASSWORD

exec ./start-server.sh \
    -cachedir="$CONFIG_DIR" \
    -servername "$SERVER_NAME" \
    "$@"