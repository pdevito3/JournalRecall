// Flow — topic badges (RICH-011): Topics are edited as removable chips with autocomplete over Topics the
// User has used before (GET /topics). Add two topics on one Session, save, RELOAD and confirm they
// round-trip, then on a second Session confirm the autocomplete datalist offers the earlier topics.
//
// PRECONDITION: FRESH DB. Run `e2e/reset-db.sh` (app stopped) then restart the app first.
// Run with:  e2e/run.sh e2e/flows/topic-badges.js <port>
await runFlow('topic-badges', async (page) => {
  await completeSetup(page)

  const marker = `topic-${runId()}`
  const id = await startSession(page, marker)
  console.log('started session', id)

  // Add two topics as badges via the topic input.
  const topicInput = page.getByPlaceholder('Add a topic')
  await topicInput.fill('work')
  await page.getByRole('button', { name: 'add' }).first().click()
  await topicInput.fill('travel')
  await page.getByRole('button', { name: 'add' }).first().click()
  await expectText(page.getByText('#work'), '#work', { message: 'work badge added' })
  await expectText(page.getByText('#travel'), '#travel', { message: 'travel badge added' })

  await page.getByRole('button', { name: 'Save metadata' }).click()
  await page.getByText('Saved', { exact: true }).first().waitFor({ state: 'visible' })
  console.log('saved two topic badges')

  // RELOAD: badges re-seed from the server.
  await gotoApp(page, `/sessions/${id}`)
  await expectText(page.getByText('#work'), '#work', { message: 'work badge survives reload' })
  await expectText(page.getByText('#travel'), '#travel', { message: 'travel badge survives reload' })
  console.log('topic badges round-tripped across reload')

  // A second Session: the autocomplete datalist now offers the earlier topics (GET /topics).
  await gotoApp(page, '/')
  await startSession(page, `${marker}-2`)
  await waitFor(
    async () => (await page.locator('#topic-suggestions option[value="work"]').count()) > 0,
    { message: 'autocomplete offers a previously-used topic' },
  )
  console.log('autocomplete offers previously-used topics')
})
