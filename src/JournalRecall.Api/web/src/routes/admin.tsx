import { useState } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { useForm } from '@tanstack/react-form'
import { z } from 'zod'
import { useIsAdmin, useMe } from '@/features/auth/useAuth'
import {
  useAdminUsers,
  useAiProvider,
  useCreateUser,
  useRegistrationSettings,
  useResetUserPassword,
  useSetUserDisabled,
  useSetUserRole,
  useUpdateAiProvider,
  useUpdateRegistration,
} from '@/features/admin/useAdmin'
import { PROVIDERS, ROLES, type AdminUser, type AiProvider } from '@/features/admin/api'
import { Button } from '@/shared/ui/button'
import {
  applyServerErrors,
  usernameSchema,
  FormShell,
  passwordSchema,
  SelectField,
  TextField,
} from '@/shared/forms'

export const Route = createFileRoute('/admin')({
  component: Admin,
})

function Admin() {
  const { isLoading } = useMe()
  const isAdmin = useIsAdmin()

  if (isLoading) return <p className="text-muted">Loading…</p>
  if (!isAdmin) {
    return <p className="text-muted">You don’t have access to this page.</p>
  }

  return (
    <section className="space-y-10">
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold text-content">Admin</h1>
        <p className="text-muted">
          Manage users and the app-wide AI provider. This surface never shows anyone’s journal — that
          stays private to each user.
        </p>
      </div>
      <Users />
      <RegistrationSettingsForm />
      <AiProviderForm />
    </section>
  )
}

function RegistrationSettingsForm() {
  const { data: settings } = useRegistrationSettings()
  const update = useUpdateRegistration()
  const enabled = settings?.selfRegistrationEnabled ?? false

  return (
    <div className="space-y-2">
      <h2 className="text-lg font-medium text-content">Registration</h2>
      <p className="text-sm text-muted">
        When open, anyone can create their own account (as a Member). When closed, only an Admin can add
        users. Closed by default.
      </p>
      <label className="flex items-center gap-2">
        <input
          type="checkbox"
          checked={enabled}
          disabled={!settings || update.isPending}
          onChange={(e) => update.mutate({ selfRegistrationEnabled: e.target.checked })}
        />
        <span className="text-content">Allow self-registration</span>
        {update.isError ? <span className="text-sm text-red-400">{update.error.message}</span> : null}
      </label>
    </div>
  )
}

