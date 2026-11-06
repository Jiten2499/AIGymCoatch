"use client";

import "temporal-polyfill/global";
import FullCalendar from "@fullcalendar/react";
import type {
  ButtonInfo,
  CalendarRef,
  DateClickInfo,
  DatesSetInfo,
  DayCellInfo,
  EventClickInfo,
  EventDisplayInfo,
} from "@fullcalendar/react";
import dayGridPlugin from "@fullcalendar/react/daygrid";
import interactionPlugin from "@fullcalendar/react/interaction";
import listPlugin from "@fullcalendar/react/list";
import classicThemePlugin from "@fullcalendar/react/themes/classic";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  coversDay,
  inclusiveEndFromExclusive,
  leaveStatusClass,
  leaveStatusColors,
  toLeaveCalendarEvents,
  type LeaveView,
  type StaffLeaveListItem,
} from "../../../lib/leaves";

import "@fullcalendar/react/skeleton.css";
import "@fullcalendar/react/themes/classic/theme.css";
import "@fullcalendar/react/themes/classic/palette.css";
import "./leaveCalendar.css";

type Props = {
  items: StaffLeaveListItem[];
  view: LeaveView;
  selectedId: string | null;
  selectedDate: string | null;
  loading: boolean;
  onViewChange: (view: LeaveView) => void;
  onRangeChange: (range: { from: string; to: string }) => void;
  onSelectLeave: (id: string) => void;
  onSelectDate: (day: string) => void;
};

const VIEW_MAP: Record<LeaveView, string> = {
  month: "dayGridMonth",
  week: "dayGridWeek",
  list: "listWeek",
};

