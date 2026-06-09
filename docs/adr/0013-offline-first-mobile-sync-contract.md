# Offline-first mobile sync: client IDs, LWW-with-Revisions, and a delta cursor

## Status

proposed — drives the iOS app (journal-recall-ios) and the server changes it needs

## Context & decision

The iOS app is **offline-first**: the local store is primary, the user can always open the app and
journal (the core promise of the product is "open it and start a Session"), and the phone converges
with the server when reachable. The web app remains a thin online client. This demands a sync
contract the server does not have today. We decided:

1. **Client-generated Session IDs.** A Session created offline gets its GUID on the device; the
   create call becomes idempotent (replaying it after a dropped response is a no-op). The server
   accepts the client's ID instead of minting one.
2. **Conflicts resolve last-write-wins, with every contender preserved as a Revision.** When an
   offline Raw edit lands on a Session whose Raw changed on the server in the meantime, the edit
   with the later actual save time becomes the current Draft; the loser still appends to the
   Revision stream. No merge UI, no blocked-sync state, no data loss — worst case the user opens
   Revisions and copies a paragraph back. Draft saves therefore carry the **base revision number**
   and the **client save timestamp**. The same LWW rule covers all user-owned full-replace writes
   (metadata, Corrections, People, settings) and queued Suggestion accepts/rejects — the
   "offline accept resurrects a web-side reject" edge is accepted as harmless noise.
3. **Sync scope is split by ownership.** Two-way sync for user-owned data (Raw drafts, Session
   metadata, Corrections, People, settings). Read-only cache for server-generated artifacts
   (Cleaned, Synopsis, Revision history, Summaries, Suggestion/People-proposal lists). Queued
   upload for OnDevice Cleanup results and offline suggestion responses. Online-only for
   Server-engine Cleanup, Summary generation, and auth/admin.
4. **Changes are pulled through a delta endpoint with a cursor** (`GET /sync/changes?since=…`)
   returning entities touched since the cursor plus the next cursor, backed by an `UpdatedAt`
   column. Chosen over full re-pull (degrades with journal size, N+1 detail fetches) and over
   SSE/WebSocket push (connection machinery overkill for a single-author journal).

## Considered options

- **Online-required mobile (like the web client).** Rejected: journaling happens where the
  self-hosted server isn't reachable; "open and write" cannot depend on the network.
- **Offline capture only (local draft + outbound queue, no two-way sync).** The cheaper middle
  ground; rejected in favor of the full model — browsing and editing the journal offline is wanted,
  not just capture.
- **Conflict-surfacing merge UI.** Safest semantically; rejected for v1 as it adds a blocked state
  and a merge surface that the append-only Revision streams make unnecessary.

## Consequences

- The append-only Revision model is what makes LWW safe; any future weakening of "Revisions are
  immutable and never pruned" would silently turn LWW into data loss.
- The server gains: client-supplied IDs on Session create, base-revision + client-timestamp on
  draft saves, an `UpdatedAt`/cursor change feed, and (per the Engine decision) an endpoint that
  records a client-run Cleanup result.
- Clock skew between devices shifts which contender "wins" the Draft slot but never loses content;
  we accept wall-clock ordering rather than introducing vector clocks.
