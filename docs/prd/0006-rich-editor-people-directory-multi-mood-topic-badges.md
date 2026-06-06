# PRD 0006 — Notion-style editor, Person directory + @-mentions, multiple Moods, Topic badges

**Status:** ready-for-agent · **Type:** AFK · **Delivery:** five vertical slices (tracer bullets),
dependency-ordered **A → B → C**, with **D** and **E** independent · **Introduces:** ADR-0009
(rich **Content** model — ProseMirror/tiptap JSON canonical + mention-projected **Person** metadata;
**Person** becomes a first-class per-**User** aggregate) · **Touches:**
[ADR-0003](../adr/0003-append-only-content-revisions.md) (Revisions now snapshot JSON),
[ADR-0004](../adr/0004-port-full-ai-agent-framework.md) (Cleanup output contract),
[ADR-0006](../adr/0006-three-layer-test-suite-user-isolated-sqlite.md) (tests).

> Domain language per [`CONTEXT.md`](../../CONTEXT.md). This PRD changes how a **Session**'s **Raw**
> and **Cleaned** **Content** is represented and edited, how **Person** and **Topic** **Metadata** is
> captured, and how **Mood** is modelled. The **Privacy invariant** (all journal data strictly
> per-**User**) and the rule that **AI never alters Raw** are preserved throughout. **No production
> data exists yet** — the database, EF migrations are dropped and a single fresh initial migration is
> generated in the new shape; there is **no backfill**.

## Problem Statement

As a journaling **User**, the current capture experience is thin in three ways:

- **Writing is plain.** Both **Raw** and **Cleaned** are bare `<textarea>`s. I can't add light
  structure (headings, lists, emphasis) when I want it, and the AI's **Cleaned** copy can't carry the
  formatting that would make it readable.
- **People and Topics are loose text.** I type **People** and **Topics** as comma-separated strings.
  There's no way to see who/what I've referenced before, names drift and duplicate, and tagging a
  **Person** is divorced from the prose where I actually mention them. AI **Suggestions** for People
  arrive as chips disconnected from the sentence that prompted them.
- **Mood is singular.** A **Session** can only carry one **Mood**, but a day rarely feels like one
  thing — I want to record that I was *content* **and** *tired*, plus a free-text shade if I need it.

## Solution

A Notion-style editing experience and richer **Metadata**, delivered as five independent vertical
slices:

- **Raw and Cleaned become rich editors** (tiptap, headless ProseMirror). I write **Raw** — usually
  plain, optionally formatted — and toggle to the AI-produced **Cleaned** copy, which now carries real
  formatting. **AI still never touches Raw.**
- **Person becomes a real per-User directory**, and I tag people by `@`-mentioning them inline in
  either editor (autocomplete shows people I already have; I can create new ones on the fly). The
  **People** **Metadata** badges are simply a read-only projection of who's mentioned in the prose.
- **AI Cleanup proposes people to tag** with a preview of every sentence it wants to tag them in. I
  approve per person; approved tags are inserted deterministically into the **Cleaned** copy. A
  per-**User** setting lets me skip the approval step and let the AI tag inline automatically.
- **Topics get a badge picker** — chips with autocomplete over Topics I've used before, plus
  add-new — while still living as **Metadata**.
- **Mood goes multiple** — a set of known moods and/or free-text custom moods on one **Session**.

## User Stories

**Slice A — Rich Content & editor**

1. As a User, I want to write my **Raw** entry in a rich editor, so that I can add headings, lists,
   and emphasis when I want without leaving the journal.
2. As a User, I want **Raw** to stay simple plain text by default, so that quick capture isn't slowed
   by formatting ceremony.
3. As a User, I want the AI's **Cleaned** copy to render with real formatting, so that the polished
   version is genuinely more readable than my **Raw** notes.
4. As a User, I want to **toggle** between the **Raw** and **Cleaned** views (one at a time), so that I
   can focus on the copy I'm reading or editing.
5. As a User, I want the **Cleaned** tab to show a "run **Cleanup** to generate" empty state before any
   **Cleanup** has run, so that I understand why it's blank.
6. As a User, I want my formatting preserved across saves and reloads, so that the editor faithfully
   restores what I wrote.
7. As a User editing the **Cleaned** copy by hand, I want my rich edits saved and snapshotted as a
   **Revision**, so that history captures the formatted copy, not a flattened one.
8. As a User browsing **Revision** history, I want past versions rendered with their formatting
   (read-only), so that a historical version looks like it did when written.
9. As a User, I want search to keep finding my entries by their words, so that adding formatting never
   hides content from the index.
