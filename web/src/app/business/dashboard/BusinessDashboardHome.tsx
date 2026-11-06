"use client";

import Link from "next/link";
import { MembershipSalesChart } from "../../../components/MembershipSalesChart";
import type { AnalyticsPreset, DashboardRange } from "../../../lib/dashboardAnalytics";
import {
  attentionLinks,
  quickActions,
  type DashboardBoard,
} from "../../../lib/dashboardBoard";
import { count, moneyOrDash, RangeFilter, Skeleton, timeLabel } from "./DashboardParts";
import { RevenueChart, revenueEmpty } from "./RevenueChart";

type BoardState = {
  data?: DashboardBoard;
  isLoading: boolean;
  isError: boolean;
  error?: string | null;
  refetch: () => void;
};

export function BusinessDashboardHome(props: {
  greeting: string;
  workspace: string;
  dateLabel: string;
  tenantId: string;
  currentDate: string;
  membershipsEnabled: boolean;
  range: DashboardRange;
  preset: AnalyticsPreset;
  customFrom: string;
  customTo: string;
  rangeInvalid: boolean;
  board: BoardState;
  onPreset: (preset: AnalyticsPreset) => void;
  onCustom: (from: string, to: string) => void;
}) {
  const board = props.board.data;
  const currency = board?.currency ?? "USD";
  return (
    <div className="grid gap-5">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-sm text-muted">{props.dateLabel}</p>
          <h1 className="mt-1 text-3xl font-semibold tracking-tight">{props.greeting}</h1>
          <p className="mt-1 text-sm text-muted">{props.workspace}</p>
        </div>
        <RangeFilter
          preset={props.preset}
          from={props.customFrom}
          to={props.customTo}
          invalid={props.rangeInvalid}
          onPreset={props.onPreset}
          onCustom={props.onCustom}
        />
      </header>

      <KpiCards board={props.board} currency={currency} />

      <section className="grid items-start gap-4 xl:grid-cols-[minmax(0,1.7fr)_minmax(18rem,1fr)]">
        <article className="rounded-2xl border border-line bg-surface p-5 shadow-sm">
          <h2 className="text-lg font-semibold">Revenue Trend</h2>
          <p className="text-sm text-muted">Confirmed payments · {props.range.from} to {props.range.to}</p>
          <RevenueBody board={props.board} currency={currency} />
        </article>
        {props.membershipsEnabled ? (
          <MembershipSalesChart currentDate={props.currentDate} range={props.range} tenantId={props.tenantId} />
        ) : (
          <article className="rounded-2xl border border-line bg-surface p-5 shadow-sm">
            <h2 className="text-lg font-semibold">Membership sales</h2>
            <p className="mt-4 text-muted">Membership management is not enabled for this workspace.</p>
          </article>
        )}
      </section>

      <section className="grid items-start gap-4 lg:grid-cols-2">
        <NeedsAttention board={props.board} />
        <TodaysOperations board={props.board} />
      </section>

      <QuickActions board={board} />
    </div>
  );
}

function KpiCards({ board, currency }: { board: BoardState; currency: string }) {
  if (board.isError && !board.data) {
    return (
      <section className="rounded-2xl border border-line bg-surface px-5 py-4 shadow-sm">
        <p className="text-sm text-muted">{board.error ?? "Unable to load dashboard metrics."}</p>
        <button className="mt-3 min-h-11 rounded-full border border-line px-4" onClick={() => board.refetch()} type="button">Try again</button>
      </section>
    );
  }

  const data = board.data;
  const cards = [
    { label: "Active Members", value: count(data?.activeMembers), href: "/business/members" },
    { label: "Payments Collected", value: moneyOrDash(data?.paymentsCollected, currency), href: "/business/ledger" },
    { label: "Today's Check-ins", value: count(data?.checkInsToday), href: "/business/members" },
    { label: "Memberships Expiring Soon", value: count(data?.expiringSoon), href: "/business/memberships" },
  ];
  return (
    <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      {cards.map((card) => (
        <Link
          key={card.label}
          href={card.href}
          className="rounded-2xl border border-line bg-surface px-5 py-4 shadow-sm transition hover:border-accent"
        >
          <p className="text-sm text-muted">{card.label}</p>
          {board.isLoading ? <span className="mt-3 block h-7 w-20 animate-pulse rounded bg-background" /> : <p className="mt-2 text-2xl font-semibold">{card.value}</p>}
        </Link>
      ))}
    </section>
  );
}

