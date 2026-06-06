// Flow — multiple Moods (RICH-010): the Metadata editor's Mood control is a multi-select chip surface.
// Toggle two known Moods, add a free-text custom Mood, save, RELOAD and confirm all three round-trip, then
// confirm they surface as chips on the timeline.
//
// PRECONDITION: FRESH DB. Run `e2e/reset-db.sh` (app stopped) then restart the app first.
// Run with:  e2e/run.sh e2e/flows/multi-mood.js <port>
await runFlow('multi-mood', async (page) => {
  await completeSetup(page)

  const marker = `mood-${runId()}`
  const id = await startSession(page, marker)
  console.log('started session', id)

  // Toggle two known Moods (chips are aria-pressed toggle buttons).
  await page.getByRole('button', { name: 'Joyful' }).click()
  await page.getByRole('button', { name: 'Tired' }).click()

  // Add a free-text custom Mood.
  await page.getByPlaceholder('Add a custom mood').fill('bittersweet')
  await page.getByRole('button', { name: 'add' }).click()
  await expectText(page.getByText('bittersweet'), 'bittersweet', { message: 'custom mood chip added' })

  await page.getByRole('button', { name: 'Save metadata' }).click()
  await page.getByText('Saved', { exact: true }).first().waitFor({ state: 'visible' })
  console.log('saved three moods')

  // RELOAD: the editor re-seeds from the server, so the selections must come back.
  await gotoApp(page, `/sessions/${id}`)
  await page.getByRole('button', { name: 'Joyful', pressed: true }).waitFor({ state: 'visible' })
  await page.getByRole('button', { name: 'Tired', pressed: true }).waitFor({ state: 'visible' })
  await expectText(page.getByText('bittersweet'), 'bittersweet', { message: 'custom mood survives reload' })
  console.log('all three moods round-tripped across reload')

  // The moods surface as chips on the timeline row.
  await gotoApp(page, '/')
  const row = page.getByRole('link', { name: new RegExp(marker) }).first()
  await row.waitFor({ state: 'visible' })
  const rowText = await row.innerText()
  for (const mood of ['Joyful', 'Tired', 'bittersweet']) {
    if (!rowText.includes(mood)) throw new Error(`timeline row missing mood chip: ${mood}`)
  }
  console.log('timeline row shows all mood chips')
})
