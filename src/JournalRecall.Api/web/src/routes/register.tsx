import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { z } from 'zod'
import { useForm } from '@tanstack/react-form'
import { Form, TextField, createForm, usernameSchema, passwordSchema, passwordsMatch } from '@/shared/forms'
import { useLogin, useRegister } from '@/features/auth'

export const Route = createFileRoute('/register')({
  component: RegisterPage,
})

// Self-registration: own schema, but it imports the shared password + username fragments so the policy
// and match rule stay in lockstep with setup and change-password.
const registerSchema = z
  .object({
    username: usernameSchema,
    password: passwordSchema,
    confirmPassword: z.string(),
  })
  .superRefine(passwordsMatch())

const { Field, applyServerErrors } = createForm<typeof registerSchema>()

function RegisterPage() {
  const navigate = useNavigate()
  const register = useRegister()
  const login = useLogin()

  const form = useForm({
    defaultValues: { username: '', password: '', confirmPassword: '' },
    validators: { onBlur: registerSchema },
    onSubmit: async ({ value }) => {
      const credentials = { username: value.username, password: value.password }
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
    <Form
      form={form}
      title="Create your account"
      footer={
        <>
          Already have an account?{' '}
          <Link to="/login" className="text-accent hover:underline">
            Sign in
          </Link>
        </>
      }
    >
      <Field name="username">
        {(field) => <TextField field={field} label="Username" autoFocus autoComplete="username" />}
      </Field>
      <Field name="password">
        {(field) => <TextField field={field} label="Password" type="password" autoComplete="new-password" />}
      </Field>
      <Field name="confirmPassword">
        {(field) => <TextField field={field} label="Confirm password" type="password" autoComplete="new-password" />}
      </Field>
      <Form.Errors />
      <Form.Submit pendingLabel="Creating account…">Create account</Form.Submit>
    </Form>
  )
}
