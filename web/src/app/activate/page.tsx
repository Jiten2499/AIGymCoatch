"use client";

import { useMutation } from "@tanstack/react-query";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { api, ApiError } from "../../lib/api";

type Preview = { fullName: string; email: string; expiresAt: string };

function ActivateForm() {
  const router = useRouter();
  const params = useSearchParams();
  const token = params.get("token") ?? "";
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [notice, setNotice] = useState<string | null>(null);

  const preview = useMutation({
    mutationFn: () => api<Preview>("/auth/activation/preview", { method: "POST", body: JSON.stringify({ token }) }),
  });

  const activate = useMutation({
    mutationFn: () => api("/auth/reset-password", { method: "POST", body: JSON.stringify({ token, newPassword: password }) }),
    onSuccess: () => {
      setNotice("Password saved. Sign in with your email and new password.");
      router.replace("/login");
    },
  });

  const error = preview.error ?? activate.error;
  const mismatch = confirm.length > 0 && password !== confirm;

  return (
    <main className="mx-auto flex min-h-screen max-w-md flex-col justify-center px-6">
      <p className="text-sm font-semibold tracking-[0.16em] text-accent uppercase">LifeFit AI</p>
      <h1 className="mt-3 text-3xl font-semibold">Activate your account</h1>
      <p className="mt-2 text-muted">Set a password for this invitation. The link works once and then expires.</p>
      {!token ? <p className="mt-6" role="alert">This activation link is missing a token.</p> : null}
      {preview.data ? (
        <div className="mt-6 rounded-2xl border border-line bg-surface p-4 text-sm">
          <p>{preview.data.fullName}</p>
          <p className="text-muted">{preview.data.email}</p>
          <p className="mt-2 text-muted">Expires {new Date(preview.data.expiresAt).toLocaleString()}</p>
        </div>
      ) : null}
      {token && !preview.data ? (
        <button className="mt-6 min-h-11 rounded-full border border-line px-4" disabled={preview.isPending} onClick={() => preview.mutate()} type="button">
          {preview.isPending ? "Checking link..." : "Check activation link"}
        </button>
      ) : null}
      {preview.data ? (
        <form
          className="mt-6 grid gap-4"
          onSubmit={(event) => {
            event.preventDefault();
            if (!mismatch) activate.mutate();
          }}
        >
          <label className="grid gap-2">
            Password
            <input className="field" onChange={(event) => setPassword(event.target.value)} type="password" value={password} />
          </label>
          <label className="grid gap-2">
            Confirm password
            <input className="field" onChange={(event) => setConfirm(event.target.value)} type="password" value={confirm} />
          </label>
          {mismatch ? <p role="alert">Passwords do not match.</p> : null}
          <button className="min-h-11 rounded-full bg-accent font-semibold text-background disabled:opacity-60" disabled={activate.isPending || mismatch || password.length === 0} type="submit">
            {activate.isPending ? "Saving..." : "Activate account"}
          </button>
        </form>
      ) : null}
      {error instanceof ApiError ? <p className="mt-4" role="alert">{error.message}</p> : null}
      {notice ? <p className="mt-4" role="status">{notice}</p> : null}
    </main>
  );
}

export default function ActivatePage() {
  return (
    <Suspense fallback={<p className="p-8 text-muted">Loading activation.</p>}>
      <ActivateForm />
    </Suspense>
  );
}
