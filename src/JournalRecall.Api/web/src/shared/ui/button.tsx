import { Button as AriaButton, type ButtonProps as AriaButtonProps } from 'react-aria-components'
import { cn } from '@/shared/utils/cn'

// A thin, styled wrapper over React Aria's Button — accessibility and interaction state come from
// React Aria; styling rides its render-prop state.

type Variant = 'primary' | 'ghost' | 'icon'

const variants: Record<Variant, string> = {
  primary: 'h-10 px-4 font-medium bg-accent text-accent-fg hover:bg-accent-strong',
  ghost: 'h-9 px-3 text-content hover:bg-surface-3',
  icon: 'size-9 text-muted hover:text-content hover:bg-surface-3',
}

export interface ButtonProps extends Omit<AriaButtonProps, 'className'> {
  variant?: Variant
  className?: string
}

export function Button({ variant = 'ghost', className, ...props }: ButtonProps) {
  return (
    <AriaButton
      {...props}
      className={cn(
        'inline-flex shrink-0 items-center justify-center gap-2 rounded-lg text-sm transition-colors',
        'outline-none focus-visible:ring-2 focus-visible:ring-accent',
        'disabled:cursor-not-allowed disabled:opacity-50',
        variants[variant],
        className,
      )}
    />
  )
}
