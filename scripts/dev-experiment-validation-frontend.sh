#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

export VITE_API_BASE_URL="http://127.0.0.1:41166"
export VITE_DEV_PROXY_TARGET="http://127.0.0.1:41166"
export VITE_DEV_SERVER_PORT="42983"
export VITE_HMR_CLIENT_PORT="42983"

cd "${repo_root}/frontend"
exec corepack pnpm exec vite --host 127.0.0.1 --port 42983 --strictPort