10. As a User, I want **Cleanup** to keep reading my actual words (not formatting markup), so that the
    AI's quality is unaffected by the rich representation.
11. As a developer, I want the AI to emit **markdown** that the server converts to the canonical JSON,
    so that I never depend on the model producing schema-valid editor JSON.

**Slice B — Person directory + @-mentions (manual path)**

12. As a User, I want a per-**User** directory of **People**, so that the people I reference are
    durable entities rather than loose repeated strings.
13. As a User, I want to type `@` in either editor and pick a **Person**, so that tagging happens where
    I actually mention them.
14. As a User, I want `@`-autocomplete to show **People** I've referenced before, so that I reuse
    existing entries instead of creating duplicates.
15. As a User, I want to create a new **Person** inline when I `@`-mention someone new, so that I'm not
    forced out to a separate management screen.
16. As a User, I want the **People** **Metadata** badges to reflect exactly who is `@`-mentioned across
    my **Raw** and **Cleaned** copy, so that the list never drifts from the prose.
17. As a User, I want removing an `@`-mention from the prose to remove that **Person** from the
    **Metadata**, so that there's one obvious way to untag someone.
18. As a User, I want renaming a **Person** in my directory to update everywhere they're mentioned, so
    that fixing a name once fixes it for good.
19. As a developer, I want the **People** **Metadata** to be a pure projection of mention nodes
    reconciled on every save, so that there is a single source of truth for who's on a **Session**.

**Slice C — AI people-tag proposal**

20. As a User running **Cleanup**, I want the AI to propose which **People** to tag, so that tagging
    benefits from the AI's reading without it silently editing my directory.
21. As a User, I want each proposed **Person** shown with a preview of every sentence the AI would tag
    them in, so that I can judge the tag in context.
22. As a User, I want to approve or reject each proposed **Person** as a whole, so that one decision
    covers all their mentions.
23. As a User, I want the proposal to reuse an existing directory **Person** when the name matches, so
    that approving doesn't create duplicates.
24. As a User, I want to reassign a proposed tag to a different existing **Person**, or force "create
    new," so that I control which directory entry a mention binds to.
25. As a User, I want a brand-new proposed **Person** clearly badged as "new," so that I know when
    approving will add to my directory.
26. As a User, I want approved tags inserted exactly where the AI proposed them with no further AI
    rewriting of my prose, so that approval is trustworthy.
27. As a User, I want a setting to skip the approval step and let the AI tag inline automatically, so
    that I can trade review for speed once I trust it.
28. As a User, I want that setting to **default to requiring approval**, so that the AI never writes to
    my **People** directory without my say-so until I opt out.
29. As a developer, I want **People** removed from the shared **Suggestion** machinery into their own
    proposal flow, so that the inline-prose model isn't shoehorned into the chip model that still suits
    **Topics** and **Moods**.

**Slice D — Multiple Moods**

30. As a User, I want to record more than one **Mood** on a **Session**, so that I can capture a
    mixed day.
31. As a User, I want to mix known **Moods** and free-text custom **Moods** on the same **Session**, so
    that I'm not limited to the predefined set.
32. As a User, I want to add more than one custom **Mood**, so that multiple shades can coexist.
33. As a User, I want a multi-select chip UI for **Moods**, so that picking several is quick.
34. As a User, I want the AI to suggest **Moods** I haven't already recorded (even if I have one), so
    that **Suggestions** stay useful after I've set a first **Mood**.
35. As a User, I want to accept or reject suggested **Moods** as chips, so that the **Mood** review
    matches how **Topic** **Suggestions** already work.
36. As a User, I want to filter the timeline by any of a **Session**'s **Moods**, so that multi-mood
    entries still surface under each.

**Slice E — Topic badges**

37. As a User, I want **Topics** shown and edited as badges, so that tagging feels like a tag picker
    rather than a comma-separated text box.
38. As a User, I want autocomplete over **Topics** I've used before, so that I reuse existing tags
    instead of inventing near-duplicates.
39. As a User, I want to add a brand-new **Topic** from the picker, so that I'm never blocked from
    coining a new life-area tag.
40. As a User, I want AI **Topic** **Suggestions** to keep arriving as accept/reject chips, so that the
    suggestion flow I know is unchanged.
41. As a developer, I want a `distinct Topic names` query backed by an index, so that autocomplete stays
    fast as my history grows.

## Implementation Decisions

**Cross-cutting**

