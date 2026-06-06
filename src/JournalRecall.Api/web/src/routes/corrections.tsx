import { createFileRoute } from '@tanstack/react-router'
import { useForm } from '@tanstack/react-form'
import { z } from 'zod'
import type { Correction } from '@/features/corrections/api'
import {
  correctionsQueryOptions,
  useCorrections,
  useCreateCorrection,
  useDeleteCorrection,
  useUpdateCorrection,
} from '@/features/corrections/useCorrections'
import { CheckboxField, FormShell, TextField, applyServerErrors } from '@/shared/forms'
import { Button } from '@/shared/ui/button'

export const Route = createFileRoute('/corrections')({
  // Prime the corrections list during navigation (kills the mount→fetch waterfall). The component
  // keeps reading via useQuery, so focus/reconnect refetch, dedup, and GC stay intact.
  loader: ({ context: { queryClient } }) => queryClient.ensureQueryData(correctionsQueryOptions()),
  component: CorrectionsPage,
})

const correctionFormSchema = z.object({
  canonicalTerm: z.string().trim().min(1, 'Enter a canonical term.'),
  mishearings: z.string(),
  hardReplace: z.boolean(),
})
type CorrectionFormValues = z.infer<typeof correctionFormSchema>

function CorrectionsPage() {
  const { data: corrections } = useCorrections()
  const create = useCreateCorrection()

  const form = useForm({
    defaultValues: { canonicalTerm: '', mishearings: '', hardReplace: false } as CorrectionFormValues,
    validators: { onBlur: correctionFormSchema },
    onSubmit: async ({ value }) => {
      try {
        await create.mutateAsync({
          canonicalTerm: value.canonicalTerm.trim(),
          mishearings: splitTerms(value.mishearings),
          hardReplace: value.hardReplace,
        })
        form.reset()
      } catch (error) {
        applyServerErrors(form, error)
      }
    },
  })

  return (
    <section className="space-y-6">
      <div className="space-y-1">
        <h1 className="text-lg font-semibold text-content">Corrections</h1>
        <p className="text-sm text-muted">
          Fix terms that get mis-dictated. During AI cleanup these are applied to the Cleaned copy
          only — your Raw text is never changed.
        </p>
      </div>

      <FormShell
        form={form}
        submitLabel="Add correction"
        pendingLabel="Adding…"
        className="space-y-3 rounded-lg border border-border bg-surface-2 p-4"
      >
        <div className="grid gap-3 sm:grid-cols-2">
          <form.Field name="canonicalTerm">
            {(field) => <TextField field={field} label="Canonical term" placeholder="Profisee" autoFocus />}
          </form.Field>
          <form.Field name="mishearings">
            {(field) => (
              <TextField field={field} label="Mishearings (comma-separated)" placeholder="prophecy, professionally" />
            )}
          </form.Field>
        </div>
        <form.Field name="hardReplace">
          {(field) => (
            <CheckboxField field={field} label="Hard-replace (substitute every occurrence deterministically)" />
          )}
        </form.Field>
      </FormShell>

      {corrections.length > 0 ? (
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
