# Single-container home-lab artifact (issue 0018, realizes ADR-0001): one image that builds the
# Vite SPA into the API's wwwroot/app, publishes the .NET API, and serves both /api and /app.
# Build context is the repo root (the SPA build writes into src/JournalRecall.Api/wwwroot/app,
# which dotnet publish then bundles).

# ---------------------------------------------------------------------------
# Stage 1 — build the client-only Vite SPA into the API's wwwroot/app.
# ---------------------------------------------------------------------------
FROM node:22-slim AS web
WORKDIR /web

# corepack ships with the Node image and pins pnpm from package.json's lockfile workflow.
RUN corepack enable

# Restore deps first (cached) using just the manifest + lockfile.
COPY src/JournalRecall.Api/web/package.json src/JournalRecall.Api/web/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

# Build the SPA. vite.config.ts writes the output to ../wwwroot/app, so lay the source out the same
# way relative to the web dir, then build.
COPY src/JournalRecall.Api/web/ ./
RUN pnpm build
# Output now lives at /web/../wwwroot/app  ==  /wwwroot/app (see below copy).

# ---------------------------------------------------------------------------
# Stage 2 — restore + publish the .NET API (includes the built SPA in wwwroot).
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the whole source tree (the API references the JournalRecall.AI.* projects). .dockerignore
# keeps bin/obj/node_modules out of the context.
COPY . .

# Drop in the SPA build output produced by stage 1 so dotnet publish bundles it under wwwroot/app.
COPY --from=web /wwwroot/app ./src/JournalRecall.Api/wwwroot/app

RUN dotnet restore src/JournalRecall.Api/JournalRecall.Api.csproj
RUN dotnet publish src/JournalRecall.Api/JournalRecall.Api.csproj \
    -c Release -o /app --no-restore

# ---------------------------------------------------------------------------
# Stage 3 — minimal ASP.NET runtime image serving /api and /app.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# The SQLite .db lives on a mounted volume at /data (see compose). Create it so the non-root user
# can own it.
RUN mkdir -p /data && chown -R app:app /data
VOLUME ["/data"]

# Kestrel listens on 8080 inside the container (mapped to the host in compose).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER app
ENTRYPOINT ["dotnet", "JournalRecall.Api.dll"]
