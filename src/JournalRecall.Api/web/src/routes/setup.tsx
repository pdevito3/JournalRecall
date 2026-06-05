import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { AuthForm } from '@/features/auth/components/auth-form'
import { useLogin, useSetup } from '@/features/auth/useAuth'

export const Route = createFileRoute('/setup')({
  component: SetupPage,
})

function SetupPage() {
  const navigate = useNavigate()
  const setup = useSetup()
  const login = useLogin()

  return (
    <AuthForm
      title="Set up JournalRecall"
      submitLabel="Create admin account"
      pending={setup.isPending || login.isPending}
      error={setup.error?.message ?? login.error?.message}
      // Create the root Admin, then sign in immediately so the cookie is set and the app opens.
      onSubmit={(credentials) =>
        setup.mutate(credentials, {
          onSuccess: () => login.mutate(credentials, { onSuccess: () => navigate({ to: '/' }) }),
        })
      }
      footer="This creates the first administrator account for a brand-new instance."
    />
  )
}
