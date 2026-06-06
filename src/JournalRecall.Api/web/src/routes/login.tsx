import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { z } from 'zod'
import { useForm } from '@tanstack/react-form'
import { FormShell, TextField, applyServerErrors, emailSchema } from '@/shared/forms'
import { useAuthConfig, useLogin } from '@/features/auth/useAuth'

export const Route = createFileRoute('/login')({
  component: LoginPage,
})

// Sign-in only needs a well-formed email and a non-empty password — it doesn't re-check the policy.
const loginSchema = z.object({
  email: emailSchema,
  password: z.string().min(1, 'Enter your password.'),
})

function LoginPage() {
  const navigate = useNavigate()
  const login = useLogin()
  const { data: config } = useAuthConfig()

  const form = useForm({
    defaultValues: { email: '', password: '' },
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
      <form.Field name="email">
        {(field) => <TextField field={field} label="Email" type="email" autoFocus autoComplete="email" />}
      </form.Field>
      <form.Field name="password">
        {(field) => <TextField field={field} label="Password" type="password" autoComplete="current-password" />}
      </form.Field>
    </FormShell>
  )
}
