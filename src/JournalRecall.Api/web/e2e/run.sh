#!/usr/bin/env bash
# Runner for committed dev-browser e2e flows (FE-028).
#
# dev-browser's QuickJS sandbox has no import/require, so we compose at the shell: inject the port,
# prepend the shared helpers (e2e/helpers.js) + the waitFor shim (e2e/shim.js, if present), then the
# flow script, and pipe the whole thing to dev-browser.
#
# Usage:   e2e/run.sh <flow-file> [port]
# Example: e2e/run.sh e2e/flows/auth-smoke.js 58867
#
# Find the isolated Vite port with:  lsof -nP -iTCP -sTCP:LISTEN | grep node   (URL is /app on that port)
set -euo pipefail

FLOW="${1:?usage: e2e/run.sh <flow-file> [port]}"
PORT="${2:-4247}"
RUN_ID="r$(date +%s)"

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

{
  echo "globalThis.JR_PORT = ${PORT};"
  echo "globalThis.JR_RUN = '${RUN_ID}';"
  cat "${DIR}/shim.js" 2>/dev/null || true
  cat "${DIR}/helpers.js"
  cat "${FLOW}"
} | dev-browser --headless --timeout 90
