import { useEffect, useState } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { useMe } from '@/features/auth/useAuth'
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
import { PROVIDERS, ROLES, type AdminUser } from '@/features/admin/api'
import { Button } from '@/shared/ui/button'

export const Route = createFileRoute('/admin')({
  component: Admin,
})

function Admin() {
  const { data: user, isLoading } = useMe()

  if (isLoading) return <p className="text-muted">Loading…</p>
  if (!user?.roles?.includes('Admin')) {
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
              <span className="text-content">{u.email}</span>
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

function CreateUserForm() {
  const create = useCreateUser()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState<string>('Member')

  function onCreate() {
    create.mutate(
      { email: email.trim(), password, role },
      {
        onSuccess: () => {
          setEmail('')
          setPassword('')
          setRole('Member')
        },
      },
    )
  }

  return (
    <div className="flex flex-wrap items-end gap-3 rounded-lg border border-dashed border-border bg-surface-2 p-3">
      <Field label="Email">
        <input
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="new.user@example.com"
          className="rounded-lg border border-border bg-surface-3 px-3 py-2 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
        />
      </Field>
      <Field label="Temporary password">
        <input
          type="text"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder="they must change it on first sign-in"
          className="rounded-lg border border-border bg-surface-3 px-3 py-2 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
        />
      </Field>
      <Field label="Role">
        <select
          value={role}
          onChange={(e) => setRole(e.target.value)}
          className="rounded-lg border border-border bg-surface-3 px-3 py-2 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
        >
          {ROLES.map((r) => (
            <option key={r} value={r}>
              {r}
            </option>
          ))}
        </select>
      </Field>
      <Button
        variant="primary"
        isDisabled={create.isPending || email.trim().length === 0 || password.length === 0}
        onPress={onCreate}
      >
        {create.isPending ? 'Creating…' : 'Create user'}
      </Button>
      {create.isError ? <p className="w-full text-sm text-red-400">{create.error.message}</p> : null}
    </div>
  )
}

function AiProviderForm() {
  const { data: provider } = useAiProvider()
  const update = useUpdateAiProvider()

  const [providerKind, setProviderKind] = useState('OpenAI')
  const [endpoint, setEndpoint] = useState('')
  const [model, setModel] = useState('')
  const [apiKey, setApiKey] = useState('')

  // Hydrate the form once the stored config loads. The API key is never returned, so it stays blank.
  useEffect(() => {
    if (provider) {
      setProviderKind(provider.provider)
      setEndpoint(provider.endpoint ?? '')
      setModel(provider.model)
    }
  }, [provider])

  function onSave() {
    update.mutate({
      provider: providerKind,
      endpoint: endpoint.trim() || null,
      apiKey: apiKey.trim() || null,
      model: model.trim(),
    })
  }

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-lg font-medium text-content">AI provider</h2>
        <p className="text-sm text-muted">
          A BYO OpenAI-compatible (or Azure OpenAI) endpoint + model that Cleanup and Summaries use. Takes
          effect on the next run.
        </p>
      </div>
      <div className="grid max-w-xl gap-3">
        <Field label="Provider">
          <select
            value={providerKind}
            onChange={(e) => setProviderKind(e.target.value)}
            className="w-full rounded-lg border border-border bg-surface-3 px-3 py-2 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            {PROVIDERS.map((p) => (
              <option key={p} value={p}>
                {p}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Endpoint">
          <input
            value={endpoint}
            onChange={(e) => setEndpoint(e.target.value)}
            placeholder="http://localhost:11434/v1"
            className="w-full rounded-lg border border-border bg-surface-3 px-3 py-2 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
          />
        </Field>
        <Field label="Model">
          <input
            value={model}
            onChange={(e) => setModel(e.target.value)}
            placeholder="llama3.1"
            className="w-full rounded-lg border border-border bg-surface-3 px-3 py-2 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
          />
        </Field>
        <Field label={provider?.hasApiKey ? 'API key (leave blank to keep current)' : 'API key'}>
          <input
            type="password"
            value={apiKey}
            onChange={(e) => setApiKey(e.target.value)}
            placeholder={provider?.hasApiKey ? '•••••••• (set)' : 'optional for local providers'}
            className="w-full rounded-lg border border-border bg-surface-3 px-3 py-2 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent"
          />
        </Field>
        <div className="flex items-center gap-3">
          <Button variant="primary" isDisabled={update.isPending || model.trim().length === 0} onPress={onSave}>
            {update.isPending ? 'Saving…' : 'Save provider'}
          </Button>
          {update.isSuccess ? <span className="text-sm text-muted">Saved</span> : null}
          {update.isError ? <span className="text-sm text-red-400">{update.error.message}</span> : null}
        </div>
      </div>
    </div>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="space-y-1">
      <span className="text-sm text-muted">{label}</span>
      {children}
    </label>
  )
}
