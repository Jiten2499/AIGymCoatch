export type CoachCertDraft = {
  name: string;
  organization: string;
  issueDate: string;
  expiryDate: string;
};

export type CoachDraft = {
  firstName: string;
  middleName: string;
  lastName: string;
  email: string;
  phone: string;
  dateOfBirth: string;
  gender: string;
  address: string;
  emergencyName: string;
  emergencyPhone: string;
  employeeCode: string;
  designation: string;
  specializations: string[];
  experienceYears: string;
  bio: string;
  certifications: CoachCertDraft[];
  previousWorkplace: string;
  employmentType: string;
  joiningDate: string;
  branchId: string;
  reportingManagerUserId: string;
  employmentStatus: string;
  salaryType: string;
  monthlySalary: string;
  perSessionRate: string;
  commissionPercent: string;
  paymentFrequency: string;
  commissionEligible: boolean;
  subscriptionId: string;
  planName: string;
  sendActivationLink: boolean;
  linkExistingUser: boolean;
};

export const coachDesignations = [
  "Head Coach",
  "Senior Trainer",
  "Personal Trainer",
  "Fitness Coach",
  "Yoga Instructor",
  "Zumba Instructor",
  "Nutrition Coach",
  "Strength & Conditioning Coach",
] as const;

export const coachSpecializations = [
  "Weight Loss",
  "Muscle Building",
  "Strength Training",
  "Bodybuilding",
  "Functional Training",
  "Cardio",
  "Yoga",
  "Rehabilitation",
  "Sports Fitness",
  "Nutrition Guidance",
] as const;

export const coachEmploymentTypes = ["FullTime", "PartTime", "Contract", "Freelance", "VisitingTrainer"] as const;
export const coachGenders = ["", "Female", "Male", "NonBinary", "PreferNotToSay"] as const;
export const salaryTypes = ["FixedMonthly", "PerSession", "CommissionBased", "FixedPlusCommission"] as const;
export const paymentFrequencies = ["Monthly", "BiWeekly", "Weekly", "PerSession"] as const;

export function emptyCoachDraft(today: string): CoachDraft {
  return {
    firstName: "",
    middleName: "",
    lastName: "",
    email: "",
    phone: "",
    dateOfBirth: "",
    gender: "",
    address: "",
    emergencyName: "",
    emergencyPhone: "",
    employeeCode: "",
    designation: "Personal Trainer",
    specializations: [],
    experienceYears: "",
    bio: "",
    certifications: [],
    previousWorkplace: "",
    employmentType: "FullTime",
    joiningDate: today,
    branchId: "",
    reportingManagerUserId: "",
    employmentStatus: "Active",
    salaryType: "FixedMonthly",
    monthlySalary: "",
    perSessionRate: "",
    commissionPercent: "",
    paymentFrequency: "Monthly",
    commissionEligible: false,
    subscriptionId: "",
    planName: "",
    sendActivationLink: false,
    linkExistingUser: false,
  };
}

export function emptyCert(): CoachCertDraft {
  return { name: "", organization: "", issueDate: "", expiryDate: "" };
}

export function isValidPhone(value: string) {
  return /^\+?[0-9][0-9 \-]{6,30}$/.test(value.trim());
}

export function isValidEmail(value: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim());
}

export function resolvedFirstName(draft: CoachDraft) {
  const first = draft.firstName.trim();
  const middle = draft.middleName.trim();
  return middle ? `${first} ${middle}` : first;
}

export function specializationText(draft: CoachDraft) {
  const text = draft.specializations.map((item) => item.trim()).filter(Boolean).join(", ");
  return text.length > 200 ? text.slice(0, 200) : text;
}

export function certificationText(draft: CoachDraft) {
  const lines = draft.certifications
    .map((cert) => {
      const name = cert.name.trim();
      if (!name) return "";
      const org = cert.organization.trim();
      const issued = cert.issueDate.trim();
      const expiry = cert.expiryDate.trim();
      return [name, org ? `Issued by ${org}` : "", issued ? `Issued ${issued}` : "", expiry ? `Expires ${expiry}` : ""]
        .filter(Boolean)
        .join(" · ");
    })
    .filter(Boolean);
  const text = lines.join("; ");
  return text.length > 500 ? text.slice(0, 500) : text;
}

