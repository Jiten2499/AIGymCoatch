"use client";

import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { FormDialog, FormField } from "../../../components/FormDialog";
import { ApiError } from "../../../lib/api";
import { applyStaffLeave } from "../../../lib/leaves";

export function ApplyLeaveDialog({ onCancel, onSaved }: { onCancel: () => void; onSaved: () => void }) {
  const [leaveType, setLeaveType] = useState("Annual");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [reason, setReason] = useState("");
  const apply = useMutation({
    mutationFn: () => applyStaffLeave({ leaveType, startDate, endDate: endDate || startDate, reason: reason.trim() }),
    onSuccess: () => onSaved(),
  });

  return (
    <FormDialog onCancel={onCancel} title="Apply for leave">
      <p className="text-muted">Submit a leave request for yourself. Approvers review it from Tasks.</p>
      <form
        className="mt-4 grid gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          apply.mutate();
        }}
      >
        <FormField label="Leave type">
          <select aria-label="Leave type" className="field" onChange={(event) => setLeaveType(event.target.value)} required value={leaveType}>
            <option value="Annual">Annual</option>
            <option value="Sick">Sick</option>
            <option value="Casual">Casual</option>
            <option value="Unpaid">Unpaid</option>
            <option value="Other">Other</option>
          </select>
        </FormField>
        <FormField label="Start date">
          <input aria-label="Start date" className="field" onChange={(event) => setStartDate(event.target.value)} required type="date" value={startDate} />
        </FormField>
        <FormField label="End date">
          <input aria-label="End date" className="field" min={startDate || undefined} onChange={(event) => setEndDate(event.target.value)} required type="date" value={endDate} />
        </FormField>
        <FormField label="Reason">
          <textarea aria-label="Reason" className="field min-h-24" maxLength={1000} onChange={(event) => setReason(event.target.value)} required value={reason} />
        </FormField>
        {apply.error instanceof ApiError ? <p role="alert">{apply.error.message}</p> : null}
        <div className="mt-2 flex justify-end gap-3">
          <button className="min-h-11 rounded-full px-4" onClick={onCancel} type="button">Cancel</button>
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={apply.isPending} type="submit">
            {apply.isPending ? "Submitting..." : "Submit leave"}
          </button>
        </div>
      </form>
    </FormDialog>
  );
}
