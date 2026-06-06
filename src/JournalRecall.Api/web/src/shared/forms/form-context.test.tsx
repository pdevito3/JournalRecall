import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { useForm } from '@tanstack/react-form'
import { Form } from './form-shell'
import { useFormContext } from './form-context'

/** Reads the context and reports whether it resolved to the provided form instance. */
function ContextProbe({ onForm }: { onForm: (form: unknown) => void }) {
  const form = useFormContext()
  onForm(form)
  return <p data-testid="probe">resolved</p>
}

describe('useFormContext', () => {
  it('throws a clear error when used outside <Form>', () => {
    // Silence React's error-boundary console noise for the expected throw.
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})
    expect(() => render(<ContextProbe onForm={() => {}} />)).toThrow(/Form\.\* must be used within <Form>/)
    spy.mockRestore()
  })

  it('returns the form provided by <Form> when used within it', () => {
    let provided: unknown
    let resolved: unknown
    function Harness() {
      const form = useForm({ defaultValues: { email: '' } })
      provided = form
      return (
        <Form form={form}>
          <ContextProbe onForm={(f) => (resolved = f)} />
        </Form>
      )
    }
    render(<Harness />)
    expect(screen.getByTestId('probe')).toBeInTheDocument()
    expect(resolved).toBe(provided)
  })
})