export function employmentNotes(draft: CoachDraft) {
  const workplace = draft.previousWorkplace.trim();
  const rate = draft.perSessionRate.trim();
  const commission = draft.commissionPercent.trim();
  const frequency = draft.paymentFrequency.trim();
  const parts = [
    workplace ? `Previous workplace: ${workplace}` : "",
    draft.salaryType === "PerSession" || draft.salaryType === "FixedPlusCommission"
      ? rate
        ? `Per-session rate: ${rate}`
        : ""
      : "",
    draft.salaryType === "CommissionBased" || draft.salaryType === "FixedPlusCommission"
      ? commission
        ? `Commission: ${commission}%`
        : ""
      : "",
    frequency ? `Payment frequency: ${frequency}` : "",
  ].filter(Boolean);
  const text = parts.join(". ");
  return text.length > 2000 ? text.slice(0, 2000) : text;
}

export function needsMonthlySalary(salaryType: string) {
  return salaryType === "FixedMonthly" || salaryType === "FixedPlusCommission";
}

export function needsPerSessionRate(salaryType: string) {
  return salaryType === "PerSession" || salaryType === "FixedPlusCommission";
}

export function needsCommission(salaryType: string) {
  return salaryType === "CommissionBased" || salaryType === "FixedPlusCommission";
}

export function commissionEligibleFor(salaryType: string) {
  return salaryType === "CommissionBased" || salaryType === "FixedPlusCommission";
}

export function stepError(step: number, draft: CoachDraft, options?: { requireCompensation?: boolean; requireSeat?: boolean }) {
  if (step === 1) {
    if (!draft.firstName.trim() || !draft.lastName.trim()) return "Enter first and last name.";
    if (!isValidEmail(draft.email)) return "Enter a valid email address.";
    if (!draft.phone.trim() || !isValidPhone(draft.phone)) return "Enter a valid mobile number (include country code such as +977 when needed).";
    if (draft.emergencyPhone.trim() && !isValidPhone(draft.emergencyPhone)) return "Enter a valid emergency contact number.";
    return null;
  }
  if (step === 2) {
    if (!draft.designation.trim()) return "Choose a designation.";
    if (draft.experienceYears.trim() && (!/^\d+$/.test(draft.experienceYears.trim()) || Number(draft.experienceYears) > 80)) {
      return "Enter experience years between 0 and 80.";
    }
    if (specializationText(draft).length > 200) return "Too many specializations selected.";
    if (certificationText(draft).length > 500) return "Certification details are too long.";
    return null;
  }
  if (step === 3) {
    if (!draft.employmentType) return "Choose an employment type.";
    if (!draft.joiningDate) return "Choose a joining date.";
    return null;
  }
  if (step === 5 && options?.requireSeat && !draft.subscriptionId.trim()) {
    return "Assign an available business seat.";
  }
  if (step === 4 && options?.requireCompensation) {
    if (needsMonthlySalary(draft.salaryType) && draft.monthlySalary.trim() && Number(draft.monthlySalary) < 0) {
      return "Monthly salary cannot be negative.";
    }
    if (needsPerSessionRate(draft.salaryType) && draft.perSessionRate.trim() && Number(draft.perSessionRate) < 0) {
      return "Per-session rate cannot be negative.";
    }
    if (needsCommission(draft.salaryType) && draft.commissionPercent.trim()) {
      const value = Number(draft.commissionPercent);
      if (Number.isNaN(value) || value < 0 || value > 100) return "Commission percentage must be between 0 and 100.";
    }
    return null;
  }
  return null;
}

export function employmentTypeLabel(value: string) {
  return (
    {
      FullTime: "Full-time",
      PartTime: "Part-time",
      Contract: "Contract",
      Freelance: "Freelance",
      VisitingTrainer: "Visiting Trainer",
    } as Record<string, string>
  )[value] ?? value;
}

export function salaryTypeLabel(value: string) {
  return (
    {
      FixedMonthly: "Fixed Monthly",
      PerSession: "Per Session",
      CommissionBased: "Commission Based",
      FixedPlusCommission: "Fixed + Commission",
    } as Record<string, string>
  )[value] ?? value;
}

export function draftIsDirty(draft: CoachDraft, today: string) {
  const empty = emptyCoachDraft(today);
  return JSON.stringify(draft) !== JSON.stringify(empty);
}
