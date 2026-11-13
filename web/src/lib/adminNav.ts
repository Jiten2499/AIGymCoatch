export type AdminLink = readonly [string, string];

export type AdminGroup = { title: string; links: readonly AdminLink[] };

const baseGroups: AdminGroup[] = [
  { title: "Overview", links: [["Overview", "/admin"]] },
  { title: "AI", links: [["Connections", "/admin/llm"], ["Models", "/admin/models"]] },
  { title: "Directory", links: [["Tenants", "/admin/tenants"], ["Users", "/admin/users"]] },
  { title: "Licensing", links: [["Plans", "/admin/plans"], ["Features", "/admin/features"], ["Subscriptions", "/admin/licenses"], ["Seats", "/admin/assignments"], ["Expiring", "/admin/expiring"]] },
  { title: "System Settings", links: [["Email & Delivery", "/admin/email"], ["Google Drive", "/admin/integrations"]] },
  { title: "Audit", links: [["Activity", "/admin/audit"]] },
];

export function adminGroups(canMonitorJobs: boolean): AdminGroup[] {
  if (!canMonitorJobs) {
    return baseGroups;
  }
  return [...baseGroups, { title: "System", links: [["Background Jobs", "/admin/jobs"]] }];
}
