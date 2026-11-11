"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { api, ApiError } from "../../lib/api";

function ResetPasswordForm() {
  const router = useRouter();
  const params = useSearchParams();
  const token = params.get("token") ?? "";
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const mismatch = confirm.length > 0 && password !== confirm;

  const reset = useMutation({
    mutationFn: () => api("/auth/reset-password", { method: "POST", body: JSON.stringify({ token, newPassword: password }) }),
    onSuccess: () => router.replace("/login"),
  });

  return (
    <main className="mx-auto flex min-h-screen max-w-md flex-col justify-center px-6">
      <p className="text-sm font-semibold tracking-[0.16em] text-accent uppercase">LifeFit AI</p>
      <h1 className="mt-3 text-3xl font-semibold">Choose a new password</h1>
      <p className="mt-2 text-muted">Use the link from your email once. After you save a password, sign in with it.</p>
      {!token ? <p className="mt-6" role="alert">This reset link is missing a token.</p> : null}
      {token ? (
        <form
          className="mt-8 grid gap-4"
          onSubmit={(event) => {
            event.preventDefault();
            if (!mismatch && password.length > 0) {
              reset.mutate();
            }
          }}
        >
          <label className="grid gap-2">
            New password
            <input className="field" onChange={(event) => setPassword(event.target.value)} type="password" value={password} />
          </label>
          <label className="grid gap-2">
            Confirm password
            <input className="field" onChange={(event) => setConfirm(event.target.value)} type="password" value={confirm} />
          </label>
          {mismatch ? <p role="alert">Passwords do not match.</p> : null}
          {reset.error instanceof ApiError ? <p role="alert">{reset.error.message}</p> : null}
          <button className="min-h-11 rounded-full bg-accent font-semibold text-background disabled:opacity-60" disabled={reset.isPending || mismatch || password.length === 0} type="submit">
            {reset.isPending ? "Saving..." : "Save password"}
          </button>
        </form>
      ) : null}
      <Link className="mt-6 text-accent" href="/login">Back to sign in</Link>
    </main>
  );
}

export default function ResetPasswordPage() {
  return (
    <Suspense fallback={<p className="p-8 text-muted">Loading password reset.</p>}>
      <ResetPasswordForm />
    </Suspense>
  );
}
