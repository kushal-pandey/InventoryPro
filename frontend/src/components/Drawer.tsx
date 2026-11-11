import { useEffect, useRef, type ReactNode } from 'react'
import { X } from 'lucide-react'

export function Drawer({ title, onClose, children }: { title: string; onClose: () => void; children: ReactNode }) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const suppressedCloseEvents = useRef(0)

  useEffect(() => {
    const dialog = dialogRef.current
    const previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null
    dialog?.showModal()
    dialog?.querySelector<HTMLElement>('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])')?.focus()
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
    <dialog ref={dialogRef} className="drawer" aria-label={title} onClose={handleClose}>
      <div className="drawer-card">
        <header className="modal-header">
          <h2>{title}</h2>
          <button type="button" className="icon-button" aria-label="Close order details" onClick={onClose}>
            <X size={18} aria-hidden="true" />
          </button>
        </header>
        {children}
      </div>
    </dialog>
  )
}
