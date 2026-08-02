#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEPLOY_DIR="$ROOT_DIR/deploy"
COMPOSE_FILE="$DEPLOY_DIR/docker-compose.yml"
SERVICE_NAME="lessie-front"
CONTAINER_NAME="lessie-front"
URL="http://127.0.0.1:81"

echo "========================================"
echo " Publicação local da Lessie"
echo "========================================"

cd "$DEPLOY_DIR"

echo
echo "[1/4] Construindo a imagem..."
docker compose -f "$COMPOSE_FILE" build "$SERVICE_NAME"

echo
echo "[2/4] Atualizando o container..."
docker compose -f "$COMPOSE_FILE" up \
    -d \
    --no-deps \
    --force-recreate \
    "$SERVICE_NAME"

echo
echo "[3/4] Aguardando o frontend responder..."

HTTP_STATUS=""

for tentativa in {1..20}; do
    HTTP_STATUS="$(curl \
        --silent \
        --output /dev/null \
        --write-out '%{http_code}' \
        "$URL" || true)"

    if [[ "$HTTP_STATUS" == "200" ]]; then
        echo "Frontend respondeu HTTP 200."
        break
    fi

    echo "Tentativa $tentativa/20: HTTP ${HTTP_STATUS:-sem resposta}"
    sleep 2
done

if [[ "$HTTP_STATUS" != "200" ]]; then
    echo
    echo "ERRO: o frontend não respondeu corretamente."
    echo
    docker compose -f "$COMPOSE_FILE" ps
    echo
    docker compose -f "$COMPOSE_FILE" logs --tail=100 "$SERVICE_NAME"
    exit 1
fi

echo
echo "[4/4] Exibindo estado final..."
docker compose -f "$COMPOSE_FILE" ps "$SERVICE_NAME"

echo
echo "Lessie publicada em:"
echo "  http://monstrinho.local:81"
echo
echo "Publicação concluída."