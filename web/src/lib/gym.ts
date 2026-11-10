export type GymMember = {
  id: string;
  fullName: string;
  mobile: string | null;
  email: string | null;
  hasPhoto: boolean;
  status: string;
  joinDate: string;
};

export type GymMemberDetails = GymMember & {
  email: string;
  firstName: string;
  lastName: string;
  phone: string | null;
  memberCode: string;
  userId?: string | null;
  dateOfBirth?: string | null;
  gender?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  emergencyContactName?: string | null;
  emergencyContactPhone?: string | null;
  emergencyContactRelationship?: string | null;
  fitnessGoal?: string | null;
  assignedTrainerId?: string | null;
  assignedTrainerName?: string | null;
  temporaryPassword?: string | null;
};

export type MembershipPackage = {
  id: string;
  name: string;
  description: string | null;
  durationDays: number;
  membershipType: string;
  price: number;
  currency: string;
  isActive: boolean;
  category: string;
  durationUnit: string;
  durationCount: number;
  includedPtSessions: number;
};

export type GymMembership = {
  id: string;
  memberProfileId: string;
  branchId: string | null;
  membershipPlanId: string;
  membershipNumber: string;
  startDate: string;
  endDate: string;
  status: string;
  memberName: string | null;
  planName: string | null;
  packagePrice?: number;
  discountAmount?: number;
  finalAmount?: number;
  currency?: string;
  totalPaid?: number;
  outstanding?: number;
  settlementStatus?: string;
  approvalRequestId?: string | null;
  includedPtSessions?: number;
  ptSessionsRemaining?: number;
};

export type LeadRow = {
  id: string;
  fullName: string;
  mobile: string;
  source: string;
  status: string;
  interestedPackage: string | null;
  assignedTo: string | null;
  nextFollowUpAt: string | null;
};

export type LeadDetails = LeadRow & {
  email: string | null;
  notes: string | null;
  lostReason: string | null;
  convertedMemberId?: string | null;
  convertedAt?: string | null;
  convertedBy?: string | null;
};

export type ExpenseRow = {
  id: string;
  category: string;
  description: string;
  amount: number;
  currency: string;
  expenseDate: string;
  paymentMethod: string;
  recordedBy: string | null;
  status: string;
};

export type ExpenseCategory = { id: string; name: string; description: string | null; isActive: boolean };

export type ExpenseSummary = {
  totalAmount: number;
  totalCount: number;
  todayAmount: number;
  monthAmount: number;
  currency: string;
  categories: { categoryId: string; name: string; amount: number; count: number }[];
};

export type FinancialAccount = {
  id: string;
  name: string;
  accountKind: string;
  currency: string;
  isActive: boolean;
  balance: number;
};

export type LedgerTransaction = {
  id: string;
  entryDate: string;
  description: string;
  entryType: string;
  accountName: string | null;
  category: string | null;
  amount: number;
  currency: string;
  status: string;
  recordedBy: string | null;
  sourceType: string;
  sourceId: string | null;
};

export type LedgerSummary = {
  moneyIn: number;
  moneyOut: number;
  netCashFlow: number;
  transactionCount: number;
  currency: string;
  accounts: { accountId: string; accountName: string; moneyIn: number; moneyOut: number; balance: number }[];
  categories: { category: string; moneyIn: number; moneyOut: number; count: number }[];
};

export type BranchOption = { id: string; name: string };

export type CommunicationType = "Announcement" | "Circular" | "Offer" | "Event" | "Alert" | "General";
export type CommunicationPriority = "Low" | "Normal" | "High";
export type CommunicationStatus = "Draft" | "Scheduled" | "Published" | "Archived";
export type CommunicationAudienceType =
  | "AllMembers"
  | "ActiveMembers"
  | "ExpiringMembers"
  | "MembershipPackage"
  | "TrainerMembers"
  | "SelectedMembers"
  | "AllTrainers"
  | "AllStaff"
  | "AllBusinessUsers"
  | "SelectedTrainers"
  | "SelectedStaff";

export type AudiencePreview = {
  members: number;
  trainers: number;
  staff: number;
  businessUsers: number;
  total: number;
};

export type InboxCommunication = {
  id: string;
  type: CommunicationType;
  title: string;
  message: string;
  priority: CommunicationPriority;
  publishAt: string;
  expiresAt: string | null;
  isRead: boolean;
  imageStorageKey: string | null;
  createdBy?: string | null;
};

export type CommunicationRow = {
  id: string;
  type: CommunicationType;
  title: string;
  priority: CommunicationPriority;
  status: CommunicationStatus;
  audienceType: CommunicationAudienceType;
  publishAt: string | null;
  expiresAt: string | null;
  createdBy: string | null;
  createdAt: string;
};

export type CommunicationDetails = CommunicationRow & {
  message: string;
  audiencePackageId: string | null;
  audienceTrainerId: string | null;
  expiringWithinDays: number | null;
  selectedMemberIds: string[];
  imageStorageKey: string | null;
  createdByUserId: string;
  publishedAt: string | null;
  archivedAt: string | null;
};

export function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}
