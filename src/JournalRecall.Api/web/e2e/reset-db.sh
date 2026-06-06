#!/usr/bin/env bash
# Reset the app to a known FRESH state (FE-028 decision #3).
#
# The SQLite store `journalrecall.db` lives in the API project dir and is per-worktree (see
# docs/dev-runbook.md). Deleting it (and its -shm/-wal sidecars) makes the next boot a first-run
# instance, so the app shows the setup gate and `completeSetup` can run.
#
# STOP the app first — EF holds the file open while running. Usage:  e2e/reset-db.sh
set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_DIR="$(cd "${DIR}/.." && pwd)"   # e2e/ -> web/ -> API project dir is web's parent
DB_DIR="$(cd "${API_DIR}/.." && pwd)"

rm -f "${DB_DIR}/journalrecall.db" "${DB_DIR}/journalrecall.db-shm" "${DB_DIR}/journalrecall.db-wal"
echo "reset: removed journalrecall.db* from ${DB_DIR}"