export function StaffLeaveCalendar({
  items,
  view,
  selectedId,
  selectedDate,
  loading,
  onViewChange,
  onRangeChange,
  onSelectLeave,
  onSelectDate,
}: Props) {
  const calendarRef = useRef<CalendarRef | null>(null);
  const [narrow, setNarrow] = useState(false);
  const events = useMemo(() => toLeaveCalendarEvents(items), [items]);
  const activeView = narrow ? "list" : view;

  useEffect(() => {
    const media = window.matchMedia("(max-width: 767px)");
    const sync = () => setNarrow(media.matches);
    sync();
    media.addEventListener("change", sync);
    return () => media.removeEventListener("change", sync);
  }, []);

  useEffect(() => {
    const api = calendarRef.current?.getApi();
    if (!api) return;
    const next = VIEW_MAP[activeView];
    if (api.view.type !== next) api.changeView(next);
  }, [activeView]);

  const handleDatesSet = useCallback(
    (info: DatesSetInfo) => {
      const from = info.startStr.slice(0, 10);
      const to = inclusiveEndFromExclusive(info.endStr.slice(0, 10));
      onRangeChange({ from, to });
      if (info.view.type.startsWith("list")) onViewChange("list");
      else if (info.view.type === "dayGridWeek") onViewChange("week");
      else if (info.view.type === "dayGridMonth") onViewChange("month");
    },
    [onRangeChange, onViewChange],
  );

  const handleEventClick = useCallback(
    (info: EventClickInfo) => {
      info.jsEvent.preventDefault();
      onSelectLeave(info.event.id);
      onSelectDate(info.event.startStr.slice(0, 10));
    },
    [onSelectDate, onSelectLeave],
  );

  const handleDateClick = useCallback(
    (info: DateClickInfo) => {
      onSelectDate(info.dateStr.slice(0, 10));
    },
    [onSelectDate],
  );

  const rowEventClass = useCallback(
    (arg: EventDisplayInfo) =>
      joinClasses("lc-event", statusClassOf(arg), arg.event.id === selectedId && "lc-event-selected"),
    [selectedId],
  );

  const listItemEventClass = useCallback(
    (arg: EventDisplayInfo) =>
      joinClasses("lc-list-item", statusClassOf(arg), arg.event.id === selectedId && "lc-list-item-selected"),
    [selectedId],
  );

  const dayCellClass = useCallback(
    (arg: DayCellInfo) =>
      joinClasses(
        "lc-day",
        arg.isOther && "lc-day-other",
        arg.isToday && "lc-day-today",
        localDayKey(arg) === selectedDate && "lc-day-selected",
      ),
    [selectedDate],
  );

  const selectedCount = selectedDate ? items.filter((item) => coversDay(item, selectedDate)).length : 0;

  return (
    <div
      data-color-scheme="dark"
      aria-busy={loading}
      className="leave-calendar relative rounded-2xl border border-line bg-surface p-3 sm:p-5"
    >
      {loading ? (
        <div className="absolute inset-x-0 top-0 h-0.5 overflow-hidden rounded-t-2xl" role="status" aria-label="Refreshing leave">
          <div className="h-full w-1/3 animate-pulse rounded-full bg-accent" />
        </div>
      ) : null}
      {!loading && items.length === 0 ? (
        <p className="mb-4 rounded-xl border border-dashed border-line bg-background/40 px-4 py-3 text-center text-sm text-muted">
          No leave in this range. Adjust filters or move to another period.
        </p>
      ) : null}
      <FullCalendar
        ref={calendarRef}
        plugins={[classicThemePlugin, dayGridPlugin, listPlugin, interactionPlugin]}
        initialView={VIEW_MAP[activeView]}
        headerToolbar={{
          left: "prev,next today",
          center: "title",
          right: narrow ? "" : "dayGridMonth,dayGridWeek,listWeek",
        }}
        todayText="Today"
        monthText="Month"
        weekTextShort="Week"
        listText="List"
        height="auto"
        firstDay={1}
        navLinks
        dayMaxEvents={3}
        moreLinkClick="popover"
        events={events}
        eventDisplay="block"
        datesSet={handleDatesSet}
        eventClick={handleEventClick}
        dateClick={handleDateClick}
        viewClass={(info) =>
          joinClasses("transition-opacity", info.view.type === "dayGridWeek" && "lc-view-week", loading && "opacity-60")
        }
        toolbarClass="lc-toolbar"
        toolbarSectionClass={(info) => `lc-toolbar-${info.name}`}
        toolbarTitleClass="lc-title"
        buttonGroupClass="lc-btn-group"
        buttonClass={buttonClass}
        tableClass="lc-table"
        dayHeaderClass="lc-day-header"
        dayHeaderInnerClass="lc-day-header-inner"
        dayRowClass="lc-day-row"
        dayCellClass={dayCellClass}
        dayCellTopInnerClass="lc-day-number"
        rowEventClass={rowEventClass}
        listItemEventClass={listItemEventClass}
        listDayHeaderClass="lc-list-day-header"
        listDayHeaderInnerClass="lc-list-day-header-inner"
        moreLinkClass="lc-more"
        popoverClass="lc-popover"
        noEventsClass="lc-no-events"
        eventContent={renderEventContent}
        noEventsContent="No leave events to show."
      />
      <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-line pt-4">
        <ul className="flex flex-wrap items-center gap-x-4 gap-y-2 text-xs text-muted" aria-label="Leave status colours">
          {LEGEND.map((status) => {
            const colors = leaveStatusColors(status);
            return (
              <li key={status} className="flex items-center gap-2">
                <span
                  className="size-2.5 rounded-full border"
                  style={{ backgroundColor: colors.borderColor, borderColor: colors.borderColor }}
                  aria-hidden
                />
                {status}
              </li>
            );
          })}
        </ul>
        {selectedDate ? (
          <p className="text-xs text-muted">
            <span className="font-semibold text-foreground">{selectedCount}</span> leave request{selectedCount === 1 ? "" : "s"} on{" "}
            <span className="text-foreground">{selectedDate}</span>
          </p>
        ) : null}
      </div>
    </div>
  );
}

const LEGEND = ["Approved", "Pending", "Rejected", "Cancelled"] as const;

function buttonClass(info: ButtonInfo): string {
  return joinClasses("lc-btn", info.isIconOnly && "lc-btn-icon", info.isSelected && "lc-btn-selected");
}

function statusClassOf(info: EventDisplayInfo): string {
  const leave = (info.event.extendedProps as { leave?: StaffLeaveListItem }).leave;
  return leaveStatusClass(leave?.status ?? "");
}

function joinClasses(...parts: (string | false | null | undefined)[]): string {
  return parts.filter(Boolean).join(" ");
}

function renderEventContent(info: EventDisplayInfo) {
  const leave = (info.event.extendedProps as { leave?: StaffLeaveListItem }).leave;
  if (!leave) return <span className="truncate font-semibold">{info.event.title}</span>;
  return (
    <div className="flex min-w-0 items-center gap-1.5 px-1 leading-tight">
      <span className="lc-event-name truncate font-semibold">{leave.staffName}</span>
      <span className="shrink-0 truncate font-normal opacity-75">{leave.leaveType}</span>
    </div>
  );
}

function localDayKey(info: DayCellInfo): string {
  const date = info.date as Date | { year: number; month: number; day: number; toString(): string };
  if (date instanceof Date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
  }

  if (typeof date.year === "number" && typeof date.month === "number" && typeof date.day === "number") {
    return `${date.year}-${String(date.month).padStart(2, "0")}-${String(date.day).padStart(2, "0")}`;
  }

  return String(date).slice(0, 10);
}
