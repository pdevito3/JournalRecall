import { type FormEvent, useState } from 'react'
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { Input, Label, TextField } from 'react-aria-components'
import { useChangePassword, useMe } from '@/features/auth/useAuth'
import { Button } from '@/shared/ui/button'

export const Route = createFileRoute('/change-password')({
  component: ChangePasswordPage,
})

function ChangePasswordPage() {
  const navigate = useNavigate()
  const { data: user } = useMe()
  const changePassword = useChangePassword()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')

  const forced = user?.mustChangePassword ?? false

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    changePassword.mutate(
      { currentPassword, newPassword },
      { onSuccess: () => navigate({ to: '/' }) },
    )
  }

  return (
    <section className="mx-auto max-w-sm space-y-6">
      <div className="space-y-1">
        <h1 className="text-2xl font-semibold text-content">Set a new password</h1>
        {forced ? (
          <p className="text-sm text-muted">
            Your account was given a temporary password. Choose a new one to continue.
          </p>
        ) : null}
      </div>
      <form onSubmit={handleSubmit} className="space-y-4">
        <TextField
          className="flex flex-col gap-1"
          value={currentPassword}
          onChange={setCurrentPassword}
          type="password"
          isRequired
          autoFocus
        >
          <Label className="text-sm text-muted">{forced ? 'Temporary password' : 'Current password'}</Label>
          <Input className="h-10 rounded-lg border border-border bg-surface-2 px-3 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent" />
        </TextField>
        <TextField className="flex flex-col gap-1" value={newPassword} onChange={setNewPassword} type="password" isRequired>
          <Label className="text-sm text-muted">New password</Label>
          <Input className="h-10 rounded-lg border border-border bg-surface-2 px-3 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent" />
        </TextField>
        {changePassword.error ? <p className="text-sm text-red-400">{changePassword.error.message}</p> : null}
        <Button type="submit" variant="primary" isDisabled={changePassword.isPending} className="w-full">
          {changePassword.isPending ? 'Saving…' : 'Set password'}
        </Button>
      </form>
    </section>
  )
}
