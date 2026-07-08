#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
CERT_PATH="${VITE_DEV_HTTPS_CERT:-${REPO_ROOT}/.certs/localhost.pem}"
KEY_PATH="${VITE_DEV_HTTPS_KEY:-${REPO_ROOT}/.certs/localhost-key.pem}"

if [[ ! -f "${CERT_PATH}" || ! -f "${KEY_PATH}" ]]; then
  cat >&2 <<'MSG'
Local HTTPS certificate files are required for Teams app local testing.
Create them with:
  brew install mkcert
  mkcert -install
  mkdir -p .certs
  mkcert -key-file .certs/localhost-key.pem -cert-file .certs/localhost.pem localhost 127.0.0.1 ::1

Certificate and key files must stay local and must not be committed.
MSG
  exit 1
fi

export UAT_FRONTEND_HTTPS=true
export UAT_LOAD_NOTIFY_LOCAL_ENV=true
export VITE_DEV_HTTPS=true
export VITE_DEV_HTTPS_CERT="${CERT_PATH}"
export VITE_DEV_HTTPS_KEY="${KEY_PATH}"
export UAT_FRONTEND_HTTPS_API_BASE_URL="${UAT_FRONTEND_HTTPS_API_BASE_URL:-}"
export VITE_DEV_PROXY_TARGET="${VITE_DEV_PROXY_TARGET:-http://127.0.0.1:5081}"
export FRONTEND_ORIGIN="${FRONTEND_ORIGIN:-https://localhost:5174,http://127.0.0.1:5174,http://localhost:5174}"
export VITE_HMR_HOST="${VITE_HMR_HOST:-localhost}"
export VITE_HMR_CLIENT_PORT="${VITE_HMR_CLIENT_PORT:-5174}"

exec "${SCRIPT_DIR}/dev-uat-start.sh"
