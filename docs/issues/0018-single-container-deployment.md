# 0018 — Single-container deployment

**Phase:** 8 · **Type:** AFK · **Status:** done · **Realizes:** ADR-0001

## What to build

The home-lab artifact: one container that serves `/api` + `/app` and owns the SQLite database file
on a mounted volume, with a `docker compose` for deployment. Data survives restarts and upgrades.

- Dockerfile that builds the Vite SPA, publishes the .NET app, and serves both from one image.
- SQLite `.db` on a **mounted volume**; migrations applied on startup.
- `docker compose` for the home-lab deployment.

## Acceptance criteria

- [ ] `docker compose up` builds and runs the app; `/app` and `/api/health` are reachable from the
      container.
- [ ] Creating a Session, then restarting the container, retains the Session (volume persistence).
- [ ] The image runs as a single container with no external database service.
- [ ] A documented volume path holds the `.db` file.

## Blocked by

- #0001
