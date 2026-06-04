import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/')({
  component: Home,
})

function Home() {
  return (
    <section className="space-y-3">
      <h1 className="text-2xl font-semibold text-content">Your journal</h1>
      <p className="text-muted">
        Start a session, write or dictate freely, and pick up where you left off. Your raw words stay
        yours — optional AI cleanup arrives in a later slice.
      </p>
    </section>
  )
}
