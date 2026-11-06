import { emptyToNull, type GymMemberDetails } from "../../../lib/gym";

export const memberGenders = ["Female", "Male", "NonBinary", "PreferNotToSay"] as const;
export const fitnessGoals = ["FatLoss", "MuscleGain", "Strength", "Endurance", "GeneralFitness", "Mobility", "WeightMaintenance"] as const;
export const memberStatuses = ["Active", "Inactive"] as const;

const phonePattern = /^\+?[0-9][0-9 \-]{6,30}$/;

export type MemberDraft = {
  fullName: string;
  phone: string;
  email: string;
  dateOfBirth: string;
  gender: string;
  address: string;
  city: string;
  state: string;
  postalCode: string;
  emergencyName: string;
  emergencyPhone: string;
  emergencyRelationship: string;
  joinDate: string;
  status: (typeof memberStatuses)[number];
  fitnessGoal: string;
  trainerId: string;
  planId: string;
  ptPackageId: string;
  branchId: string;
  startDate: string;
  enableBasicAppAccess: boolean;
  assignAppAccess: boolean;
  appSubscriptionId: string;
  paymentAmount: string;
  paymentMethod: string;
  paymentDate: string;
  paymentReference: string;
  paymentNotes: string;
};

export const memberPaymentMethods = ["Cash", "Upi", "DebitCard", "CreditCard", "BankTransfer", "OnlinePayment", "Cheque", "Other"] as const;

const referenceMethods = new Set(["Upi", "DebitCard", "CreditCard", "BankTransfer", "OnlinePayment", "Cheque"]);

export function emptyDraft(today: string): MemberDraft {
  return {
    fullName: "",
    phone: "",
    email: "",
    dateOfBirth: "",
    gender: "",
    address: "",
    city: "",
    state: "",
    postalCode: "",
    emergencyName: "",
    emergencyPhone: "",
    emergencyRelationship: "",
    joinDate: today,
    status: "Active",
    fitnessGoal: "",
    trainerId: "",
    planId: "",
    ptPackageId: "",
    branchId: "",
    startDate: today,
    enableBasicAppAccess: false,
    assignAppAccess: false,
    appSubscriptionId: "",
    paymentAmount: "",
    paymentMethod: "Cash",
    paymentDate: today,
    paymentReference: "",
    paymentNotes: "",
  };
}

export function splitFullName(fullName: string): { firstName: string; lastName: string } | null {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  if (parts.length < 2) {
    return null;
  }
  return { firstName: parts.slice(0, -1).join(" "), lastName: parts.at(-1) ?? "" };
}

export function stepError(step: number, draft: MemberDraft): string | null {
  if (step === 1) {
    if (!splitFullName(draft.fullName)) {
      return "Enter a first and last name.";
    }
    if (!phonePattern.test(draft.phone.trim())) {
      return "Enter a mobile number.";
    }
    if (draft.email.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(draft.email.trim())) {
      return "Enter a valid email.";
    }
    if (draft.dateOfBirth && draft.dateOfBirth > new Date().toISOString().slice(0, 10)) {
      return "Date of birth cannot be in the future.";
    }
  }
  if (step === 2 && draft.emergencyPhone.trim() && !phonePattern.test(draft.emergencyPhone.trim())) {
    return "Enter a valid emergency mobile number.";
  }
  if (step === 3) {
    if (!draft.joinDate) {
      return "Enter a joining date.";
    }
    if ((draft.enableBasicAppAccess || draft.assignAppAccess) && !draft.email.trim()) {
      return "Enter an email for LifeFit app login.";
    }
    if (draft.assignAppAccess && !draft.appSubscriptionId) {
      return "Select a LifeFit app plan.";
    }
  }
  if (step === 4) {
    if (draft.planId && !draft.startDate) {
      return "Enter a membership start date.";
    }
    if (draft.paymentAmount.trim() && !draft.planId) {
      return "Select a membership before recording a payment.";
    }
    if (draft.planId && draft.paymentAmount.trim()) {
      const amount = Number(draft.paymentAmount);
      if (!Number.isFinite(amount) || amount <= 0) {
        return "Enter a payment amount greater than zero.";
      }
      if (!draft.paymentDate) {
        return "Enter a payment date when recording a payment.";
      }
      if (referenceMethods.has(draft.paymentMethod) && !draft.paymentReference.trim()) {
        return "Enter a reference number for this payment method.";
      }
    }
  }
  return null;
}

export function draftFromMember(member: GymMemberDetails): MemberDraft {
  const draft = emptyDraft(member.joinDate);
  draft.fullName = `${member.firstName} ${member.lastName}`.trim();
  draft.phone = member.phone || member.mobile || "";
  draft.email = member.email ?? "";
  draft.dateOfBirth = member.dateOfBirth ?? "";
  draft.gender = member.gender ?? "";
  draft.address = member.address ?? "";
  draft.city = member.city ?? "";
  draft.state = member.state ?? "";
  draft.postalCode = member.postalCode ?? "";
  draft.emergencyName = member.emergencyContactName ?? "";
  draft.emergencyPhone = member.emergencyContactPhone ?? "";
  draft.emergencyRelationship = member.emergencyContactRelationship ?? "";
  draft.joinDate = member.joinDate;
  draft.status = member.status === "Inactive" ? "Inactive" : "Active";
  draft.fitnessGoal = member.fitnessGoal ?? "";
  draft.trainerId = member.assignedTrainerId ?? "";
  return draft;
}

export function memberUpdateBody(draft: MemberDraft) {
  const name = splitFullName(draft.fullName);
  return {
    firstName: name?.firstName ?? "",
    lastName: name?.lastName ?? "",
    phone: emptyToNull(draft.phone),
    dateOfBirth: emptyToNull(draft.dateOfBirth),
    gender: emptyToNull(draft.gender),
    emergencyContactName: emptyToNull(draft.emergencyName),
    emergencyContactPhone: emptyToNull(draft.emergencyPhone),
    emergencyContactRelationship: emptyToNull(draft.emergencyRelationship),
    address: emptyToNull(draft.address),
    city: emptyToNull(draft.city),
    state: emptyToNull(draft.state),
    postalCode: emptyToNull(draft.postalCode),
    joinDate: draft.joinDate || null,
    fitnessGoal: emptyToNull(draft.fitnessGoal),
    assignedTrainerId: draft.trainerId || null,
    status: draft.status,
  };
}

export function editError(draft: MemberDraft): string | null {
  const basic = stepError(1, draft) ?? stepError(2, draft);
  if (basic) {
    return basic;
  }
  if (!draft.joinDate) {
    return "Enter a joining date.";
  }
  if (draft.joinDate > new Date().toISOString().slice(0, 10)) {
    return "Joining date cannot be in the future.";
  }
  return null;
}

export function availablePtSessions(included: number, standaloneSessions: number | null): { included: number; standalone: number; total: number } {
  const membershipSessions = Math.max(0, included);
  const packageSessions = standaloneSessions ?? 0;
  return { included: membershipSessions, standalone: packageSessions, total: membershipSessions + packageSessions };
}
