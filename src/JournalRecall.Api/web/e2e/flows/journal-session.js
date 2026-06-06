// Reference flow — journal session (FE-030): the core write→autosave→persist journey. Setup, start a
// Session, write the Raw draft, confirm the debounced autosave reports "Saved", then RELOAD and confirm
// the draft is still there (proves the route loader re-fetches the Session on navigation, FE-007/008).
//
// PRECONDITION: FRESH DB. Run `e2e/reset-db.sh` (app stopped) then restart the app first.
// Run with:  e2e/run.sh e2e/flows/journal-session.js <port>
await runFlow('journal-session', async (page) => {
  await completeSetup(page)

  const marker = `note-${runId()}`
  const id = await startSession(page, `${marker} — my first entry`)
  console.log('started + autosaved session', id)

  // Reload from the server: the route loader awaits the Session, so the draft must come back.
  await gotoApp(page, `/sessions/${id}`)
  await expectText(rawEditor(page), marker, { message: `reloaded draft contains ${marker}` })
  console.log('draft persisted across reload')
})
