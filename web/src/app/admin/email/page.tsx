"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState, type ReactNode } from "react";
import { PageState } from "../../../components/AdminShell";
import { DataTable } from "../../../components/DataTable";
import { api, ApiError } from "../../../lib/api";
import type { EmailConfiguration, EmailDeliveryLog, EmailDeliveryPage, EmailTestResult } from "../../../lib/types";

type Tab = "provider" | "sender" | "delivery" | "logs";

type FormState = {
  provider: string;
  smtpHost: string;
  smtpPort: number;
  encryption: string;
  authenticationEnabled: boolean;
  username: string;
  password: string;
  senderName: string;
  senderEmail: string;
  replyToEmail: string;
  connectionTimeoutSeconds: number;
  maxEmailsPerMinute: number;
  maxDeliveryAttempts: number;
  retryBaseDelaySeconds: number;
  isEnabled: boolean;
};

const emptyForm: FormState = {
  provider: "SmtpHostinger",
  smtpHost: "smtp.hostinger.com",
  smtpPort: 465,
  encryption: "SslTls",
  authenticationEnabled: true,
  username: "notifications@mindgridtech.com",
  password: "",
  senderName: "LifeFit AI",
  senderEmail: "notifications@mindgridtech.com",
  replyToEmail: "support@mindgridtech.com",
  connectionTimeoutSeconds: 30,
  maxEmailsPerMinute: 2,
  maxDeliveryAttempts: 5,
  retryBaseDelaySeconds: 60,
  isEnabled: false,
};

function fromConfig(config: EmailConfiguration): FormState {
  return {
    provider: config.provider,
    smtpHost: config.smtpHost,
    smtpPort: config.smtpPort,
    encryption: config.encryption,
    authenticationEnabled: config.authenticationEnabled,
    username: config.username,
    password: "",
    senderName: config.senderName,
    senderEmail: config.senderEmail,
    replyToEmail: config.replyToEmail ?? "",
    connectionTimeoutSeconds: config.connectionTimeoutSeconds,
    maxEmailsPerMinute: config.maxEmailsPerMinute,
    maxDeliveryAttempts: config.maxDeliveryAttempts,
    retryBaseDelaySeconds: config.retryBaseDelaySeconds,
    isEnabled: config.isEnabled,
  };
}

