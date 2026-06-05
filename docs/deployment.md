# Deployment — single container (home lab)

JournalRecall ships as **one container** that serves both the API (`/api`) and the SPA (`/app`)
and owns a file-based SQLite database on a mounted volume (realizes
[ADR-0001](adr/0001-vite-spa-embedded-in-dotnet.md)). There is **no separate database service** —
SQLite is a file inside the container's data volume.

## Artifacts

- [`Dockerfile`](../Dockerfile) — multi-stage build:
  1. **web** (`node`): builds the Vite SPA into the API's `wwwroot/app`.
  2. **build** (`dotnet/sdk:10.0`): `dotnet publish` the API, bundling the built SPA.
  3. **runtime** (`dotnet/aspnet:10.0`): minimal image; runs as the non-root `app` user and
     listens on port **8080** inside the container.
- [`compose.yaml`](../compose.yaml) — one `app` service, a named volume, the connection string and
  JWT signing key as env.

EF Core migrations are applied automatically on startup (`MigrationHostedService`), so there is no
manual migration step at deploy time.

## Run it

```bash
docker compose up -d --build      # build the image and start the container
```

The app is then reachable on the host at:

- App UI:      <http://localhost:8080/app>  (and `/` redirects there)
- API health:  <http://localhost:8080/api/health>  → `Healthy`

Stop it (data is preserved):

```bash
docker compose down               # stops the container, keeps the volume
docker compose down -v            # ALSO deletes the data volume (wipes the journal)
```

## Configuration

Set these via a `.env` file next to `compose.yaml` or via your shell environment.

| Variable | Default | Purpose |
|---|---|---|
| `JOURNALRECALL_PORT` | `8080` | Host port the app is published on. |
| `JWT__SIGNINGKEY` | a placeholder dev key | **Override in production.** The app refuses to start unless this is ≥ 32 bytes. |

The connection string is fixed by compose to point at the data volume:
`ConnectionStrings__JournalRecall=Data Source=/data/journalrecall.db`.

## Data persistence (the volume)

- **Container path:** `/data`
- **Database file:** `/data/journalrecall.db` (plus SQLite `-wal` / `-shm` sidecars)
- **Named volume:** `journalrecall_journalrecall-data` (Docker prefixes the compose project name)

Because the `.db` lives on the named volume — not in the container's writable layer — your journal
survives `docker compose restart`, `docker compose down` + `up`, and image upgrades. Only
`docker compose down -v` (or deleting the volume) destroys the data.

Inspect / back up the volume:

```bash
docker volume ls | grep journalrecall                 # find the volume
docker compose exec app ls -la /data                  # see the .db file
```
