export function ConfirmDialog({
  title,
  body,
  confirmLabel,
  pending,
  onCancel,
  onConfirm,
}: {
  title: string;
  body: string;
  confirmLabel: string;
  pending?: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <div className="fixed inset-0 z-20 flex items-center justify-center bg-black/60 p-4" role="presentation">
      <div aria-modal="true" className="w-full max-w-md rounded-2xl border border-line bg-surface p-6" role="dialog">
        <h2 className="text-xl font-semibold">{title}</h2>
        <p className="mt-3 text-muted">{body}</p>
        <div className="mt-6 flex justify-end gap-3">
          <button className="min-h-11 rounded-full px-4" onClick={onCancel} type="button">
            Cancel
          </button>
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={pending} onClick={onConfirm} type="button">
            {pending ? "Working..." : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
