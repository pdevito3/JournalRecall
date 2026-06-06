// Reference flow — rich editor (RICH-003): the Raw editor is a tiptap surface over canonical ProseMirror
// JSON. Type markdown shorthands so StarterKit's input rules produce real formatting, autosave, RELOAD,
// and confirm the heading/list/bold round-trip from the server JSON. Then exercise the Raw/Cleaned toggle:
// Cleaned shows its "run Cleanup to generate" empty state before any Cleanup has run.
//
// PRECONDITION: FRESH DB. Run `e2e/reset-db.sh` (app stopped) then restart the app first.
// Run with:  e2e/run.sh e2e/flows/rich-editor.js <port>
await runFlow('rich-editor', async (page) => {
  await completeSetup(page)

  const marker = `rich-${runId()}`
  // `# ` → H1, `- ` → bullet item, `**bold**` → bold. Newlines start new blocks.
  const id = await startSession(page, `# ${marker}\nfollowed by a paragraph with a **bold** word\n- a bullet item`)
  console.log('typed rich content + autosaved', id)

  // The formatting is real DOM in the live editor (not literal markdown characters).
  await expectText(page.locator('.rich-editor h1'), marker, { message: 'heading rendered as <h1>' })
  await page.locator('.rich-editor li').first().waitFor({ state: 'visible' }) // bullet became a list item
  await page.locator('.rich-editor strong').first().waitFor({ state: 'visible' }) // bold became <strong>
  console.log('formatting rendered as real elements')

  // RELOAD: the editor re-seeds from the server JSON, so the formatting must come back (not lost to text).
  await gotoApp(page, `/sessions/${id}`)
  await expectText(page.locator('.rich-editor h1'), marker, { message: 'heading survives reload as <h1>' })
  await page.locator('.rich-editor strong').first().waitFor({ state: 'visible' })
  // The marker must never appear as a literal "# " — that would mean JSON/markdown leaked as plain text.
  const raw = await rawEditor(page).innerText()
  if (raw.includes(`# ${marker}`)) throw new Error('heading round-tripped as literal markdown, not formatting')
  console.log('formatting round-tripped across reload')

  // Toggle to Cleaned: no Cleanup has run, so the empty state invites one.
  await page.getByRole('tab', { name: /Cleaned/ }).click()
  await expectText(page.getByText('No cleaned copy yet'), 'No cleaned copy yet', { message: 'Cleaned empty state' })
  console.log('Cleaned tab shows the run-Cleanup empty state')

  // Toggle back to Raw: the heading is still there (state preserved across the toggle).
  await page.getByRole('tab', { name: /Raw/ }).click()
  await expectText(page.locator('.rich-editor h1'), marker, { message: 'Raw heading intact after toggle' })
  console.log('toggled back to Raw with content intact')
})
