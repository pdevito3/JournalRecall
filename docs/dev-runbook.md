# Dev runbook: isolated-mode app + dev-browser

How to run the app on throwaway ports (so multiple worktrees / the root can run at
once) and drive its UI end-to-end with `dev-browser`.

## Aspire isolated mode

```bash
aspire run --isolated      # randomized ports + isolated user secrets → multiple instances at once
aspire run                 # standalone, fixed ports (API 5247/7247, web 4247)
```

Works the same from a worktree or the non-worktree root. Use `--isolated` whenever
another instance might already be up (another worktree, or the root) so they don't
collide on ports.

### Finding the URLs

Isolated ports are random and the CLI only prints the dashboard URL. List the
listeners:

```bash
lsof -nP -iTCP -sTCP:LISTEN | grep -iE 'node|dotnet'
```

- `node …:<port>` → the Vite SPA. **Browser URL is `http://localhost:<port>/app/`**
  (note the `/app` base path).
- `dotnet …` → the API (https) + Aspire dashboard. You rarely hit these directly.

### Why a single URL is enough

The Vite dev server proxies `/api/*` to the API and follows Aspire's injected
`services__api__http__0`, so it targets whatever random port the API was allocated
(see `vite.config.ts`). Everything is one **http** origin — no TLS/cert friction.

Health check:

```bash
curl http://localhost:<nodePort>/api/health      # expect 200
```

### Data store

`journalrecall.db` (SQLite) lives in the API project dir, so it is **per-worktree** —
each worktree has its own data; they don't share. Within one worktree, repeated runs
reuse the same file (isolated mode randomizes ports/secrets, not the DB).

### Teardown

`Ctrl+C` should stop the run. The AppHost has been observed to ignore SIGINT; if a
graceful stop leaves it running, kill the worktree's process tree by path and confirm
the ports are freed. Leave PlateWise / any pre-existing `dcp` processes alone.

```bash
pkill -f 'JournalRecall-worktrees/<name>/JournalRecall.AppHost'
pkill -f 'JournalRecall-worktrees/<name>/src/JournalRecall.Api/bin'
lsof -nP -iTCP:<ports> -sTCP:LISTEN               # expect empty
```

## dev-browser

[`dev-browser`](https://github.com/SawyerHood/dev-browser) is a sandboxed CLI (not an
MCP). One-time setup:

```bash
npm install -g dev-browser
dev-browser install        # Playwright Chromium
```

Drive it with heredoc scripts. Scripts run in a QuickJS sandbox (no Node APIs), but
`browser` is a real Playwright handle returning real `Page` objects:

```bash
dev-browser --headless --timeout 50 <<'EOF'
const page = await browser.getPage("t");                 // named pages persist across runs
await page.goto("http://localhost:<nodePort>/app/login", { waitUntil: "networkidle" });
await page.locator('input[name="email"]').fill('a@b.com');
await page.keyboard.press('Tab');                        // validation is onBlur — focus then blur
console.log(await page.locator('p[role="alert"]').allInnerTexts());   // form banner
await saveScreenshot(await page.screenshot({ fullPage: true }), "shot.png");
EOF
```

- Screenshots land in `~/.dev-browser/tmp/`.
- `dev-browser stop` kills the daemon + Chromium. `dev-browser --help` has the full API.
- To skip permission prompts in Claude Code, add `Bash(dev-browser *)` to the settings
  `allow` list.

### Form selectors for this app

The forms use react-aria + `@tanstack/react-form` (see
`docs/adr/0007-forms-on-tanstack-react-form-zod.md`):

- Inputs carry `name={field.name}` → `input[name="email"]`.
- Validation is **onBlur** — fill, then `Tab` (or blur) to trigger it.
- Field errors render in `span.text-red-400`; the form-level banner is `p[role="alert"]`.
- react-aria Selects are a trigger button + `div[role="option"]` items — scope queries
  to the form's `section` so native `<select>`s elsewhere on the page don't collide.
