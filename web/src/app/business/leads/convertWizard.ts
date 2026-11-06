import { fitnessGoals, memberGenders, splitFullName } from "../members/memberWizard";

const phonePattern = /^\+?[0-9][0-9 \-]{6,30}$/;

export type ConvertDraft = {
  fullName: string;
  phone: string;
  email: string;
  source: string;
  notes: string;
  dateOfBirth: string;
  gender: string;
  address: string;
  emergencyName: string;
  emergencyPhone: string;
  joinDate: string;
  fitnessGoal: string;
  trainerId: string;
  memberProfileId: string | null;
  createNew: boolean;
  planId: string;
  branchId: string;
  startDate: string;
  enableBasicAppAccess: boolean;
  assignAppAccess: boolean;
  appSubscriptionId: string;
};

export type MemberMatch = {
  id: string;
  fullName: string;
  phone: string | null;
  email: string | null;
  memberCode: string;
  status: string;
  joinDate: string;
};

export { fitnessGoals, memberGenders, splitFullName };

export function draftFromLead(lead: { fullName: string; mobile: string; email: string | null; source: string; notes: string | null }, today: string): ConvertDraft {
  return {
    fullName: lead.fullName,
    phone: lead.mobile,
    email: lead.email ?? "",
    source: lead.source,
    notes: lead.notes ?? "",
    dateOfBirth: "",
    gender: "",
    address: "",
    emergencyName: "",
    emergencyPhone: "",
    joinDate: today,
    fitnessGoal: "",
    trainerId: "",
    memberProfileId: null,
    createNew: true,
    planId: "",
    branchId: "",
    startDate: today,
    enableBasicAppAccess: false,
    assignAppAccess: false,
    appSubscriptionId: "",
  };
}

export function convertStepError(step: number, draft: ConvertDraft): string | null {
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
    if (!draft.joinDate) {
      return "Enter a joining date.";
    }
    if (draft.emergencyPhone.trim() && !phonePattern.test(draft.emergencyPhone.trim())) {
      return "Enter a valid emergency mobile number.";
    }
  }
  if (step === 2 && !draft.createNew && !draft.memberProfileId) {
    return "Choose an existing member or create a new one.";
  }
  if (step === 3) {
    if (draft.planId && !draft.startDate) {
      return "Enter a membership start date.";
    }
    if ((draft.enableBasicAppAccess || draft.assignAppAccess) && !draft.email.trim()) {
      return "Enter an email for LifeFit app login.";
    }
    if (draft.assignAppAccess && !draft.appSubscriptionId) {
      return "Select a LifeFit app plan.";
    }
  }
  return null;
}
