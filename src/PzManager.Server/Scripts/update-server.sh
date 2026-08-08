#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

DATA_DIR="$ROOT_DIR/Data"

STEAMCMD_DIR="$DATA_DIR/steamcmd"
SERVER_DIR="$DATA_DIR/pz-server"

if [[ ! -f "$STEAMCMD_DIR/steamcmd.sh" ]]; then
    echo "SteamCMD is not installed."
    exit 1
fi

echo "Updating Project Zomboid Dedicated Server..."

"$STEAMCMD_DIR/steamcmd.sh" \
    +force_install_dir "$SERVER_DIR" \
    +login anonymous \
    +app_update 380870 validate \
    +quit

echo "Update completed."