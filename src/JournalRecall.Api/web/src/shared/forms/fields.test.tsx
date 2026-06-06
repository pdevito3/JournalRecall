import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { z } from 'zod'
import { useForm } from '@tanstack/react-form'
import { CheckboxField, SelectField, TextField } from './fields'

/**
 * Render a single bound field inside a real form. Tests assert what a consumer observes — rendered
 * error text, aria-invalid, the value that propagates back — never react-form's internal state shape.
 */
function FieldHarness({
  defaultValue,
  validator,
  children,
}: {
  defaultValue: unknown
  validator?: z.ZodType
  children: (field: any) => React.ReactNode
}) {
  const form = useForm({
    defaultValues: { value: defaultValue },
    validators: validator ? { onBlur: z.object({ value: validator }) } : undefined,
  })
  return (
    <form>
      <form.Field name="value">{(field) => <>{children(field)}</>}</form.Field>
      <form.Subscribe selector={(s) => s.values.value}>
        {(value) => <output data-testid="value">{JSON.stringify(value)}</output>}
      </form.Subscribe>
    </form>
  )
}

describe('TextField', () => {
  it('renders the label associated with the input', () => {
    render(<FieldHarness defaultValue="">{(field) => <TextField field={field} label="Email" />}</FieldHarness>)
    expect(screen.getByLabelText('Email')).toBeInTheDocument()
  })

  it('propagates the raw typed value back to the form', async () => {
    const user = userEvent.setup()
    render(<FieldHarness defaultValue="">{(field) => <TextField field={field} label="Email" />}</FieldHarness>)

    await user.type(screen.getByLabelText('Email'), 'me@example.com')
    expect(screen.getByTestId('value')).toHaveTextContent('"me@example.com"')
  })

  it('shows error text and sets aria-invalid once the field has errored on blur', async () => {
    const user = userEvent.setup()
    render(
      <FieldHarness defaultValue="" validator={z.email('Enter a valid email address.')}>
        {(field) => <TextField field={field} label="Email" type="email" />}
      </FieldHarness>,
    )

    const input = screen.getByLabelText('Email')
    await user.type(input, 'nope')
    await user.tab() // blur triggers onBlur validation

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument()
    expect(input).toHaveAttribute('aria-invalid', 'true')
  })
})

describe('SelectField', () => {
  it('renders the label and propagates the chosen option back to the form', async () => {
    const user = userEvent.setup()
    render(
      <FieldHarness defaultValue="Member">
        {(field) => <SelectField field={field} label="Role" options={['Member', 'Admin']} />}
      </FieldHarness>,
    )

    await user.click(screen.getByLabelText('Role'))
    await user.click(await screen.findByRole('option', { name: 'Admin' }))

    expect(screen.getByTestId('value')).toHaveTextContent('"Admin"')
  })
})

describe('CheckboxField', () => {
  it('propagates the checked state back to the form', async () => {
    const user = userEvent.setup()
    render(
      <FieldHarness defaultValue={false}>
        {(field) => <CheckboxField field={field} label="Hard-replace" />}
      </FieldHarness>,
    )

    expect(screen.getByTestId('value')).toHaveTextContent('false')
    await user.click(screen.getByLabelText('Hard-replace'))
    expect(screen.getByTestId('value')).toHaveTextContent('true')
  })
})
