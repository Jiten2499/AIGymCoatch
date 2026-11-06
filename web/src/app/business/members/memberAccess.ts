const memberEditors = ["GymOwner", "GymManager", "Receptionist", "SuperAdmin"];
const sessionEditors = ["GymOwner", "GymManager", "SuperAdmin"];

export function memberDetailsHref(memberId: string): string {
  return `/business/members/${memberId}`;
}

export function canUpdateMember(roles: readonly string[]): boolean {
  return roles.some((role) => memberEditors.includes(role));
}

export function canRecordPtSession(roles: readonly string[]): boolean {
  return roles.some((role) => sessionEditors.includes(role));
}

export function canReadPayments(roles: readonly string[]): boolean {
  return roles.some((role) => ["GymOwner", "GymManager", "Receptionist", "SuperAdmin"].includes(role));
}
