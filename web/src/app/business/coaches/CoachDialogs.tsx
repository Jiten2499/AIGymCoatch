"use client";

import { useState } from "react";
import type { CreatedCoach } from "../../../lib/types";

export function PasswordDialog({ created, notice, activationQueued = false, onClose }: { created: CreatedCoach; notice?: string | null; activationQueued?: boolean; onClose: () => void }) {
  const password = created.temporaryPassword;
  const [copied, setCopied] = useState(false);
  return (
    <div className="fixed inset-0 z-20 flex items-center justify-center bg-black/60 p-4" role="presentation">
      <div aria-labelledby="coach-dialog-title" aria-modal="true" className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl border border-line bg-surface p-6" role="dialog">
        <div className="flex items-start justify-between gap-4">
          <h2 className="text-xl font-semibold" id="coach-dialog-title">Coach account</h2>
          <button aria-label="Close" className="flex size-11 shrink-0 items-center justify-center rounded-full border border-line text-lg" onClick={onClose} type="button">×</button>
        </div>
        <p className="mt-3">{created.firstName} {created.lastName} can sign in as {created.email}.</p>
        {notice ? <p className="mt-3" role="alert">{notice}</p> : null}
        {password ? (
          <label className="mt-4 grid gap-2">
            Password
            <input aria-label="Temporary password" className="field font-mono" readOnly value={password} />
          </label>
        ) : activationQueued ? (
          <p className="mt-3">An activation link was queued for {created.email}. They set their own password from that email. An account that already signs in to another business keeps its current password.</p>
        ) : (
          <p className="mt-3">No activation email was sent. Send an activation link so they can set a password.</p>
        )}
        <div className="mt-6 flex justify-end gap-3">
          {password ? (
            <button className="min-h-11 rounded-full px-4" onClick={() => { void navigator.clipboard.writeText(password).then(() => setCopied(true)); }} type="button">
              {copied ? "Copied" : "Copy password"}
            </button>
          ) : null}
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" onClick={onClose} type="button">Close</button>
        </div>
      </div>
    </div>
  );
}
