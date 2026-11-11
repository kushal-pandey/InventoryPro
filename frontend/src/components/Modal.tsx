import { useEffect, useRef, type ReactNode } from 'react'
import { X } from 'lucide-react'

interface ModalProps {
  title: string
  onClose: () => void
  children: ReactNode
  wide?: boolean
}

/** Accessible modal built on the native <dialog>: focus trap, Esc to close and inert background for free. */
export function Modal({ title, onClose, children, wide = false }: ModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const suppressedCloseEvents = useRef(0)

  useEffect(() => {
    const dialog = dialogRef.current
    const previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null
    if (dialog && !dialog.open) {
      dialog.showModal()
      const firstFocusable = dialog.querySelector<HTMLElement>(
        '[autofocus], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), button:not([disabled]), [tabindex]:not([tabindex="-1"])',
      )
      firstFocusable?.focus()
    }
    return () => {
      if (dialog?.open) {
        suppressedCloseEvents.current += 1
        dialog.close()
      }
      previouslyFocused?.focus()
    }
  }, [])

  function handleClose() {
    if (suppressedCloseEvents.current > 0) {
      suppressedCloseEvents.current -= 1
      return
    }
    onClose()
  }

  return (
    <dialog
      ref={dialogRef}
      className={wide ? 'modal modal-wide' : 'modal'}
      aria-label={title}
      onClose={handleClose}
      onClick={(event) => {
        if (event.target === dialogRef.current) onClose()
      }}
    >
      <div className="modal-card">
        <header className="modal-header">
          <h2>{title}</h2>
          <button type="button" className="icon-button" aria-label="Close" onClick={onClose}>
            <X size={18} />
          </button>
        </header>
        {children}
      </div>
    </dialog>
  )
}
