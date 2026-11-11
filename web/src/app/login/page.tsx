"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { ApiError, api, login } from "../../lib/api";
import { enterWorkspace } from "../../lib/home";
import { touchActivity } from "../../lib/session";

const loginSchema = z.object({
  email: z.string().email("Enter a valid email"),
  password: z.string().min(1, "Enter your password"),
});

const forgotSchema = z.object({
  email: z.string().email("Enter a valid email"),
});

type LoginValues = z.infer<typeof loginSchema>;
type ForgotValues = z.infer<typeof forgotSchema>;

export default function LoginPage() {
  const router = useRouter();
  const [mode, setMode] = useState<"sign-in" | "forgot">("sign-in");
  const [serverError, setServerError] = useState<string | null>(null);
  const [forgotSent, setForgotSent] = useState(false);
  const loginForm = useForm<LoginValues>({ resolver: zodResolver(loginSchema), defaultValues: { email: "", password: "" } });
  const forgotForm = useForm<ForgotValues>({ resolver: zodResolver(forgotSchema), defaultValues: { email: "" } });

  const submitLogin = loginForm.handleSubmit(async (values) => {
    setServerError(null);
    try {
      await login(values.email, values.password);
      touchActivity();
      router.replace(await enterWorkspace());
    } catch (error) {
      setServerError(error instanceof ApiError ? error.message : "Sign-in failed.");
    }
  });

  const submitForgot = forgotForm.handleSubmit(async (values) => {
    setServerError(null);
    setForgotSent(false);
    try {
      await api("/auth/forgot-password", { method: "POST", body: JSON.stringify({ email: values.email.trim() }) });
      setForgotSent(true);
    } catch (error) {
      setServerError(error instanceof ApiError ? error.message : "The reset email could not be sent.");
    }
  });

  return (
    <main className="mx-auto flex min-h-screen max-w-md flex-col justify-center px-6">
      <p className="text-sm font-semibold tracking-[0.16em] text-accent uppercase">LifeFit AI</p>
      {mode === "sign-in" ? (
        <>
          <h1 className="mt-3 text-3xl font-semibold">Platform sign in</h1>
          <p className="mt-2 text-muted">Your workspace opens after sign-in.</p>
          <form className="mt-8 grid gap-4" onSubmit={submitLogin}>
            <label className="grid gap-2">
              Email
              <input className="min-h-11 rounded-xl border border-line bg-surface px-3" type="email" {...loginForm.register("email")} />
              {loginForm.formState.errors.email ? <span className="text-sm text-red-300">{loginForm.formState.errors.email.message}</span> : null}
            </label>
            <label className="grid gap-2">
              Password
              <input className="min-h-11 rounded-xl border border-line bg-surface px-3" type="password" {...loginForm.register("password")} />
              {loginForm.formState.errors.password ? <span className="text-sm text-red-300">{loginForm.formState.errors.password.message}</span> : null}
            </label>
            {serverError ? <p role="alert">{serverError}</p> : null}
            <button className="min-h-11 rounded-full bg-accent font-semibold text-background disabled:opacity-60" disabled={loginForm.formState.isSubmitting} type="submit">
              {loginForm.formState.isSubmitting ? "Signing in..." : "Sign in"}
            </button>
          </form>
          <button
            className="mt-4 min-h-11 text-left text-accent"
            onClick={() => {
              setMode("forgot");
              setServerError(null);
              setForgotSent(false);
              forgotForm.reset({ email: loginForm.getValues("email") });
            }}
            type="button"
          >
            Forgot password?
          </button>
        </>
      ) : (
        <>
          <h1 className="mt-3 text-3xl font-semibold">Reset your password</h1>
          <p className="mt-2 text-muted">Enter the email registered on your LifeFit AI account. If it matches an active account, a reset link is emailed.</p>
          {forgotSent ? (
            <p className="mt-6 rounded-2xl border border-line bg-surface p-4" role="status">
              If that email is registered, a password reset link is on its way. Check your inbox and spam folder.
            </p>
          ) : (
            <form className="mt-8 grid gap-4" onSubmit={submitForgot}>
              <label className="grid gap-2">
                Email
                <input className="min-h-11 rounded-xl border border-line bg-surface px-3" type="email" {...forgotForm.register("email")} />
                {forgotForm.formState.errors.email ? <span className="text-sm text-red-300">{forgotForm.formState.errors.email.message}</span> : null}
              </label>
              {serverError ? <p role="alert">{serverError}</p> : null}
              <button className="min-h-11 rounded-full bg-accent font-semibold text-background disabled:opacity-60" disabled={forgotForm.formState.isSubmitting} type="submit">
                {forgotForm.formState.isSubmitting ? "Sending..." : "Send password reset link"}
              </button>
            </form>
          )}
          <button
            className="mt-4 min-h-11 text-left text-accent"
            onClick={() => {
              setMode("sign-in");
              setServerError(null);
              setForgotSent(false);
            }}
            type="button"
          >
            Back to sign in
          </button>
          {forgotSent ? <Link className="mt-2 text-sm text-muted" href="/login">Open sign in</Link> : null}
        </>
      )}
    </main>
  );
}
