import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { z } from 'zod'
import { useForm } from '@tanstack/react-form'
import { FormShell, TextField, applyServerErrors, passwordSchema, passwordsMatch } from '@/shared/forms'
import { useChangePassword, useMe } from '@/features/auth'

export const Route = createFileRoute('/change-password')({
  component: ChangePasswordPage,
})

// New password validated by the shared policy fragment; match enforced by the shared refine against
// the new-password field. The current password just has to be present.
const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Enter your current password.'),
    newPassword: passwordSchema,
    confirmPassword: z.string(),
  })
  .superRefine(passwordsMatch('newPassword', 'confirmPassword'))

function ChangePasswordPage() {
  const navigate = useNavigate()
  const { data: user } = useMe()
  const changePassword = useChangePassword()
  // Same form serves the voluntary change and the forced-change-on-first-login flow (ADR-0024).
  const forced = user?.mustChangePassword ?? false

  const form = useForm({
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
    validators: { onBlur: changePasswordSchema },
    onSubmit: async ({ value }) => {
      try {
        await changePassword.mutateAsync({ currentPassword: value.currentPassword, newPassword: value.newPassword })
        await navigate({ to: '/' })
      } catch (error) {
        applyServerErrors(form, error)
      }
    },
  })

  return (
    <FormShell form={form} title="Set a new password" submitLabel="Set password" pendingLabel="Saving…">
      {forced ? (
        <p className="text-sm text-muted">
          Your account was given a temporary password. Choose a new one to continue.
        </p>
      ) : null}
      <form.Field name="currentPassword">
        {(field) => (
          <TextField
            field={field}
            label={forced ? 'Temporary password' : 'Current password'}
            type="password"
            autoFocus
            autoComplete="current-password"
          />
        )}
      </form.Field>
      <form.Field name="newPassword">
        {(field) => <TextField field={field} label="New password" type="password" autoComplete="new-password" />}
      </form.Field>
      <form.Field name="confirmPassword">
        {(field) => (
          <TextField field={field} label="Confirm new password" type="password" autoComplete="new-password" />
        )}
      </form.Field>
    </FormShell>
  )
}
