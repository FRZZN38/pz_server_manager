#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

DATA_DIR="$ROOT_DIR/Data"
STEAMCMD_DIR="$DATA_DIR/steamcmd"
SERVER_DIR="$DATA_DIR/pz-server"

mkdir -p "$SERVER_DIR"

if [[ ! -x "$STEAMCMD_DIR/steamcmd.sh" ]]; then
    echo "SteamCMD is not installed."
    echo "Run Scripts/bootstrap.sh first."
    exit 1
fi

cat >"$DATA_DIR/install_server.scmd" <<EOF
@ShutdownOnFailedCommand 1
@NoPromptForPassword 1
force_install_dir $SERVER_DIR
login anonymous
app_update 380870 validate
quit
EOF

echo "Installing / Updating Project Zomboid Dedicated Server..."

"$STEAMCMD_DIR/steamcmd.sh" +runscript "$DATA_DIR/install_server.scmd"

echo
echo "Project Zomboid Dedicated Server is ready."