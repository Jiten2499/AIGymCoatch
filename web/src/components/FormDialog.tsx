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
  const width = large ? "max-w-6xl" : wide ? "max-w-3xl" : "max-w-lg";
  return (
    <div className="fixed inset-0 z-20 overflow-y-auto bg-black/60 p-4" role="presentation">
      <div className="flex min-h-full items-center justify-center">
        <div aria-labelledby="form-dialog-title" aria-modal="true" className={`my-4 w-full min-w-0 rounded-2xl border border-line bg-surface ${width}`} role="dialog">
          <div className="border-b border-line px-6 py-5">
            {header ?? (
              <div className="flex items-start justify-between gap-4">
                <h2 className="text-xl font-semibold" id="form-dialog-title">{title}</h2>
                <button className="min-h-11 rounded-full border border-line px-4" onClick={onCancel} type="button">Close</button>
              </div>
            )}
          </div>
          <div className="px-6 py-5">{children}</div>
        </div>
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
