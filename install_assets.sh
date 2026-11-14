#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
SRC="$SCRIPT_DIR/assets"
DST="$SCRIPT_DIR/bin/Debug/net10.0"

if [ ! -d "$SRC" ]; then
    echo "[ERROR] Source folder not found: $SRC" >&2
    exit 1
fi

mkdir -p "$DST"
rsync -a --delete "$SRC/" "$DST/"

echo "Sync done: $SRC -> $DST"