export type ApprovalRequestItem = {
  id: string;
  requestType: "MembershipFreeze" | "MembershipExtension" | "TrainerRewardAward" | "StaffLeave";
  title: string;
  summary: string;
  requestedByName: string;
  requestedAt: string;
  relatedLabel: string;
  amount: number | null;
  currency: string | null;
  days: number | null;
  status: "Pending" | "Approved" | "Rejected";
};

export type ApprovalHistoryEntry = {
  action: string;
  at: string;
  actorName: string;
  comment: string | null;
};

export type ApprovalRequestDetails = ApprovalRequestItem & {
  reason: string | null;
  memberProfileId: string | null;
  sourceId: string;
  originalEndDate: string | null;
  proposedEndDate: string | null;
  decisionComment: string | null;
  history: ApprovalHistoryEntry[];
};

export function approvalStamp(value: string): string {
  return new Date(value).toLocaleString("en-GB", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "UTC",
  });
}
