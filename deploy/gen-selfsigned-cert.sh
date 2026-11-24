#!/usr/bin/env bash
# Generate a self-signed TLS certificate for a quick start / internal testing.
# For production, use a CA-issued certificate (Let's Encrypt or your institution's PKI) instead.
#
# Usage: ./gen-selfsigned-cert.sh [hostname]   (default: localhost)
set -euo pipefail
HOST="${1:-localhost}"
DIR="$(cd "$(dirname "$0")" && pwd)/certs"
mkdir -p "$DIR"

if [[ -f "$DIR/fullchain.pem" && -f "$DIR/privkey.pem" ]]; then
  echo "Certs already exist in $DIR — remove them first to regenerate." >&2
  exit 0
fi

openssl req -x509 -nodes -newkey rsa:2048 -days 365 \
  -keyout "$DIR/privkey.pem" -out "$DIR/fullchain.pem" \
  -subj "/CN=$HOST" \
  -addext "subjectAltName=DNS:$HOST,DNS:localhost,IP:127.0.0.1"

chmod 600 "$DIR/privkey.pem"
echo "Self-signed cert for '$HOST' written to $DIR (valid 365 days)."
echo "Browsers will warn it is untrusted — expected for self-signed. Replace with a CA cert for production."
