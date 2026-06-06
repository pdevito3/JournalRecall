/**
 * Bound field components (PRD-0004 / ADR-0007). Each bridges a react-aria-components input to
 * `@tanstack/react-form` exactly once: it takes the react-form `field` via an explicit prop (no
 * `createFormHook`/context), forwards react-aria's `onChange(value)` (the raw value, not a DOM event)
 * and `onBlur` to `field.handleChange`/`field.handleBlur`, and maps `field.state.meta.errors` to
 * react-aria's `isInvalid` + `FieldError` so a11y (`aria-invalid`/`aria-describedby`) and error text
 * are correct and consistent for free. No form re-derives this wiring.
 */

import type { ReactNode } from 'react'
import {
  Button as AriaButton,
  Checkbox as AriaCheckbox,
  Select as AriaSelect,
  TextField as AriaTextField,
  FieldError,
  Input,
  Label,
  ListBox,
  ListBoxItem,
  Popover,
  SelectValue,
} from 'react-aria-components'
import type { AnyFieldApi } from '@tanstack/react-form'
import { cn } from '@/shared/utils/cn'

const inputClass =
  'h-10 w-full rounded-lg border border-border bg-surface-2 px-3 text-content outline-none focus-visible:ring-2 focus-visible:ring-accent aria-[invalid=true]:border-red-400'

const labelClass = 'text-sm text-muted'
const errorClass = 'text-sm text-red-400'

/** react-form stores errors as `unknown[]` — zod issues (objects with `.message`) or server strings. */
function messagesOf(errors: readonly unknown[]): string[] {
  return errors
    .map((error) => {
      if (typeof error === 'string') return error
      if (error && typeof error === 'object' && 'message' in error) return String((error as { message: unknown }).message)
      return error == null ? '' : String(error)
    })
    .filter((message) => message.length > 0)
}

interface TextFieldProps {
  field: AnyFieldApi
  label: string
  type?: 'text' | 'password' | 'email'
  placeholder?: string
  autoFocus?: boolean
  autoComplete?: string
}

export function TextField({ field, label, type = 'text', placeholder, autoFocus, autoComplete }: TextFieldProps) {
  const messages = messagesOf(field.state.meta.errors)
  return (
    <AriaTextField
      className="flex flex-col gap-1"
      name={field.name}
      value={(field.state.value as string | undefined) ?? ''}
      onChange={(value) => field.handleChange(value)}
      onBlur={field.handleBlur}
      type={type}
      isInvalid={messages.length > 0}
      autoComplete={autoComplete}
    >
      <Label className={labelClass}>{label}</Label>
      <Input autoFocus={autoFocus} placeholder={placeholder} className={inputClass} />
      <FieldError className={errorClass}>{messages.join(' ')}</FieldError>
    </AriaTextField>
  )
}

export type SelectOption = string | { id: string; label: string }

interface SelectFieldProps {
  field: AnyFieldApi
  label: string
  options: readonly SelectOption[]
  placeholder?: string
}

export function SelectField({ field, label, options, placeholder = 'Select…' }: SelectFieldProps) {
  const messages = messagesOf(field.state.meta.errors)
  const items = options.map((option) => (typeof option === 'string' ? { id: option, label: option } : option))
  return (
    <AriaSelect
      className="flex flex-col gap-1"
      name={field.name}
      selectedKey={(field.state.value as string | undefined) ?? null}
      onSelectionChange={(key) => field.handleChange(key == null ? '' : String(key))}
      onBlur={field.handleBlur}
      isInvalid={messages.length > 0}
      placeholder={placeholder}
    >
      <Label className={labelClass}>{label}</Label>
      <AriaButton
        className={cn(
          inputClass,
          'flex items-center justify-between gap-2 text-left outline-none focus-visible:ring-2 focus-visible:ring-accent',
        )}
      >
        <SelectValue className="truncate data-[placeholder]:text-muted" />
        <span aria-hidden className="text-muted">
          ▾
        </span>
      </AriaButton>
      <FieldError className={errorClass}>{messages.join(' ')}</FieldError>
      <Popover className="w-[--trigger-width] rounded-lg border border-border bg-surface-2 p-1 shadow-lg outline-none">
        <ListBox className="outline-none">
          {items.map((item) => (
            <ListBoxItem
              key={item.id}
              id={item.id}
              className="cursor-pointer rounded-md px-3 py-2 text-content outline-none data-[focused]:bg-surface-3 data-[selected]:text-accent"
            >
              {item.label}
            </ListBoxItem>
          ))}
        </ListBox>
      </Popover>
    </AriaSelect>
  )
}

interface CheckboxFieldProps {
  field: AnyFieldApi
  label: ReactNode
}

export function CheckboxField({ field, label }: CheckboxFieldProps) {
  return (
    <AriaCheckbox
      className="group flex items-center gap-2 text-sm text-content"
      isSelected={Boolean(field.state.value)}
      onChange={(isSelected) => field.handleChange(isSelected)}
      onBlur={field.handleBlur}
    >
      {({ isSelected }) => (
        <>
          <span
            className={cn(
              'flex size-4 shrink-0 items-center justify-center rounded border text-[10px] leading-none',
              isSelected ? 'border-accent bg-accent text-accent-fg' : 'border-border bg-surface-2',
              'group-data-[focus-visible]:ring-2 group-data-[focus-visible]:ring-accent',
            )}
            aria-hidden
          >
            {isSelected ? '✓' : ''}
          </span>
          {label}
        </>
      )}
    </AriaCheckbox>
  )
}
