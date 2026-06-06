import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { z } from 'zod'
import { useForm } from '@tanstack/react-form'
import { FormShell, TextField, applyServerErrors, usernameSchema } from '@/shared/forms'
import { useAuthConfig, useLogin } from '@/features/auth/useAuth'

export const Route = createFileRoute('/login')({
  component: LoginPage,
})

// Sign-in only needs a non-empty username and password — it doesn't re-check the format policy.
const loginSchema = z.object({
  username: usernameSchema,
  password: z.string().min(1, 'Enter your password.'),
})

function LoginPage() {
  const navigate = useNavigate()
  const login = useLogin()
  const { data: config } = useAuthConfig()

  const form = useForm({
    defaultValues: { username: '', password: '' },
    validators: { onBlur: loginSchema },
    onSubmit: async ({ value }) => {
      try {
        await login.mutateAsync(value)
        await navigate({ to: '/' })
      } catch (error) {
        // Invalid credentials (and any other server error) surface as the top-level banner.
        applyServerErrors(form, error)
      }
    },
  })

  return (
    <FormShell
      form={form}
      title="Sign in"
      submitLabel="Sign in"
      pendingLabel="Signing in…"
      footer={
        // The "create an account" link appears only when the operator has opened self-registration.
        config?.selfRegistrationEnabled ? (
          <>
            New here?{' '}
            <Link to="/register" className="text-accent hover:underline">
              Create an account
            </Link>
          </>
        ) : null
      }
    >
      <form.Field name="username">
        {(field) => <TextField field={field} label="Username" autoFocus autoComplete="username" />}
      </form.Field>
      <form.Field name="password">
        {(field) => <TextField field={field} label="Password" type="password" autoComplete="current-password" />}
      </form.Field>
    </FormShell>
  )
}
