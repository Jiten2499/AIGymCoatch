import { emptyCoachDraft, type CoachCertDraft, type CoachDraft } from "./coachWizard";

export type CoachDetail = {
  id: string;
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  phone?: string | null;
  bio?: string | null;
  specialization?: string | null;
  experienceYears?: number | null;
  certification?: string | null;
  branchId?: string | null;
  status: string;
  employeeCode?: string | null;
  joiningDate?: string | null;
  commissionEligible?: boolean;
  hasPhoto?: boolean;
  staffProfileId?: string | null;
  designation?: string | null;
  dateOfBirth?: string | null;
  gender?: string | null;
  address?: string | null;
  emergencyContactName?: string | null;
  emergencyContactPhone?: string | null;
  employmentType?: string | null;
  reportingManagerUserId?: string | null;
  employmentNotes?: string | null;
  subscriptionId?: string | null;
  planName?: string | null;
};

function parseCertifications(value: string | null | undefined): CoachCertDraft[] {
  if (!value?.trim()) {
    return [];
  }
  return value.split(";").map((part) => {
    const bits = part.split(" · ").map((item) => item.trim()).filter(Boolean);
    const name = bits[0] ?? "";
    const organization = bits.find((item) => item.startsWith("Issued by "))?.replace("Issued by ", "") ?? "";
    const issueDate = bits.find((item) => item.startsWith("Issued ") && !item.startsWith("Issued by "))?.replace("Issued ", "") ?? "";
    const expiryDate = bits.find((item) => item.startsWith("Expires "))?.replace("Expires ", "") ?? "";
    return { name, organization, issueDate, expiryDate };
  }).filter((cert) => cert.name);
}

function noteValue(notes: string | null | undefined, prefix: string) {
  if (!notes) {
    return "";
  }
  const match = notes.split(".").map((part) => part.trim()).find((part) => part.startsWith(prefix));
  return match ? match.slice(prefix.length).trim() : "";
}

export function draftFromCoachDetail(detail: CoachDetail, today: string): CoachDraft {
  const notes = detail.employmentNotes ?? "";
  const rate = noteValue(notes, "Per-session rate:");
  const commission = noteValue(notes, "Commission:").replace("%", "").trim();
  const frequency = noteValue(notes, "Payment frequency:") || "Monthly";
  const workplace = noteValue(notes, "Previous workplace:");
  let salaryType = "FixedMonthly";
  if (detail.commissionEligible && rate) salaryType = "FixedPlusCommission";
  else if (detail.commissionEligible) salaryType = "CommissionBased";
  else if (rate) salaryType = "PerSession";

  return {
    ...emptyCoachDraft(today),
    firstName: detail.firstName,
    middleName: "",
    lastName: detail.lastName,
    email: detail.email,
    phone: detail.phone ?? "",
    dateOfBirth: detail.dateOfBirth?.slice(0, 10) ?? "",
    gender: detail.gender ?? "",
    address: detail.address ?? "",
    emergencyName: detail.emergencyContactName ?? "",
    emergencyPhone: detail.emergencyContactPhone ?? "",
    employeeCode: detail.employeeCode ?? "",
    designation: detail.designation || "Personal Trainer",
    specializations: (detail.specialization ?? "").split(",").map((item) => item.trim()).filter(Boolean),
    experienceYears: detail.experienceYears == null ? "" : String(detail.experienceYears),
    bio: detail.bio ?? "",
    certifications: parseCertifications(detail.certification),
    previousWorkplace: workplace,
    employmentType: detail.employmentType || "FullTime",
    joiningDate: detail.joiningDate?.slice(0, 10) || today,
    branchId: detail.branchId ?? "",
    reportingManagerUserId: detail.reportingManagerUserId ?? "",
    employmentStatus: detail.status === "Inactive" ? "Inactive" : "Active",
    salaryType,
    monthlySalary: "",
    perSessionRate: rate,
    commissionPercent: commission,
    paymentFrequency: frequency,
    commissionEligible: detail.commissionEligible === true,
    subscriptionId: detail.subscriptionId ?? "",
    planName: detail.planName ?? "",
    sendActivationLink: false,
    linkExistingUser: true,
  };
}

export function findCoachPay(rows: { staffProfileId: string; baseSalary: number; allowances: number }[] | undefined, staffProfileId: string | null | undefined) {
  if (!rows || !staffProfileId) {
    return null;
  }
  return rows.find((row) => row.staffProfileId === staffProfileId) ?? null;
}
