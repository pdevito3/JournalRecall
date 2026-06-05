import { type FormEvent, useState } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import type { Correction } from '@/features/corrections/api'
import {
  useCorrections,
  useCreateCorrection,
  useDeleteCorrection,
  useUpdateCorrection,
} from '@/features/corrections/useCorrections'
import { Button } from '@/shared/ui/button'

export const Route = createFileRoute('/corrections')({
  component: CorrectionsPage,
})

function CorrectionsPage() {
  const { data: corrections, isLoading } = useCorrections()
  const create = useCreateCorrection()

  const [canonical, setCanonical] = useState('')
  const [mishearings, setMishearings] = useState('')
  const [hardReplace, setHardReplace] = useState(false)

  function onSubmit(event: FormEvent) {
    event.preventDefault()
    if (!canonical.trim()) return
    create.mutate(
      { canonicalTerm: canonical.trim(), mishearings: splitTerms(mishearings), hardReplace },
      {
        onSuccess: () => {
          setCanonical('')
          setMishearings('')
          setHardReplace(false)
        },
      },
    )
  }

  return (
    <section className="space-y-6">
      <div className="space-y-1">
        <h1 className="text-lg font-semibold text-content">Corrections</h1>
        <p className="text-sm text-muted">
          Fix terms that get mis-dictated. During AI cleanup these are applied to the Cleaned copy
          only — your Raw text is never changed.
        </p>
      </div>

      <form onSubmit={onSubmit} className="space-y-3 rounded-lg border border-border bg-surface-2 p-4">
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="space-y-1">
            <span className="text-sm text-muted">Canonical term</span>
            <input
              value={canonical}
              onChange={(e) => setCanonical(e.target.value)}
              placeholder="Profisee"
              className="w-full rounded-lg border border-border bg-surface-3 px-3 py-2 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
            />
          </label>
          <label className="space-y-1">
            <span className="text-sm text-muted">Mishearings (comma-separated)</span>
            <input
              value={mishearings}
              onChange={(e) => setMishearings(e.target.value)}
              placeholder="prophecy, professionally"
              className="w-full rounded-lg border border-border bg-surface-3 px-3 py-2 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
            />
          </label>
        </div>
        <div className="flex items-center justify-between">
          <label className="flex items-center gap-2 text-sm text-content">
            <input type="checkbox" checked={hardReplace} onChange={(e) => setHardReplace(e.target.checked)} />
            Hard-replace (substitute every occurrence deterministically)
          </label>
          <Button type="submit" variant="primary" isDisabled={create.isPending || !canonical.trim()}>
            Add correction
          </Button>
        </div>
      </form>

      {isLoading ? (
        <p className="text-muted">Loading…</p>
      ) : corrections && corrections.length > 0 ? (
        <ul className="space-y-2">
          {corrections.map((c) => (
            <CorrectionRow key={c.id} correction={c} />
          ))}
        </ul>
      ) : (
        <p className="text-sm text-muted">No corrections yet.</p>
      )}
    </section>
  )
}

function CorrectionRow({ correction }: { correction: Correction }) {
  const update = useUpdateCorrection()
  const remove = useDeleteCorrection()

  return (
    <li className="flex items-center justify-between gap-3 rounded-lg border border-border bg-surface-2 p-3">
      <div className="min-w-0">
        <div className="flex items-center gap-2">
          <span className="font-medium text-content">{correction.canonicalTerm}</span>
          {correction.hardReplace ? (
            <span className="rounded-full bg-accent/15 px-2 py-0.5 text-xs text-accent">hard-replace</span>
          ) : (
            <span className="rounded-full bg-surface-3 px-2 py-0.5 text-xs text-muted">hint</span>
          )}
        </div>
        <p className="truncate text-sm text-muted">
          {correction.mishearings.length > 0 ? correction.mishearings.join(', ') : 'no mishearings'}
        </p>
      </div>
      <div className="flex shrink-0 items-center gap-1">
        <Button
          onPress={() =>
            update.mutate({
              id: correction.id,
              body: {
                canonicalTerm: correction.canonicalTerm,
                mishearings: correction.mishearings,
                hardReplace: !correction.hardReplace,
              },
            })
          }
        >
          {correction.hardReplace ? 'Make hint' : 'Make hard'}
        </Button>
        <Button onPress={() => remove.mutate(correction.id)}>Delete</Button>
      </div>
    </li>
  )
}

function splitTerms(value: string): string[] {
  return value
    .split(',')
    .map((t) => t.trim())
    .filter((t) => t.length > 0)
}
