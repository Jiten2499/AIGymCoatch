import type { DocArticle } from "./types";

const updated = "2026-11-18";

export const growventurLeadArticles: DocArticle[] = [
  {
    product: "growventur",
    slug: "leads/add-lead",
    title: "Add and follow a lead",
    description: "Track a prospective customer before they become a member.",
    category: "Leads",
    roles: ["Owner", "Staff"],
    updated,
    related: ["members/add-member", "settings/configuration"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Leads is the list of people who are not members yet. Add lead records the prospect. Lead sources and lost reasons are maintained in Configuration under CRM."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["People with CRM read access, and only when the CRM feature is enabled."],
      },
      {
        heading: "Before you begin",
        paragraphs: ["Optional: add lead sources and lost reasons in Configuration so the form uses your gym's labels."],
      },
      {
        heading: "Step-by-step instructions",
        steps: [
          "Open Leads.",
          "Choose Add lead.",
          "Enter the prospect details and save.",
          "Open the lead to update status and follow-up notes.",
          "When they join, add them from Members. Do not keep a second unrelated account for the same email.",
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The lead stays in Leads until you update it. Adding a member does not delete the lead automatically. Update the lead status so the list stays accurate."],
      },
      {
        heading: "Common issues",
        paragraphs: ["If Leads is missing, the business plan does not include CRM or your role cannot read leads."],
      },
    ],
  }
];
