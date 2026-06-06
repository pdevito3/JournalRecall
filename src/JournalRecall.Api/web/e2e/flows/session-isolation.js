// Reference flow — session isolation (FE-013, FE-030): two Sessions must each show their OWN Raw draft.
// FE-013 added `key={sessionId}` so the editor remounts and re-seeds from server data when the Session
// changes, instead of leaking the previous Session's text. This asserts the user-facing guarantee: open
// Session A → see A's text (not B's); open Session B → see B's text (not A's).
//
// PRECONDITION: FRESH DB. Run `e2e/reset-db.sh` (app stopped) then restart the app first.
// Run with:  e2e/run.sh e2e/flows/session-isolation.js <port>
await runFlow('session-isolation', async (page) => {
  await completeSetup(page)

  const alpha = `ALPHA-${runId()}`
  const bravo = `BRAVO-${runId()}`

  // Two distinct Sessions, each with its own unique Raw draft.
  await startSession(page, alpha)
  await gotoApp(page, '/')
  await startSession(page, bravo)
  await gotoApp(page, '/')

  const draft = page.getByPlaceholder('Write freely…')

  // Open A from the timeline → editor shows A's text, never B's.
  await openFromTimeline(page, alpha)
  await expectValue(draft, alpha, { message: 'Session A shows ALPHA' })
  if ((await draft.inputValue()).includes(bravo)) throw new Error('Session A leaked BRAVO text')
  console.log('Session A shows its own draft')

  // Back to the timeline, open B → editor shows B's text, never A's.
  await gotoApp(page, '/')
  await openFromTimeline(page, bravo)
  await expectValue(draft, bravo, { message: 'Session B shows BRAVO' })
  if ((await draft.inputValue()).includes(alpha)) throw new Error('Session B leaked ALPHA text')
  console.log('Session B shows its own draft')
})