- **Canonical Content representation is ProseMirror/tiptap JSON** for both **Raw** and **Cleaned**,
  stored in the existing content columns (introduces **ADR-0009**). The node/mark set is kept small
  and standard (paragraph, headings, bold/italic, lists, blockquote, code, mention) so JSON→HTML/text
  is deterministic. ProseMirror's document model is an open, documented format — coupling is to *our*
  schema, not a vendor.
- **A derived plaintext projection** is produced on every save and used for the search index and as the
  AI **Cleanup** input. Nothing correctness-critical parses the JSON; if tiptap were removed, the data
  remains portable JSON + readable text.
- **No production data** — drop the database and EF migrations, generate one fresh initial migration in
  the new shape. No backfill logic.
- **Conversion lives on the server.** The AI emits markdown; the server converts to canonical JSON via
  `MarkdownToProseMirror` (likely Markdig for the parse) and stores JSON. A User's own edits are pure
  JSON round-tripped through tiptap with no conversion.

**Slice A — Rich Content & editor**
- **Deep modules:** `MarkdownToProseMirror` (markdown → tiptap JSON, small node set, pure);
  `ProseMirrorToPlainText` (JSON → plaintext for search + AI input, pure).
- **Cleanup output contract** becomes a structured object: `{ cleanedMarkdown, topicSuggestions[],
  moodSuggestions[], peopleProposal[] }`. Prose as markdown; everything else as structured
  side-channels. (`peopleProposal` is consumed in Slice C; earlier slices can ignore it.)
- **Revisions** snapshot the canonical JSON (ADR-0003 still append-only). The **Stale**/timestamp
  derivation is unchanged. The drill-down renders JSON read-only instead of `<pre>` text.
- **Editor UI:** tiptap replaces both textareas; Raw/Cleaned switch from side-by-side to a **toggle**
  (one view at a time) with a "run Cleanup to generate" empty state for **Cleaned**. The editor keeps
  the keyed-remount pattern (FE-013/FE-015): keyed on **Session** identity / latest **Cleaned**
  **Revision**, seeded from server JSON, debounce-saving JSON. tiptap is uncontrolled — server state is
  never fed back into a live editor (avoids the cursor-reset class of bug, ref. tiptap discussion 4945).
- **Search** indexes the derived plaintext of **current** state only (unchanged behaviour).