function RevenueBody({ board, currency }: { board: BoardState; currency: string }) {
  if (board.isLoading) {
    return <div className="mt-4 h-40 animate-pulse rounded-xl bg-background" />;
  }
  if (board.isError) {
    return (
      <div className="mt-4">
        <p className="text-muted">{board.error ?? "Unable to load revenue."}</p>
        <button className="mt-3 min-h-11 rounded-full border border-line px-4" onClick={() => board.refetch()} type="button">Try again</button>
      </div>
    );
  }
  if (!board.data?.revenue) {
    return <p className="mt-6 text-muted">Payments are not enabled for this workspace.</p>;
  }
  if (revenueEmpty(board.data.revenue)) {
    return <p className="mt-6 text-muted">No confirmed payments in this period.</p>;
  }
  return <RevenueChart points={board.data.revenue} currency={currency} />;
}

function NeedsAttention({ board }: { board: BoardState }) {
  const links = board.data ? attentionLinks(board.data) : [];
  return (
    <article className="rounded-2xl border border-line bg-surface p-5 shadow-sm">
      <h2 className="text-lg font-semibold">Needs Attention</h2>
      {board.isLoading ? <Skeleton /> : board.isError && !board.data ? (
        <div className="mt-4">
          <p className="text-muted">{board.error ?? "Unable to load attention items."}</p>
          <button className="mt-3 min-h-11 rounded-full border border-line px-4" onClick={() => board.refetch()} type="button">Try again</button>
        </div>
      ) : links.length === 0 ? (
        <p className="mt-4 text-muted">Nothing needs attention right now.</p>
      ) : (
        <ul className="mt-3 grid gap-2">
          {links.map((item) => (
            <li key={item.label}>
              <Link href={item.href} className="flex items-center justify-between gap-3 rounded-xl border border-line px-4 py-3 hover:border-accent">
                <span>
                  <span className="block text-sm font-medium">{item.label}</span>
                  <span className="text-xs text-muted">{item.detail}</span>
                </span>
                <span className="text-lg font-semibold">{item.count}</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </article>
  );
}

function TodaysOperations({ board }: { board: BoardState }) {
  const data = board.data;
  const sessions = data?.sessions ?? [];
  const activities = data?.activities ?? [];
  const alerts = data?.alerts ?? [];
  const empty = sessions.length === 0 && activities.length === 0 && alerts.length === 0;
  return (
    <article className="rounded-2xl border border-line bg-surface p-5 shadow-sm">
      <h2 className="text-lg font-semibold">Today&apos;s Operations</h2>
      {board.isLoading ? <Skeleton /> : board.isError && !board.data ? (
        <div className="mt-4">
          <p className="text-muted">{board.error ?? "Unable to load today's operations."}</p>
          <button className="mt-3 min-h-11 rounded-full border border-line px-4" onClick={() => board.refetch()} type="button">Try again</button>
        </div>
      ) : empty ? (
        <p className="mt-4 text-muted">Nothing scheduled needs action today.</p>
      ) : (
        <ul className="mt-3 grid gap-2">
          {alerts.map((alert) => (
            <li className="rounded-xl border border-accent/40 bg-background px-4 py-3" key={`alert-${alert.kind}`}>
              <span className="flex items-center justify-between gap-3">
                <span className="text-sm font-medium">{alert.title}</span>
                <span className="text-lg font-semibold">{alert.count}</span>
              </span>
            </li>
          ))}
          {sessions.map((session) => (
            <li key={`session-${session.id}`}>
              <Link href="/business/members" className="flex items-center justify-between gap-3 rounded-xl border border-line px-4 py-3 hover:border-accent">
                <span>
                  <span className="block text-sm font-medium">PT session · {session.memberName}</span>
                  <span className="text-xs text-muted">{timeLabel(session.startUtc)}</span>
                </span>
              </Link>
            </li>
          ))}
          {activities.map((activity, index) => (
            <li className="rounded-xl border border-line px-4 py-3" key={`activity-${index}`}>
              <span className="block text-sm font-medium">{activity.title}</span>
              <span className="text-xs text-muted">{timeLabel(activity.atUtc)}</span>
            </li>
          ))}
        </ul>
      )}
    </article>
  );
}

function QuickActions({ board }: { board?: DashboardBoard }) {
  const fallback = [
    { href: "/business/members?action=add", label: "Add member" },
    { href: "/business/members", label: "Record payment" },
    { href: "/business/members", label: "Record attendance" },
    { href: "/business/leads?action=add", label: "Add lead" },
  ];
  const actions = board ? quickActions(board) : fallback;
  if (actions.length === 0) {
    return null;
  }
  return (
    <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      {actions.map((action) => (
        <Link
          key={action.label}
          href={action.href}
          className="rounded-2xl border border-line bg-surface px-5 py-4 text-center text-sm font-semibold shadow-sm transition hover:border-accent"
        >
          {action.label}
        </Link>
      ))}
    </section>
  );
}
