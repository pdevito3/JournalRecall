import { useState } from 'react'
import { useForm } from '@tanstack/react-form'
import { z } from 'zod'
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
} from '../useAdmin'
import { PROVIDERS, ROLES, type AdminUser, type AiProvider } from '../api'
import { Button } from '@/shared/ui/button'
import {
  createForm,
  usernameSchema,
  Form,
  passwordSchema,
  SelectField,
  TextField,
} from '@/shared/forms'

/**
 * The Admin surface: user management (registration settings + the user list/create form) and the
 * app-wide AI-provider config. Carries NO access check — the route `beforeLoad` gate (FE-006) admits
 * only Admins before this renders. Reads its first-paint data from caches the route loader primed.
 */
export function AdminPage() {
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
  const enabled = settings.selfRegistrationEnabled

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
          disabled={update.isPending}
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

const createUserForm = createForm<typeof createUserSchema>()

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
        createUserForm.applyServerErrors(form, e)
      }
    },
  })

  return (
    <Form
      form={form}
      className="space-y-4 rounded-lg border border-dashed border-border bg-surface-2 p-3"
    >
      <createUserForm.Field name="username">
        {(field) => <TextField field={field} label="Username" autoComplete="username" placeholder="new.user" />}
      </createUserForm.Field>
      <createUserForm.Field name="password">
        {(field) => (
          <TextField
            field={field}
            label="Temporary password"
            type="text"
            placeholder="they must change it on first sign-in"
          />
        )}
      </createUserForm.Field>
      <createUserForm.Field name="role">
        {(field) => <SelectField field={field} label="Role" options={ROLES} />}
      </createUserForm.Field>
      <Form.Errors />
      <Form.Submit pendingLabel="Creating…">Create user</Form.Submit>
    </Form>
  )
}

const aiProviderSchema = z.object({
  provider: z.string(),
  endpoint: z.string(),
  model: z.string().trim().min(1, 'Model is required.'),
  apiKey: z.string(),
})

const aiProviderForm = createForm<typeof aiProviderSchema>()

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
      <AiProviderFormInner key={aiProviderKey(provider)} provider={provider} />
    </div>
  )
}

// Change-token for the AI-provider form: the singleton config DTO carries no revision field, so derive
// one from its values. When a saved change lands (this admin or another), the key changes and the form
// remounts, re-seeding its defaults from the fresh server values instead of the ones captured at mount.
function aiProviderKey(provider: AiProvider): string {
  return JSON.stringify([provider.provider, provider.endpoint, provider.model, provider.hasApiKey])
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
        aiProviderForm.applyServerErrors(form, e)
      }
    },
  })

  return (
    <Form
      form={form}
      className="grid max-w-xl gap-3"
      footer={update.isSuccess ? 'Saved' : undefined}
    >
      <aiProviderForm.Field name="provider">
        {(field) => <SelectField field={field} label="Provider" options={PROVIDERS} />}
      </aiProviderForm.Field>
      <aiProviderForm.Field name="endpoint">
        {(field) => <TextField field={field} label="Endpoint" placeholder="http://localhost:11434/v1" />}
      </aiProviderForm.Field>
      <aiProviderForm.Field name="model">
        {(field) => <TextField field={field} label="Model" placeholder="llama3.1" />}
      </aiProviderForm.Field>
      <aiProviderForm.Field name="apiKey">
        {(field) => (
          <TextField
            field={field}
            label={provider.hasApiKey ? 'API key (leave blank to keep current)' : 'API key'}
            type="password"
            placeholder={provider.hasApiKey ? '•••••••• (set)' : 'optional for local providers'}
          />
        )}
      </aiProviderForm.Field>
      <Form.Errors />
      <Form.Submit pendingLabel="Saving…">Save provider</Form.Submit>
    </Form>
  )
}
