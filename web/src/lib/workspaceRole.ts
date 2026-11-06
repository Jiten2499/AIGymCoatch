const businessStaff = ["GymOwner", "BusinessAdmin", "GymManager", "Receptionist", "Accountant", "Trainer"] as const;
const personal = new Set(["PersonalOwner", "PersonalMember", "Member"]);

const labels: Record<string, string> = {
  GymOwner: "Business Owner",
  BusinessAdmin: "Business Admin",
  GymManager: "Manager",
  Receptionist: "Receptionist",
  Accountant: "Accountant",
  Trainer: "Coach",
};

export function displayWorkspaceRole(roles: readonly string[]) {
  const custom = roles.find((role) => !personal.has(role) && !businessStaff.includes(role as (typeof businessStaff)[number]) && role !== "SuperAdmin");
  if (custom) {
    return custom;
  }

  return businessStaff.find((role) => roles.includes(role))
    ?? roles.find((role) => !personal.has(role))
    ?? roles[0]
    ?? "";
}

export function workspaceRoleLabel(roleName: string) {
  return labels[roleName] ?? roleName;
}
