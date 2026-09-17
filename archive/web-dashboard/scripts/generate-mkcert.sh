#!/usr/bin/env bash
# Génère le certificat TLS local (mkcert) pour le reverse proxy Nginx.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CERT_DIR="${ROOT}/certs"
HOST_NAME="${GAIA_HOST:-gaia.local}"
LAN_IP="${GAIA_LAN_IP:-}"

mkdir -p "$CERT_DIR"

install_mkcert() {
    if command -v mkcert >/dev/null 2>&1; then
        return
    fi
    echo "mkcert introuvable, tentative d'installation…"
    if command -v apt-get >/dev/null 2>&1; then
        sudo apt-get update
        sudo apt-get install -y libnss3-tools wget
        curl -fsSL -o /tmp/mkcert "https://github.com/FiloSottile/mkcert/releases/latest/download/mkcert-v1.4.4-linux-amd64"
        sudo install -m 0755 /tmp/mkcert /usr/local/bin/mkcert
    elif command -v brew >/dev/null 2>&1; then
        brew install mkcert nss
    else
        echo "Installez mkcert manuellement : https://github.com/FiloSottile/mkcert" >&2
        exit 1
    fi
}

detect_lan_ip() {
    hostname -I 2>/dev/null | awk '{print $1}'
}

install_mkcert
mkcert -install

if [ -z "$LAN_IP" ]; then
    LAN_IP="$(detect_lan_ip)"
fi
if [ -z "$LAN_IP" ]; then
    echo "Impossible de détecter l'IP LAN. Exportez GAIA_LAN_IP." >&2
    exit 1
fi

echo "Noms du certificat : ${HOST_NAME} ${LAN_IP} localhost 127.0.0.1"
mkcert -cert-file "${CERT_DIR}/gaia.pem" -key-file "${CERT_DIR}/gaia-key.pem" \
    "$HOST_NAME" "$LAN_IP" localhost 127.0.0.1

cp "$(mkcert -CAROOT)/rootCA.pem" "${CERT_DIR}/rootCA.pem"
echo "Certificats écrits dans ${CERT_DIR}"
echo "CA publique à copier sur les téléphones : ${CERT_DIR}/rootCA.pem"
echo "Ne commitez jamais gaia-key.pem ni rootCA-key.pem."
