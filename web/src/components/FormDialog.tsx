export function FormDialog({
  title,
  header,
  onCancel,
  wide,
  large,
  children,
}: {
  title: string;
  header?: React.ReactNode;
  onCancel: () => void;
  wide?: boolean;
  large?: boolean;
  children: React.ReactNode;
}) {
  const width = large ? "max-h-[94vh] max-w-6xl" : wide ? "max-h-[90vh] max-w-3xl" : "max-h-[90vh] max-w-lg";
  return (
    <div className="fixed inset-0 z-20 flex items-center justify-center bg-black/60 p-4" role="presentation">
      <div aria-labelledby="form-dialog-title" aria-modal="true" className={`flex w-full min-w-0 flex-col overflow-hidden rounded-2xl border border-line bg-surface ${width}`} role="dialog">
        <div className="shrink-0 border-b border-line px-6 py-5">
          {header ?? (
            <div className="flex items-start justify-between gap-4">
              <h2 className="text-xl font-semibold" id="form-dialog-title">{title}</h2>
              <button className="min-h-11 rounded-full border border-line px-4" onClick={onCancel} type="button">Close</button>
            </div>
          )}
        </div>
        <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-6 py-5">{children}</div>
      </div>
    </div>
  );
}

export function FormField({ label, error, children }: { label: string; error?: string; children: React.ReactNode }) {
  return (
    <label className="grid gap-2">
      {label}
      {children}
      {error ? <span className="text-sm text-red-300">{error}</span> : null}
    </label>
  );
}

export function FormActions({ cancel, pending, submitLabel }: { cancel: () => void; pending: boolean; submitLabel: string }) {
  return (
    <div className="mt-2 flex justify-end gap-3">
      <button className="min-h-11 rounded-full px-4" onClick={cancel} type="button">Cancel</button>
      <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={pending} type="submit">
        {pending ? "Saving..." : submitLabel}
      </button>
    </div>
  );
}
