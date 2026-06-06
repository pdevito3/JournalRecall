import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { z } from 'zod'
import { useForm } from '@tanstack/react-form'
import { FormShell, TextField, applyServerErrors, emailSchema, passwordSchema, passwordsMatch } from '@/shared/forms'
import { useLogin, useRegister } from '@/features/auth/useAuth'

export const Route = createFileRoute('/register')({
  component: RegisterPage,
})

// Self-registration: own schema, but it imports the shared password + email fragments so the policy
// and match rule stay in lockstep with setup and change-password.
const registerSchema = z
  .object({
    email: emailSchema,
    password: passwordSchema,
    confirmPassword: z.string(),
  })
  .superRefine(passwordsMatch())

function RegisterPage() {
  const navigate = useNavigate()
  const register = useRegister()
  const login = useLogin()

  const form = useForm({
    defaultValues: { email: '', password: '', confirmPassword: '' },
    validators: { onBlur: registerSchema },
    onSubmit: async ({ value }) => {
      const credentials = { email: value.email, password: value.password }
      try {
        await register.mutateAsync(credentials)
        // Registration doesn't sign you in — log in immediately so the cookie is set.
        await login.mutateAsync(credentials)
        await navigate({ to: '/' })
      } catch (error) {
        applyServerErrors(form, error)
      }
    },
  })

  return (
    <FormShell
      form={form}
      title="Create your account"
      submitLabel="Create account"
      pendingLabel="Creating account…"
      footer={
        <>
          Already have an account?{' '}
          <Link to="/login" className="text-accent hover:underline">
            Sign in
          </Link>
        </>
      }
    >
      <form.Field name="email">
        {(field) => <TextField field={field} label="Email" type="email" autoFocus autoComplete="email" />}
      </form.Field>
      <form.Field name="password">
        {(field) => <TextField field={field} label="Password" type="password" autoComplete="new-password" />}
      </form.Field>
      <form.Field name="confirmPassword">
        {(field) => <TextField field={field} label="Confirm password" type="password" autoComplete="new-password" />}
      </form.Field>
    </FormShell>
  )
}