**Slice B — Person directory + @-mentions**
- **`Person` aggregate** (new EF entity, per-**User**): `Id`, `UserId`, `Label`, with room for aliases
  later. Independently queryable (unlike today's owned `SessionPerson` string collection).
- **`SessionPerson` changes role** from owning a name string to holding a `PersonId` reference
  (provenance dropped for People, see Slice C).
- **Deep modules:** `PersonResolver` (detected name → existing `PersonId` via deterministic
  exact/alias match, else "new"; repo-backed) and `MentionProjection` (tiptap doc → set of
  `PersonId`s; reconciles a **Session**'s `SessionPerson` refs on save, **unioned across Raw +
  Cleaned**, pure).
- **People Metadata is a pure projection** of mention nodes — never edited directly. Mention nodes
  carry `{ personId, label }`; `label` is a display snapshot, `personId` is the durable link, so a
  directory rename propagates.
- **Endpoints:** `GET /people` (per-**User** directory, powers autocomplete + resolution),
  `POST /people` (create), `PATCH /people/{id}` (rename).
- **Indices:** `Person(UserId, Label)`; unique `SessionPerson(SessionId, PersonId)`;
  `SessionPerson(PersonId)` for reverse lookup.

**Slice C — AI people-tag proposal**
- **People leave the shared `MetadataSuggestion` machinery** (`SuggestionKind.Person` is removed) into
  a dedicated **people-tag proposal**: per-candidate `{ label, resolution hint, context spans }`. The
  AI is given the User's **Person** directory as context to favour reuse.
- **Resolution:** deterministic exact/alias matches auto-link to an existing **Person**; non-matches
  are proposed as "new"; fuzzy/low-confidence targets are AI-proposed but **must be confirmed by the
  User** — the AI never silently binds a fuzzy match. Every target is overridable in the review card.
- **Approval is per-Person**, with all of that person's context spans shown beneath. Instance-level
  exclusion is handled afterward by editing the prose (mentions are live nodes).
- **Deep module:** `MentionInsertion` — given the doc + approved spans, wraps those spans in mention
  nodes (pure ProseMirror transform). **No second AI pass** over the prose post-approval.
- **`UserSettings.RequirePeopleTagApproval`** (default **true**). When false, resolved mentions are
  inserted inline automatically at **Cleanup** time. New **People** are upserted to the directory only
  on approval (or immediately, when approval is off).

**Slice D — Multiple Moods**
- **`Mood` value object retained**, gaining single-string resolution: a string matching a known
  `MoodType` name resolves to that known **Mood**, otherwise to a custom **Mood**. The literal
  `"Custom"` sentinel is never persisted.
- **Session holds a `string[]` of Moods** (value-converted / JSON column), e.g.
  `["Content", "Tired", "bittersweet"]`, deduped (known by key, custom by text, case-insensitive).
  Multiple customs allowed. No primary, no ordering. No provenance on Moods.
- **AI Mood Suggestions** drop the "only if none set" guard — the AI suggests any **Mood** not already
  present, deduped against the existing set, flowing through the existing `MetadataSuggestion` chip
  flow.
- **UI:** multi-select chips (known Moods + add-custom). **Filtering** matches a **Session** if any of
  its Moods match.

**Slice E — Topic badges**
- **No Topic directory entity** — Topics stay owned `SessionTopic` strings with provenance (they're a
  separate badge surface, never inline, never id-referenced).
- **`GET /topics`** returns the **distinct Topic names** across the User's **Sessions**, powering
  badge autocomplete; adding an unknown Topic just creates a new `SessionTopic`. AI **Topic**
  **Suggestions** unchanged.
- **Indices:** confirm `Session(UserId)`; add `SessionTopic(SessionId, Name)` so the join + distinct
  runs off the index. Denormalising `UserId` onto `SessionTopic` is explicitly deferred unless
  profiling demands it.

## Testing Decisions

Good tests assert **external behaviour**, not implementation detail: a converter is tested by
input→output, a projection by doc→resulting Metadata, an endpoint by request→response + persisted
state. Prior art: domain value-object/aggregate tests in `UnitTests`, endpoint/contract tests in
`IntegrationTests`, and end-to-end UI flows in `FunctionalTests` (ADR-0006; the `SharedTestHelpers`
builders/fakers).

- **Unit (pure deep modules):** `MarkdownToProseMirror`, `ProseMirrorToPlainText`, `PersonResolver`,
  `MentionProjection`, `MentionInsertion`, and the `Mood` value object (single-string resolution +
  dedupe). These are the correctness-critical core and cheap to cover exhaustively (edge cases:
  nested lists, mixed known/custom moods, exact-vs-alias-vs-new person resolution, union of Raw+Cleaned
  mentions, span insertion offsets).
- **Integration:** the new/changed endpoints (`GET/POST/PATCH /people`, `GET /topics`), the **Cleanup**
  output contract, the people-tag-proposal approve flow (incl. the `RequirePeopleTagApproval` on/off
  branches), and the Mood `string[]` persistence + Suggestion dedupe.
- **Functional:** one flow per UI path — rich-editor write + toggle, `@`-mention create/pick +
  projected badges, proposal review/approve, multi-mood pick, Topic badge pick/add. tiptap wiring is
  covered here, **not** with unit tests.

Each vertical slice carries its own unit + integration + functional coverage (it ships tested, not
"tests later").

## Out of Scope

- **No data backfill / migration of existing content** — the DB is dropped and reseeded fresh.
- **Per-instance** (vs per-Person) approval of people tags.
- **Person merge/dedupe tooling and aliases** beyond leaving schema room — aliases are a later add.
- **Person-as-a-filter** in timeline search (filter Sessions by `PersonId`) — the reverse index is
  added now, the filter UI is later.
- **Custom-Mood autocomplete** across Sessions (no distinct-custom-Mood query/index).
- **A second AI pass** that rewrites prose to resolve pronouns into mentions ("had lunch with him" →
  "@Sam") — only verbatim-name tagging is in scope.
- **Display tinting** of AI-inserted vs User-typed mentions (an optional mention-node `origin` attr is
  a later nicety).
- **Nightly/batch Cleanup** and any scheduler changes.

## Further Notes

- **Why People are inline-projected but Topics/Moods are chip surfaces:** a **Person** is mentioned
  *in the prose*, so a mention needs a stable id to point at and the Metadata is naturally derived from
  the text. **Topics** and **Moods** are picked from a side panel, never written into prose, so they
  stay on the directly-edited `MetadataSuggestion` chip model. This asymmetry is intentional.
- **Lock-in posture:** keeping the node schema small and always being able to project to plaintext/HTML
  is the deliberate hedge — the canonical JSON is an open ProseMirror document, and nothing critical
  depends on parsing it.
- **Delivery order:** ship **A** first (keystone — everything rich hangs off it), then **B**, then
  **C**. **D** (Moods) and **E** (Topics) are independent and can be picked up at any time.
