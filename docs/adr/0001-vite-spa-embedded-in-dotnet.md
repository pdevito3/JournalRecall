# Frontend is a client-only Vite SPA embedded in the .NET app

## Status

accepted

## Context & decision

The template (PlateWise) builds its frontend with **TanStack Start**, a full-stack React
framework that runs its own Node/Nitro server and serves its own server-side `api/` routes. That
cannot be "built into the .NET server" — it *is* a server. We instead build a **client-only Vite
SPA** (keeping TanStack Router in client mode, TanStack Query, React Aria, and Tailwind from the
template) that Vite compiles into the API project's `wwwroot/app`. ASP.NET serves it at **`/app/*`**
with an SPA fallback to `/app/index.html`; the API lives at **`/api`**. In development the Vite
dev server proxies `/api` → the .NET app, so the browser always talks to a **single origin**.

## Considered options

- **TanStack Start as a separate container (BFF-style)** — closest to the template, but
  reintroduces the extra origin / Node server we explicitly want to avoid.
- **Vite SPA hosted separately (own container/CDN)** — needs CORS and cross-origin cookie handling.

## Consequences

- **No CORS, no BFF, and the HttpOnly auth cookie stays same-origin** (including in dev) — the core
  reason for the architecture (see ADR-0002).
- One build artifact, one container — ideal for the home-lab deployment target.
- We **give up SSR**. For a private app behind auth this costs nothing (no SEO, app-style first
  paint is fine), which is what makes the trade worthwhile.
