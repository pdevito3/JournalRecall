import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/chat')({
  component: ChatComingSoon,
})

function ChatComingSoon() {
  return (
    <section className="flex flex-col items-center justify-center gap-3 py-16 text-center">
      <h1 className="text-2xl font-semibold text-content">Chat</h1>
      <p className="max-w-md text-muted">
        Coming soon — ask questions across your journal once retrieval lands. For now this is a
        placeholder so the route and navigation exist end to end.
      </p>
    </section>
  )
}
