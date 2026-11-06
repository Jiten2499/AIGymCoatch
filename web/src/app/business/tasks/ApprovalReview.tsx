"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { FormActions, FormDialog, FormField } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import { approvalStamp, type ApprovalRequestDetails } from "../../../lib/approvals";

export function ApprovalReview({ id, tenantId, onClose }: { id: string; tenantId: string | null; onClose: () => void }) {
  const client = useQueryClient();
  const [comment, setComment] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const request = useQuery({
    queryKey: ["approval-request", tenantId, id],
    enabled: tenantId !== null,
    queryFn: () => api<ApprovalRequestDetails>(`/approval-requests/${id}`),
  });
  const decide = useMutation({
    mutationFn: (approve: boolean) => api<ApprovalRequestDetails>(`/approval-requests/${id}/${approve ? "approve" : "reject"}`, {
      method: "POST",
      body: JSON.stringify({ comment: comment.trim() || null }),
    }),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ["approval-requests", tenantId] });
      await client.invalidateQueries({ queryKey: ["approval-count", tenantId] });
      await client.invalidateQueries({ queryKey: ["approval-request", tenantId, id] });
      await client.invalidateQueries({ queryKey: ["memberships", tenantId] });
      await client.invalidateQueries({ queryKey: ["staff-leaves", tenantId] });
      await client.invalidateQueries({ queryKey: ["staff-leave", tenantId] });
      onClose();
    },
  });
  const item = request.data;
  const pending = item?.status === "Pending";
  const isLeave = item?.requestType === "StaffLeave";

  return (
    <FormDialog title={item?.title ?? "Approval request"} onCancel={onClose} wide>
      {request.isLoading ? <p role="status">Loading approval request.</p> : null}
      {request.isError ? <p role="alert">{request.error instanceof ApiError ? request.error.message : "The approval request could not be loaded."}</p> : null}
      {item ? (
        <div className="mt-4 grid gap-3 text-sm">
          <p>{isLeave ? "Staff" : "Member"}: {item.relatedLabel}</p>
          <p>Requested by: {item.requestedByName}</p>
          <p>Requested: {approvalStamp(item.requestedAt)}</p>
          <p>Status: {item.status}</p>
          <p>{item.summary}</p>
          {item.days ? <p>Days: {item.days}</p> : null}
          {item.originalEndDate ? <p>{isLeave ? "Start" : "Original end"}: {item.originalEndDate}</p> : null}
          {item.proposedEndDate ? <p>{isLeave ? "End" : "Proposed end"}: {item.proposedEndDate}</p> : null}
          {item.amount != null ? <p>Amount: {item.currency} {item.amount}</p> : null}
          <p>Reason: {item.reason || "None"}</p>
          {isLeave ? <p className="text-muted">Leave decisions do not change payroll unless a separate leave/payroll policy is configured.</p> : null}
          {item.decisionComment ? <p>Decision: {item.decisionComment}</p> : null}
          <h3 className="font-semibold">History</h3>
          {item.history.map((entry) => (
            <p key={`${entry.action}-${entry.at}`}>{entry.action} · {approvalStamp(entry.at)} · {entry.actorName}{entry.comment ? ` · ${entry.comment}` : ""}</p>
          ))}
          {pending ? (
            <form
              className="grid gap-3"
              onSubmit={(event) => {
                event.preventDefault();
                const approve = (event.nativeEvent as SubmitEvent).submitter?.getAttribute("data-decision") !== "reject";
                if (!approve && comment.trim().length === 0) {
                  setMessage("A reason is required to reject this request.");
                  return;
                }
                setMessage(null);
                decide.mutate(approve);
              }}
            >
              <FormField label="Comment">
                <textarea className="field min-h-20 py-2" maxLength={500} onChange={(event) => setComment(event.target.value)} value={comment} />
              </FormField>
              {message ? <p role="alert">{message}</p> : null}
              {decide.error instanceof ApiError ? <p role="alert">{decide.error.message}</p> : null}
              <div className="flex justify-end gap-3">
                <button className="min-h-11 rounded-full border border-line px-4" data-decision="reject" disabled={decide.isPending} type="submit">Reject</button>
                <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" data-decision="approve" disabled={decide.isPending} type="submit">Approve</button>
              </div>
            </form>
          ) : <FormActions cancel={onClose} pending={false} submitLabel="Close" />}
        </div>
      ) : null}
    </FormDialog>
  );
}
