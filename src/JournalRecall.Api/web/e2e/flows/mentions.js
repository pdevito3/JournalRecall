// Flow — @-mention People (RICH-007): typing `@` in the Raw editor opens an autocomplete over the User's
// Person directory; a name not in the directory can be created inline. Inserting a mention projects the
// Person onto the read-only People badges (reconciled on save). Picking the same Person again dedupes;
// removing the mention untags them.
//
// PRECONDITION: FRESH DB. Run `e2e/reset-db.sh` (app stopped) then restart the app first.
// Run with:  e2e/run.sh e2e/flows/mentions.js <port>
await runFlow('mentions', async (page) => {
  await completeSetup(page)

  const marker = `mention-${runId()}`
  const id = await startSession(page, marker)
  console.log('started session', id)

  // Type `@Sam` — the directory is empty, so the popup offers to create Sam inline.
  const editor = rawEditor(page)
  await editor.click()
  await page.keyboard.press('End')
  await page.keyboard.press('Enter')
  await page.keyboard.type('@Sam', { delay: 30 })

  const createOption = page.locator('.mention-list button', { hasText: 'Create' })
  await createOption.waitFor({ state: 'visible' })
  await createOption.click()
  console.log('created + mentioned Sam inline')

  // Debounced autosave reconciles People → the read-only badge shows @Sam.
  await page.getByText('Saved', { exact: true }).first().waitFor({ state: 'visible' })
  const badges = page.locator('[data-testid="people-badges"]')
  await expectText(badges, '@Sam', { message: 'Sam projected onto the People badges' })
  console.log('Sam appears in the projected People badges')

  // Mention Sam again — this time the directory autocomplete offers the existing entry; reuse it.
  await editor.click()
  await page.keyboard.press('End')
  await page.keyboard.type(' @Sam', { delay: 30 })
  const samOption = page.locator('.mention-list button', { hasText: '@Sam' })
  await samOption.first().waitFor({ state: 'visible' })
  await samOption.first().click()
  await page.getByText('Saved', { exact: true }).first().waitFor({ state: 'visible' })
  // Two mentions, one Person — the badges dedupe to a single @Sam.
  await waitFor(
    async () => (await page.locator('[data-testid="people-badges"] >> text=@Sam').count()) === 1,
    { message: 'two mentions of Sam dedupe to one badge' },
  )
  console.log('reused the directory entry; badge deduped')

  // Remove every mention (clear the editor) → Sam is untagged. (Meta+A is select-all on macOS;
  // Ctrl+A is move-to-line-start there.)
  await editor.click()
  await page.keyboard.press('Meta+A')
  await page.keyboard.press('Backspace')
  await page.keyboard.type('nobody here now', { delay: 8 })
  await expectText(
    page.getByText('Type @ in an editor to tag someone.'),
    'Type @',
    { message: 'removing the mention untags Sam (badges empty)' },
  )
  console.log('removed the mention; Sam untagged')
})
