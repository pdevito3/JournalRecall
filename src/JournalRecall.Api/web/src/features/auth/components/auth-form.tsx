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
}

export function AuthForm({ title, submitLabel, pending, error, onSubmit, footer }: AuthFormProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
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
        {error ? <p className="text-sm text-red-400">{error}</p> : null}
        <Button type="submit" variant="primary" isDisabled={pending} className="w-full">
          {pending ? 'Working…' : submitLabel}
        </Button>
      </form>
      <p className="text-sm text-muted">{footer}</p>
    </section>
  )
}
