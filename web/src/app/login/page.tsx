"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { ApiError, login } from "../../lib/api";
import { enterWorkspace } from "../../lib/home";

const schema = z.object({
  email: z.string().email("Enter a valid email"),
  password: z.string().min(1, "Enter your password"),
});

type FormValues = z.infer<typeof schema>;

export default function LoginPage() {
  const router = useRouter();
  const [serverError, setServerError] = useState<string | null>(null);
  const form = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { email: "", password: "" } });

  const submit = form.handleSubmit(async (values) => {
    setServerError(null);
    try {
      await login(values.email, values.password);
      router.replace(await enterWorkspace());
    } catch (error) {
      setServerError(error instanceof ApiError ? error.message : "Sign-in failed.");
    }
  });

  return (
    <main className="mx-auto flex min-h-screen max-w-md flex-col justify-center px-6">
      <p className="text-sm font-semibold tracking-[0.16em] text-accent uppercase">LifeFit AI</p>
      <h1 className="mt-3 text-3xl font-semibold">Platform sign in</h1>
      <p className="mt-2 text-muted">Your workspace opens after sign-in.</p>
      <form className="mt-8 grid gap-4" onSubmit={submit}>
        <label className="grid gap-2">
          Email
          <input className="min-h-11 rounded-xl border border-line bg-surface px-3" type="email" {...form.register("email")} />
          {form.formState.errors.email ? <span className="text-sm text-red-300">{form.formState.errors.email.message}</span> : null}
        </label>
        <label className="grid gap-2">
          Password
          <input className="min-h-11 rounded-xl border border-line bg-surface px-3" type="password" {...form.register("password")} />
          {form.formState.errors.password ? <span className="text-sm text-red-300">{form.formState.errors.password.message}</span> : null}
        </label>
        {serverError ? <p role="alert">{serverError}</p> : null}
        <button className="min-h-11 rounded-full bg-accent font-semibold text-background disabled:opacity-60" disabled={form.formState.isSubmitting} type="submit">
          {form.formState.isSubmitting ? "Signing in..." : "Sign in"}
        </button>
      </form>
    </main>
  );
}