function Users() {
  const { data: users } = useAdminUsers()
  const setRole = useSetUserRole()
  const setDisabled = useSetUserDisabled()

  return (
    <div className="space-y-4">
      <h2 className="text-lg font-medium text-content">Users</h2>
      <CreateUserForm />
      {!users ? (
        <p className="text-muted">Loading users…</p>
      ) : (
        <ul className="divide-y divide-border rounded-lg border border-border">
          {users.map((u) => (
            <li key={u.id} className="flex flex-wrap items-center gap-3 p-3">
              <span className="text-content">{u.username}</span>
              {u.isDisabled ? (
                <span className="rounded-full bg-amber-500/15 px-2 py-0.5 text-xs text-amber-400">disabled</span>
              ) : null}
              <div className="ml-auto flex items-center gap-2">
                <select
                  value={roleOf(u)}
                  onChange={(e) => setRole.mutate({ id: u.id, role: e.target.value })}
                  className="rounded-lg border border-border bg-surface-2 px-2 py-1 text-sm text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
                >
                  {ROLES.map((r) => (
                    <option key={r} value={r}>
                      {r}
                    </option>
                  ))}
                </select>
                <ResetPasswordControl userId={u.id} />
                <Button onPress={() => setDisabled.mutate({ id: u.id, disabled: !u.isDisabled })}>
                  {u.isDisabled ? 'Enable' : 'Disable'}
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function roleOf(u: AdminUser): string {
  return u.roles.includes('Admin') ? 'Admin' : 'Member'
}

function ResetPasswordControl({ userId }: { userId: string }) {
  const reset = useResetUserPassword()
  const [open, setOpen] = useState(false)
  const [password, setPassword] = useState('')

  if (!open) {
    return <Button onPress={() => setOpen(true)}>Reset password</Button>
  }

  return (
    <div className="flex items-center gap-2">
      <input
        type="text"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        placeholder="Temporary password"
        className="rounded-lg border border-border bg-surface-2 px-2 py-1 text-sm text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
      />
      <Button
        variant="primary"
        isDisabled={reset.isPending || password.length === 0}
        onPress={() =>
          reset.mutate(
            { id: userId, password },
            {
              onSuccess: () => {
                setPassword('')
                setOpen(false)
              },
            },
          )
        }
      >
        {reset.isPending ? 'Saving…' : 'Save'}
      </Button>
      <Button onPress={() => { setOpen(false); setPassword('') }}>Cancel</Button>
    </div>
  )
}

const createUserSchema = z.object({
  username: usernameSchema,
  password: passwordSchema,
  role: z.enum(['Member', 'Admin']),
})

function CreateUserForm() {
  const create = useCreateUser()
  const form = useForm({
    defaultValues: { username: '', password: '', role: 'Member' as 'Member' | 'Admin' },
    validators: { onBlur: createUserSchema },
    onSubmit: async ({ value }) => {
      try {
        await create.mutateAsync({ username: value.username.trim(), password: value.password, role: value.role })
        form.reset()
      } catch (e) {
        applyServerErrors(form, e)
      }
    },
  })

  return (
    <FormShell
      form={form}
      submitLabel="Create user"
      pendingLabel="Creating…"
      className="space-y-4 rounded-lg border border-dashed border-border bg-surface-2 p-3"
    >
      <form.Field name="username">
        {(field) => <TextField field={field} label="Username" autoComplete="username" placeholder="new.user" />}
      </form.Field>
      <form.Field name="password">
        {(field) => (
          <TextField
            field={field}
            label="Temporary password"
            type="text"
            placeholder="they must change it on first sign-in"
          />
        )}
      </form.Field>
      <form.Field name="role">{(field) => <SelectField field={field} label="Role" options={ROLES} />}</form.Field>
    </FormShell>
  )
}

const aiProviderSchema = z.object({
  provider: z.string(),
  endpoint: z.string(),
  model: z.string().trim().min(1, 'Model is required.'),
  apiKey: z.string(),
})

function AiProviderForm() {
  const { data: provider } = useAiProvider()

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-lg font-medium text-content">AI provider</h2>
        <p className="text-sm text-muted">
          A BYO OpenAI-compatible (or Azure OpenAI) endpoint + model that Cleanup and Summaries use. Takes
          effect on the next run.
        </p>
      </div>
      {!provider ? (
        <p className="text-muted">Loading…</p>
      ) : (
        <AiProviderFormInner provider={provider} />
      )}
    </div>
  )
}

function AiProviderFormInner({ provider }: { provider: AiProvider }) {
  const update = useUpdateAiProvider()
  const form = useForm({
    defaultValues: {
      provider: provider.provider,
      endpoint: provider.endpoint ?? '',
      model: provider.model,
      apiKey: '',
    },
    validators: { onBlur: aiProviderSchema },
    onSubmit: async ({ value }) => {
      try {
        await update.mutateAsync({
          provider: value.provider,
          endpoint: value.endpoint.trim() || null,
          apiKey: value.apiKey.trim() || null,
          model: value.model.trim(),
        })
      } catch (e) {
        applyServerErrors(form, e)
      }
    },
  })

  return (
    <FormShell
      form={form}
      submitLabel="Save provider"
      pendingLabel="Saving…"
      className="grid max-w-xl gap-3"
      footer={update.isSuccess ? 'Saved' : undefined}
    >
      <form.Field name="provider">{(field) => <SelectField field={field} label="Provider" options={PROVIDERS} />}</form.Field>
      <form.Field name="endpoint">
        {(field) => <TextField field={field} label="Endpoint" placeholder="http://localhost:11434/v1" />}
      </form.Field>
      <form.Field name="model">
        {(field) => <TextField field={field} label="Model" placeholder="llama3.1" />}
      </form.Field>
      <form.Field name="apiKey">
        {(field) => (
          <TextField
            field={field}
            label={provider.hasApiKey ? 'API key (leave blank to keep current)' : 'API key'}
            type="password"
            placeholder={provider.hasApiKey ? '•••••••• (set)' : 'optional for local providers'}
          />
        )}
      </form.Field>
    </FormShell>
  )
}
