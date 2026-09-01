import { useEffect, useRef, type KeyboardEvent as ReactKeyboardEvent } from "react";
import { createPortal } from "react-dom";
import { ChevronLeft, ChevronRight, X } from "lucide-react";

export type GalleryShot = {
  src: string;
  alt: string;
  label: string;
};

export function Lightbox({
  shots,
  index,
  onClose,
  onIndexChange,
}: {
  shots: GalleryShot[];
  index: number | null;
  onClose: () => void;
  onIndexChange: (index: number) => void;
}) {
  const closeRef = useRef<HTMLButtonElement>(null);
  const open = index !== null && shots[index] !== undefined;
  const shot = open ? shots[index] : null;

  useEffect(() => {
    if (!open) {
      return;
    }

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    closeRef.current?.focus();

    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose();
      } else if (event.key === "ArrowLeft") {
        onIndexChange((index! - 1 + shots.length) % shots.length);
      } else if (event.key === "ArrowRight") {
        onIndexChange((index! + 1) % shots.length);
      }
    };

    window.addEventListener("keydown", onKey);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", onKey);
    };
  }, [open, index, shots.length, onClose, onIndexChange]);

  if (typeof document === "undefined" || !open || shot === null) {
    return null;
  }

  const go = (delta: number) => onIndexChange((index! + delta + shots.length) % shots.length);

  const onDialogKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    if (event.key !== "Tab") {
      return;
    }

    const focusable = event.currentTarget.querySelectorAll<HTMLElement>("button");
    if (focusable.length === 0) {
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  return createPortal(
    <div
      className="lightbox-backdrop"
      role="dialog"
      aria-modal="true"
      aria-label={shot.label}
      onClick={onClose}
      onKeyDown={onDialogKeyDown}
    >
      <button
        ref={closeRef}
        type="button"
        className="lightbox-close"
        onClick={onClose}
        aria-label="Close image viewer"
      >
        <X className="size-5" aria-hidden />
      </button>
      {shots.length > 1 ? (
        <>
          <button
            type="button"
            className="lightbox-nav lightbox-nav-prev"
            onClick={(event) => {
              event.stopPropagation();
              go(-1);
            }}
            aria-label="Previous screenshot"
          >
            <ChevronLeft className="size-7" aria-hidden />
          </button>
          <button
            type="button"
            className="lightbox-nav lightbox-nav-next"
            onClick={(event) => {
              event.stopPropagation();
              go(1);
            }}
            aria-label="Next screenshot"
          >
            <ChevronRight className="size-7" aria-hidden />
          </button>
        </>
      ) : null}
      <figure
        className="lightbox-frame"
        onClick={(event) => event.stopPropagation()}
      >
        <img src={shot.src} alt={shot.alt} className="lightbox-image" />
        <figcaption className="lightbox-caption">
          <span>{shot.label}</span>
          <span className="lightbox-count">
            {index! + 1} / {shots.length}
          </span>
        </figcaption>
        <p className="lightbox-hint">Esc to close · arrow keys to browse</p>
      </figure>
    </div>,
    document.body,
  );
}
