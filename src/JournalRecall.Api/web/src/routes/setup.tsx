import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { z } from 'zod'
import { useForm } from '@tanstack/react-form'
import { Form, TextField, createForm, usernameSchema, passwordSchema, passwordsMatch } from '@/shared/forms'
import { useLogin, useSetup } from '@/features/auth'

export const Route = createFileRoute('/setup')({
  component: SetupPage,
})

// First-run root-Admin bootstrap: kept a separate form from register (independent lifecycle), but it
// imports the same shared password + username fragments so the rules don't drift apart.
const setupSchema = z
  .object({
    username: usernameSchema,
    password: passwordSchema,
    confirmPassword: z.string(),
  })
  .superRefine(passwordsMatch())

const { Field, applyServerErrors } = createForm<typeof setupSchema>()

function SetupPage() {
  const navigate = useNavigate()
  const setup = useSetup()
  const login = useLogin()

  const form = useForm({
    defaultValues: { username: '', password: '', confirmPassword: '' },
    validators: { onChange: setupSchema },
    onSubmit: async ({ value }) => {
      const credentials = { username: value.username, password: value.password }
      try {
        // Create the root Admin, then sign in immediately so the cookie is set and the app opens.
        await setup.mutateAsync(credentials)
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
      title="Set up JournalRecall"
      footer="This creates the first administrator account for a brand-new instance."
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
      <Form.Submit pendingLabel="Creating…">Create admin account</Form.Submit>
    </Form>
  )
}
