export type StaffDraft = {
  fullName: string;
  email: string;
  phone: string;
  dateOfBirth: string;
  gender: string;
  address: string;
  emergencyName: string;
  emergencyPhone: string;
  employeeCode: string;
  designation: string;
  department: string;
  employmentType: string;
  joiningDate: string;
  branchId: string;
  reportingManagerUserId: string;
  employmentStatus: string;
  employmentNotes: string;
  roleName: string;
  subscriptionId: string;
  authorizedBranchIds: string[];
  memberScope: string;
  paymentScope: string;
  permissionOverrides: string[];
  specialization: string;
  certification: string;
  experienceYears: string;
  trainingExpertise: string;
  jobDescription: string;
  linkExistingUser: boolean;
  sendActivationLink: boolean;
};

export const employmentTypes = ["FullTime", "PartTime", "Contract", "Temporary", "Intern"] as const;
export const genders = ["", "Female", "Male", "NonBinary", "PreferNotToSay"] as const;
export const dataScopes = ["Own", "Assigned", "Branch", "All"] as const;

export function emptyStaffDraft(today: string, roleName = "Receptionist"): StaffDraft {
  return {
    fullName: "",
    email: "",
    phone: "",
    dateOfBirth: "",
    gender: "",
    address: "",
    emergencyName: "",
    emergencyPhone: "",
    employeeCode: "",
    designation: "",
    department: "",
    employmentType: "FullTime",
    joiningDate: today,
    branchId: "",
    reportingManagerUserId: "",
    employmentStatus: "Active",
    employmentNotes: "",
    roleName,
    subscriptionId: "",
    authorizedBranchIds: [],
    memberScope: "All",
    paymentScope: "Own",
    permissionOverrides: [],
    specialization: "",
    certification: "",
    experienceYears: "",
    trainingExpertise: "",
    jobDescription: "",
    linkExistingUser: false,
    sendActivationLink: false,
  };
}

export function splitFullName(value: string) {
  const parts = value.trim().split(/\s+/).filter(Boolean);
  if (parts.length < 2) {
    return null;
  }
  return { firstName: parts[0], lastName: parts.slice(1).join(" ") };
}

export function roleKind(roleName: string) {
  if (roleName === "Trainer") return "trainer";
  if (roleName === "GymManager") return "manager";
  if (roleName === "Accountant") return "accountant";
  if (roleName === "Receptionist") return "receptionist";
  return "other";
}

export function stepError(step: number, draft: StaffDraft, requireBranch: boolean, options?: { requireSeat?: boolean }) {
  const requireSeat = options?.requireSeat !== false;
  if (step === 1) {
    if (!splitFullName(draft.fullName)) return "Enter a first and last name.";
    if (!draft.email.trim() || !draft.email.includes("@")) return "Enter a valid email address.";
    if (!draft.phone.trim()) return "Enter a mobile number.";
    return null;
  }
  if (step === 2) {
    if (!draft.designation.trim()) return "Enter a designation / job title.";
    if (!draft.joiningDate) return "Choose a joining date.";
    if (requireBranch && !draft.branchId) return "Choose a branch.";
    return null;
  }
  if (step === 3) {
    if (!draft.roleName) return "Choose a role.";
    if (requireSeat && !draft.subscriptionId) return "Assign an available business seat.";
    return null;
  }
  return null;
}

export type StaffDetail = {
  userId: string;
  staffProfileId: string | null;
  email: string;
  firstName: string;
  lastName: string;
  phone: string | null;
  roleName: string;
  isOwner: boolean;
  accountStatus: string;
  employmentStatus: string;
  employeeCode: string | null;
  designation: string | null;
  department: string | null;
  employmentType: string | null;
  joiningDate: string | null;
  branchId: string | null;
  reportingManagerUserId: string | null;
  employmentNotes: string | null;
  jobDescription: string | null;
  dateOfBirth: string | null;
  gender: string | null;
  address: string | null;
  emergencyContactName: string | null;
  emergencyContactPhone: string | null;
  authorizedBranchIds: string[];
  subscriptionId: string | null;
  planName: string | null;
  specialization: string | null;
  certification: string | null;
  experienceYears: number | null;
  trainingExpertise: string | null;
  memberScope: string;
  paymentScope: string;
};

export function draftFromDetail(detail: StaffDetail): StaffDraft {
  const deactivated = detail.employmentStatus === "Inactive" ? "Deactivated" : detail.employmentStatus;
  return {
    fullName: `${detail.firstName} ${detail.lastName}`.trim(),
    email: detail.email,
    phone: detail.phone ?? "",
    dateOfBirth: detail.dateOfBirth?.slice(0, 10) ?? "",
    gender: detail.gender ?? "",
    address: detail.address ?? "",
    emergencyName: detail.emergencyContactName ?? "",
    emergencyPhone: detail.emergencyContactPhone ?? "",
    employeeCode: detail.employeeCode ?? "",
    designation: detail.designation ?? "",
    department: detail.department ?? "",
    employmentType: detail.employmentType ?? "FullTime",
    joiningDate: detail.joiningDate?.slice(0, 10) ?? "",
    branchId: detail.branchId ?? "",
    reportingManagerUserId: detail.reportingManagerUserId ?? "",
    employmentStatus: deactivated || "Active",
    employmentNotes: detail.employmentNotes ?? "",
    roleName: detail.roleName === "Trainer" ? "Receptionist" : detail.roleName || "Receptionist",
    subscriptionId: detail.subscriptionId ?? "",
    authorizedBranchIds: detail.authorizedBranchIds ?? [],
    memberScope: detail.memberScope || "All",
    paymentScope: detail.paymentScope || "Own",
    permissionOverrides: [],
    specialization: detail.specialization ?? "",
    certification: detail.certification ?? "",
    experienceYears: detail.experienceYears?.toString() ?? "",
    trainingExpertise: detail.trainingExpertise ?? "",
    jobDescription: detail.jobDescription ?? "",
    linkExistingUser: true,
    sendActivationLink: false,
  };
}

export function employmentTypeLabel(value: string) {
  return ({ FullTime: "Full-time", PartTime: "Part-time", Contract: "Contract", Temporary: "Temporary", Intern: "Intern" } as Record<string, string>)[value] ?? value;
}

export function scopeLabel(value: string) {
  return ({ Own: "Own records", Assigned: "Assigned records", Branch: "Branch", All: "Business" } as Record<string, string>)[value] ?? value;
}
