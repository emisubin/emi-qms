#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export REVIEW_UAT_FRONTEND_HTTPS="true"
exec "${SCRIPT_DIR}/dev-uat-review-start.sh"
