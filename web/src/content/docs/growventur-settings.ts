import type { DocArticle } from "./types";

const updated = "2026-11-18";

export const growventurSettingsArticles: DocArticle[] = [
  {
    product: "growventur",
    slug: "settings/configuration",
    title: "Change business configuration",
    description: "Set membership, discount, category, training, freeze, attendance, CRM, chat, and role options.",
    category: "Settings",
    roles: ["Owner"],
    updated,
    related: ["memberships/packages", "leads/add-lead", "getting-started/roles"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Configuration is where the business sets shared rules. The tabs are General, Membership, Discounts, Member Categories, PT Packages, Coach Rewards, Freeze & Extensions, Attendance, CRM, Chat, and Roles & Permissions. Chat appears only when chat is enabled. General is where the business currency is chosen. That currency is used for prices, payments, expenses, payroll, and the dashboard."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["People with configuration access."],
      },
      {
        heading: "Step-by-step instructions",
        steps: [
          "Open Configuration.",
          "On General, choose the currency and save it. This is the only place that sets the currency.",
          "Choose the tab you need.",
          "Use Add category, Add discount, Add lead source, or Add lost reason where those buttons are shown.",
          "Save the form on that tab.",
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["New packages, discounts, categories, and CRM labels become available in the related screens. Freeze and extension rules are what Tasks uses when those requests are reviewed."],
      },
      {
        heading: "Common issues",
        paragraphs: ["A missing Chat tab means gym chat is not enabled. It is not a broken page."],
      },
    ],
  },
  {
    product: "growventur",
    slug: "settings/plans",
    title: "View the business plan",
    description: "See the subscription that turns workspace features on.",
    category: "Settings",
    roles: ["Owner"],
    updated,
    related: ["getting-started/roles", "settings/configuration"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Plans under Licensing shows the subscription for this business. Features such as chat, ledger, payroll, communications, and leave follow that subscription. This page is not where you edit a member's workout plan."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["People who can read the business licence."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open Plans.", "Review the current subscription and the features it includes.", "Ask the platform owner if a feature your gym needs is not listed. Staff cannot invent a feature from this screen."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["Enabled features show their menus. Disabled features stay hidden even for an owner."],
      },
      {
        heading: "Common issues",
        paragraphs: ["Payroll and Ledger stay hidden until those features are on the plan and your role can read them. There is no separate invoice or refund module in the workspace."],
      },
    ],
  }
];
