#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="${ROOT_DIR:-/opt/b2b}"
SOURCE_DIR="${SOURCE_DIR:-$ROOT_DIR/backups}"

# Destination can be:
# - local mount path (recommended): /mnt/backup/b2b
# - remote via ssh (advanced): user@host:/path  (requires ssh keys + rsync)
DEST="${DEST:-/mnt/backup/b2b}"

if [[ ! -d "$SOURCE_DIR" ]]; then
  echo "Missing source dir: $SOURCE_DIR" >&2
  exit 1
fi

echo "Mirroring backups:"
echo "  from: $SOURCE_DIR/"
echo "  to:   $DEST/"

# Ensure destination exists for local paths (best-effort).
if [[ "$DEST" != *:* ]]; then
  sudo mkdir -p "$DEST"
fi

rsync -a --delete --human-readable --stats "$SOURCE_DIR"/ "$DEST"/

