import { type FormEvent, type ReactNode, useState } from 'react'
import { Input, Label, TextField } from 'react-aria-components'
import { Button } from '@/shared/ui/button'

interface AuthFormProps {
  title: string
  submitLabel: string
  pending: boolean
  error?: string | null
  onSubmit: (credentials: { email: string; password: string }) => void
  footer: ReactNode
  // When true, render a second password field and require it to match before submitting.
  confirmPassword?: boolean
}

export function AuthForm({ title, submitLabel, pending, error, onSubmit, footer, confirmPassword = false }: AuthFormProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')

  const mismatch = confirmPassword && confirm.length > 0 && confirm !== password

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (confirmPassword && password !== confirm) return
    onSubmit({ email, password })
  }

  return (
    <section className="mx-auto max-w-sm space-y-6">
      <h1 className="text-2xl font-semibold text-content">{title}</h1>
      <form onSubmit={handleSubmit} className="space-y-4">
        <TextField className="flex flex-col gap-1" value={email} onChange={setEmail} type="email" isRequired autoFocus>
          <Label className="text-sm text-muted">Email</Label>
          <Input className="h-10 rounded-lg border border-border bg-surface-2 px-3 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent" />
        </TextField>
        <TextField className="flex flex-col gap-1" value={password} onChange={setPassword} type="password" isRequired>
          <Label className="text-sm text-muted">Password</Label>
          <Input className="h-10 rounded-lg border border-border bg-surface-2 px-3 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent" />
        </TextField>
        {confirmPassword ? (
          <TextField
            className="flex flex-col gap-1"
            value={confirm}
            onChange={setConfirm}
            type="password"
            isRequired
            isInvalid={mismatch}
          >
            <Label className="text-sm text-muted">Confirm password</Label>
            <Input className="h-10 rounded-lg border border-border bg-surface-2 px-3 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent" />
            {mismatch ? <p className="text-sm text-red-400">Passwords don’t match.</p> : null}
          </TextField>
        ) : null}
        {error ? <p className="text-sm text-red-400">{error}</p> : null}
        <Button type="submit" variant="primary" isDisabled={pending || mismatch} className="w-full">
          {pending ? 'Working…' : submitLabel}
        </Button>
      </form>
      <p className="text-sm text-muted">{footer}</p>
    </section>
  )
}