export default function EmailDeliveryPage() {
  const client = useQueryClient();
  const [tab, setTab] = useState<Tab>("provider");
  const [form, setForm] = useState<FormState>(emptyForm);
  const [notice, setNotice] = useState<string | null>(null);
  const [testRecipient, setTestRecipient] = useState("");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("All");
  const [templateKey, setTemplateKey] = useState("All");

  const configuration = useQuery({
    queryKey: ["email-configuration"],
    queryFn: () => api<EmailConfiguration>("/email/configuration"),
  });

  const logs = useQuery({
    queryKey: ["email-delivery-logs", search, status, templateKey],
    enabled: tab === "logs" || tab === "provider",
    queryFn: () => {
      const params = new URLSearchParams({ page: "1", pageSize: "20" });
      if (search.trim()) params.set("search", search.trim());
      if (status !== "All") params.set("status", status);
      if (templateKey !== "All") params.set("templateKey", templateKey);
      return api<EmailDeliveryPage>(`/email/delivery-logs?${params}`);
    },
  });

  useEffect(() => {
    if (configuration.data) {
      setForm(fromConfig(configuration.data));
    }
  }, [configuration.data]);

  const save = useMutation({
    mutationFn: () =>
      api<EmailConfiguration>("/email/configuration", {
        method: "PUT",
        body: JSON.stringify({
          ...form,
          password: form.password.trim() || null,
          replyToEmail: form.replyToEmail.trim() || null,
          activate: true,
        }),
      }),
    onSuccess: async (data) => {
      setForm(fromConfig(data));
      setNotice("Configuration saved. The SMTP password is never returned after saving.");
      await client.invalidateQueries({ queryKey: ["email-configuration"] });
    },
  });

  const toggle = useMutation({
    mutationFn: (isEnabled: boolean) => api<EmailConfiguration>("/email/configuration/enabled", { method: "POST", body: JSON.stringify({ isEnabled }) }),
    onSuccess: async (data) => {
      setForm(fromConfig(data));
      setNotice(data.isEnabled ? "Provider enabled." : "Provider disabled.");
      await client.invalidateQueries({ queryKey: ["email-configuration"] });
    },
  });

  const testConnection = useMutation({
    mutationFn: () => api<EmailTestResult>("/email/configuration/test-connection", { method: "POST" }),
    onSuccess: async (result) => {
      setNotice(result.succeeded ? result.message : `Connection failed: ${result.message}`);
      await client.invalidateQueries({ queryKey: ["email-configuration"] });
    },
  });

  const sendTest = useMutation({
    mutationFn: () => api<EmailTestResult>("/email/configuration/test-email", { method: "POST", body: JSON.stringify({ recipient: testRecipient.trim() }) }),
    onSuccess: async (result) => {
      setNotice(result.message);
      setTestRecipient("");
      await client.invalidateQueries({ queryKey: ["email-delivery-logs"] });
      await client.invalidateQueries({ queryKey: ["email-configuration"] });
    },
  });

  const retry = useMutation({
    mutationFn: (id: string) => api<EmailDeliveryLog>(`/email/delivery-logs/${id}/retry`, { method: "POST" }),
    onSuccess: async () => {
      setNotice("Delivery queued for retry.");
      await client.invalidateQueries({ queryKey: ["email-delivery-logs"] });
    },
  });

  const config = configuration.data;
  const actionError = save.error ?? toggle.error ?? testConnection.error ?? sendTest.error ?? retry.error;
  const tabs: { id: Tab; label: string }[] = [
    { id: "provider", label: "Provider Configuration" },
    { id: "sender", label: "Sender Settings" },
    { id: "delivery", label: "Delivery Settings" },
    { id: "logs", label: "Email Delivery Logs" },
  ];

  return (
    <PageState error={configuration.error instanceof ApiError ? configuration.error.message : null} loading={configuration.isLoading} onRetry={() => void configuration.refetch()}>
      <p className="text-sm text-muted">System Settings &gt; Email &amp; Delivery</p>
      <h1 className="mt-2 text-3xl font-semibold">Email &amp; Delivery</h1>
      <p className="mt-2 max-w-3xl text-muted">Configure and manage the platform email provider for transactional emails (e.g., welcome, invitations, account activation).</p>

      <div className="mt-6 flex flex-wrap gap-2 border-b border-line pb-2">
        {tabs.map((item) => (
          <button
            key={item.id}
            className={`min-h-11 rounded-full px-4 text-sm font-semibold ${tab === item.id ? "bg-accent text-background" : "border border-line text-foreground"}`}
            onClick={() => setTab(item.id)}
            type="button"
          >
            {item.label}
          </button>
        ))}
      </div>

      {notice ? <p className="mt-4" role="status">{notice}</p> : null}
      {actionError instanceof ApiError ? <p className="mt-4" role="alert">{actionError.message}</p> : null}
      {config?.rateLimitWarning && (tab === "provider" || tab === "delivery") ? <p className="mt-4 text-sm text-muted" role="status">{config.rateLimitWarning}</p> : null}

      {tab === "provider" ? (
        <div className="mt-6 grid gap-6 lg:grid-cols-[minmax(0,18rem)_minmax(0,1fr)]">
          <div className="space-y-4">
            <section className="rounded-2xl border border-line bg-surface p-5">
              <h2 className="text-lg font-semibold">Email Provider</h2>
              <label className="mt-4 block text-sm text-muted">
                Provider
                <select className="field mt-2 w-full" onChange={(event) => setForm((current) => ({ ...current, provider: event.target.value }))} value={form.provider}>
                  {(config?.availableProviders ?? []).map((option) => (
                    <option disabled={!option.implemented} key={option.provider} value={option.provider}>
                      {option.displayName}{option.implemented ? "" : " (coming soon)"}
                    </option>
                  ))}
                </select>
              </label>
              <div className="mt-4 rounded-xl border border-line px-4 py-3">
                <div className="flex items-center gap-2">
                  <span className={`h-2.5 w-2.5 rounded-full ${config?.isEnabled ? "bg-accent" : "bg-muted"}`} />
                  <span className="font-medium">{config?.displayName ?? "SMTP (Hostinger)"}</span>
                </div>
                <p className="mt-1 text-sm text-muted">{config?.isEnabled ? "Active" : "Inactive"}</p>
              </div>
            </section>

            <section className="rounded-2xl border border-line bg-surface p-5">
              <h2 className="text-lg font-semibold">Provider Status</h2>
              <label className="mt-4 flex items-center justify-between gap-3 text-sm">
                <span>Enabled</span>
                <input
                  checked={form.isEnabled}
                  onChange={(event) => {
                    const next = event.target.checked;
                    setForm((current) => ({ ...current, isEnabled: next }));
                    if (config?.id) toggle.mutate(next);
                  }}
                  type="checkbox"
                />
              </label>
              <dl className="mt-4 space-y-2 text-sm">
                <div className="flex justify-between gap-3"><dt className="text-muted">Last Test</dt><dd>{formatStamp(config?.lastTestedAt)} {config?.lastTestStatus ? `(${config.lastTestStatus})` : ""}</dd></div>
                <div className="flex justify-between gap-3"><dt className="text-muted">Last Email Sent</dt><dd>{formatStamp(config?.lastEmailSentAt)} {config?.lastEmailStatus ? `(${config.lastEmailStatus})` : ""}</dd></div>
                <div className="flex justify-between gap-3"><dt className="text-muted">Last Updated</dt><dd>{formatStamp(config?.updatedAt)}{config?.updatedByName ? ` By ${config.updatedByName}` : ""}</dd></div>
              </dl>
            </section>
          </div>

          <section className="rounded-2xl border border-line bg-surface p-5">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h2 className="text-lg font-semibold">SMTP Configuration</h2>
                <p className={`mt-2 rounded-xl px-3 py-2 text-sm ${config?.healthStatus === "Healthy" ? "bg-accent/15 text-accent" : "bg-background text-muted"}`}>
                  SMTP Connection — {config?.lastTestMessage ?? config?.healthStatus ?? "Not tested"}
                  {config?.lastTestedAt ? ` (Last tested: ${formatStamp(config.lastTestedAt)})` : ""}
                </p>
              </div>
              <div className="flex flex-wrap gap-2">
                <button className="min-h-11 rounded-full border border-line px-4 disabled:opacity-60" disabled={testConnection.isPending || !config?.id} onClick={() => testConnection.mutate()} type="button">
                  {testConnection.isPending ? "Testing..." : "Test Connection"}
                </button>
                <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={sendTest.isPending || !testRecipient.trim() || !form.isEnabled} onClick={() => sendTest.mutate()} type="button">
                  {sendTest.isPending ? "Sending..." : "Send Test Email"}
                </button>
              </div>
            </div>

            <div className="mt-4 grid gap-4 md:grid-cols-2">
              <Field label="SMTP Host"><input className="field w-full" onChange={(event) => setForm((current) => ({ ...current, smtpHost: event.target.value }))} value={form.smtpHost} /></Field>
              <Field label="SMTP Port"><input className="field w-full" onChange={(event) => setForm((current) => ({ ...current, smtpPort: Number(event.target.value) || 0 }))} type="number" value={form.smtpPort} /></Field>
              <Field label="Username"><input className="field w-full" onChange={(event) => setForm((current) => ({ ...current, username: event.target.value }))} value={form.username} /></Field>
              <Field label="Encryption">
                <select className="field w-full" onChange={(event) => setForm((current) => ({ ...current, encryption: event.target.value }))} value={form.encryption}>
                  <option value="SslTls">SSL / TLS</option>
                  <option value="StartTls">STARTTLS</option>
                </select>
              </Field>
              <Field label="Sender Name"><input className="field w-full" onChange={(event) => setForm((current) => ({ ...current, senderName: event.target.value }))} value={form.senderName} /></Field>
              <Field label="Password">
                <input
                  autoComplete="new-password"
                  className="field w-full"
                  onChange={(event) => setForm((current) => ({ ...current, password: event.target.value }))}
                  placeholder={config?.passwordConfigured ? "•••••••• (leave blank to keep)" : "Enter SMTP password"}
                  type="password"
                  value={form.password}
                />
              </Field>
              <Field label="Reply-To Email"><input className="field w-full" onChange={(event) => setForm((current) => ({ ...current, replyToEmail: event.target.value }))} value={form.replyToEmail} /></Field>
              <Field label="Sender Email"><input className="field w-full" onChange={(event) => setForm((current) => ({ ...current, senderEmail: event.target.value }))} value={form.senderEmail} /></Field>
              <Field label="Test recipient"><input className="field w-full" onChange={(event) => setTestRecipient(event.target.value)} placeholder="you@company.com" type="email" value={testRecipient} /></Field>
            </div>

            <div className="mt-6 flex flex-wrap gap-3">
              <button className="min-h-11 rounded-full border border-line px-4" onClick={() => configuration.data && setForm(fromConfig(configuration.data))} type="button">Reset</button>
              <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={save.isPending} onClick={() => save.mutate()} type="button">
                {save.isPending ? "Saving..." : "Save Changes"}
              </button>
            </div>
          </section>
        </div>
      ) : null}

      {tab === "sender" ? (
        <section className="mt-6 max-w-2xl rounded-2xl border border-line bg-surface p-5">
          <h2 className="text-lg font-semibold">Sender Settings</h2>
          <div className="mt-4 grid gap-4">
            <Field label="Sender Name"><input className="field w-full" onChange={(event) => setForm((current) => ({ ...current, senderName: event.target.value }))} value={form.senderName} /></Field>
            <Field label="Sender Email"><input className="field w-full" onChange={(event) => setForm((current) => ({ ...current, senderEmail: event.target.value }))} value={form.senderEmail} /></Field>
            <Field label="Reply-To Email"><input className="field w-full" onChange={(event) => setForm((current) => ({ ...current, replyToEmail: event.target.value }))} value={form.replyToEmail} /></Field>
          </div>
          <button className="mt-6 min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={save.isPending} onClick={() => save.mutate()} type="button">Save Changes</button>
        </section>
      ) : null}

      {tab === "delivery" ? (
        <section className="mt-6 max-w-2xl rounded-2xl border border-line bg-surface p-5">
          <h2 className="text-lg font-semibold">Delivery Settings</h2>
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            <Field label="Max emails per minute"><input className="field w-full" onChange={(event) => setForm((current) => ({ ...current, maxEmailsPerMinute: Number(event.target.value) || 0 }))} type="number" value={form.maxEmailsPerMinute} /></Field>
            <Field label="Max delivery attempts"><input className="field w-full" onChange={(event) => setForm((current) => ({ ...current, maxDeliveryAttempts: Number(event.target.value) || 0 }))} type="number" value={form.maxDeliveryAttempts} /></Field>
            <Field label="Retry base delay (seconds)"><input className="field w-full" onChange={(event) => setForm((current) => ({ ...current, retryBaseDelaySeconds: Number(event.target.value) || 0 }))} type="number" value={form.retryBaseDelaySeconds} /></Field>
            <Field label="SMTP connection timeout (seconds)"><input className="field w-full" onChange={(event) => setForm((current) => ({ ...current, connectionTimeoutSeconds: Number(event.target.value) || 0 }))} type="number" value={form.connectionTimeoutSeconds} /></Field>
          </div>
          <label className="mt-4 flex items-center gap-3 text-sm">
            <input checked={form.authenticationEnabled} onChange={(event) => setForm((current) => ({ ...current, authenticationEnabled: event.target.checked }))} type="checkbox" />
            Authentication enabled
          </label>
          <button className="mt-6 min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={save.isPending} onClick={() => save.mutate()} type="button">Save Changes</button>
        </section>
      ) : null}

      {tab === "logs" || tab === "provider" ? (
        <section className="mt-6 rounded-2xl border border-line bg-surface p-5">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <div>
              <h2 className="text-lg font-semibold">Recent Email Delivery Logs</h2>
              <p className="mt-1 text-sm text-muted">SMTP acceptance is shown as Accepted. Confirmed delivery events appear only when the provider supplies them.</p>
            </div>
            <div className="flex flex-wrap gap-2">
              <input className="field" onChange={(event) => setSearch(event.target.value)} placeholder="Search recipient or subject" value={search} />
              <select className="field" onChange={(event) => setStatus(event.target.value)} value={status}>
                {["All", "Queued", "Processing", "Accepted", "Retrying", "Failed"].map((item) => <option key={item} value={item}>{item === "All" ? "All Status" : item}</option>)}
              </select>
              <select className="field" onChange={(event) => setTemplateKey(event.target.value)} value={templateKey}>
                {["All", "Account.Invitation.Staff", "Account.PasswordReset", "System.TestEmail"].map((item) => <option key={item} value={item}>{item === "All" ? "All Templates" : item}</option>)}
              </select>
            </div>
          </div>
          <div className="mt-4">
            <DataTable
              columns={[
                { header: "Date & Time", value: (row) => row.queuedAt, cell: (row) => formatStamp(row.sentAt ?? row.queuedAt) },
                { header: "Recipient", cell: (row) => row.recipient },
                { header: "Subject", cell: (row) => row.subject },
                { header: "Template", cell: (row) => row.templateKey },
                { header: "Status", cell: (row) => row.status },
                { header: "Attempts", cell: (row) => String(row.attemptCount) },
                { header: "Message ID", cell: (row) => row.providerMessageId ?? row.failureReason ?? "" },
                {
                  header: "Action",
                  cell: (row) => row.canRetry ? (
                    <button className="text-accent underline disabled:opacity-60" disabled={retry.isPending} onClick={() => retry.mutate(row.id)} type="button">Retry</button>
                  ) : "—",
                },
              ]}
              empty="No delivery logs were returned."
              rowKey={(row) => row.id}
              rows={logs.data?.items ?? []}
            />
          </div>
        </section>
      ) : null}
    </PageState>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block text-sm text-muted">
      {label}
      <div className="mt-2 text-foreground">{children}</div>
    </label>
  );
}

function formatStamp(value: string | null | undefined) {
  if (!value) return "—";
  return new Date(value).toLocaleString();
}
