import { useCallback, useEffect, useRef } from 'react'

/**
 * Returns a debounced callback that defers `save` until `delay` ms after the last call.
 * The pending timer is cleared on unmount so a save can't fire after the editor is gone.
 */
export function useAutosave<T>(save: (value: T) => void, delay = 2000) {
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  useEffect(() => () => clearTimeout(timer.current), [])

  return useCallback(
    (value: T) => {
      clearTimeout(timer.current)
      timer.current = setTimeout(() => save(value), delay)
    },
    [save, delay],
  )
}
