#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="${ROOT_DIR:-/opt/b2b}"

cd "$ROOT_DIR"

echo "Pulling latest code..."
git pull

echo "Pulling images (GHCR override)..."
docker compose -f docker-compose.yml -f docker-compose.ghcr.yml pull

echo "Restarting..."
docker compose -f docker-compose.yml -f docker-compose.ghcr.yml up -d

echo "Health check..."
curl -fsS http://localhost:8080/health >/dev/null
curl -fsS http://localhost:8080/health/ready >/dev/null

echo "OK"

