import type { DocArticle } from "./types";

const updated = "2026-11-18";

export const growventurStartArticles: DocArticle[] = [
  {
    product: "growventur",
    slug: "getting-started/introduction",
    title: "Introduction to Growventur",
    description: "What the business workspace is for, and how it differs from the GrowFit phone app.",
    category: "Getting started",
    roles: ["Owner", "Staff", "Trainer"],
    updated,
    popular: true,
    related: ["getting-started/sign-in", "getting-started/dashboard", "getting-started/roles"],
    sections: [
      {
        heading: "Overview",
        paragraphs: [
          "Growventur is the workspace where a gym or studio runs day-to-day work: members, memberships, coaches, leads, money records, circulars, and chat.",
          "GrowFit is the phone app members and trainers use for workouts, nutrition, progress, and chat. The same person can have a workspace role and a GrowFit login. Do not create a second person for the same email.",
        ],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Anyone who has been given a workspace. What you see depends on your role and on the features included in the business plan."],
      },
      {
        heading: "Before you begin",
        bullets: [
          "Use the web address your business was given, then open Platform sign in.",
          "A menu item appears only when your role allows it and the feature is turned on for the business.",
        ],
      },
      {
        heading: "Understanding the workspace",
        bullets: [
          "Overview holds the Dashboard.",
          "Access holds Staff & Access and Coaches.",
          "Gym holds Members, Memberships, Tasks, Leads, Expenses, Ledger, Payroll, Staff Leaves, Circulars, Chats, and Configuration.",
          "Licensing holds Plans for the business subscription.",
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["After sign-in, Growventur opens the workspace you belong to. If you belong to more than one, the current workspace is the one shown for this session."],
      },
      {
        heading: "Common issues",
        paragraphs: ["A missing menu usually means the role does not include that permission, or the business plan does not include that feature. Ask the person who manages Staff & Access."],
      },
    ],
  },
  {
    product: "growventur",
    slug: "getting-started/sign-in",
    title: "Sign in to Growventur",
    description: "How to open the business workspace from the sign-in page.",
    category: "Getting started",
    roles: ["Owner", "Staff", "Trainer"],
    updated,
    popular: true,
    related: ["getting-started/reset-password", "getting-started/dashboard"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["The sign-in page is labelled Platform sign in. It opens the workspace attached to your account."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Anyone with an active Growventur account. New staff and members receive access from the business. They do not create a second account if the email already exists."],
      },
      {
        heading: "Before you begin",
        bullets: ["Use the email registered for your account.", "Have the password, or use Forgot password first."],
      },
      {
        heading: "Step-by-step instructions",
        steps: [
          "Open the Growventur sign-in page.",
          "Enter Email and Password.",
          "Choose Sign in.",
          "Wait until the workspace opens. The page says your workspace opens after sign-in.",
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["You land on the business dashboard when your role can see it. The sidebar lists only the areas you are allowed to open."],
      },
      {
        heading: "Common issues",
        bullets: [
          "An email without @ is rejected before sign-in.",
          "A wrong password shows a sign-in failure. Use Forgot password rather than guessing.",
          "If the page stays on Checking your session, refresh once. If it continues, the account session could not be restored.",
        ],
      },
    ],
  },
  {
    product: "growventur",
    slug: "getting-started/reset-password",
    title: "Reset a forgotten password",
    description: "Request a reset link and return to sign-in.",
    category: "Getting started",
    roles: ["Owner", "Staff", "Trainer", "Member"],
    updated,
    related: ["getting-started/sign-in"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Forgot password emails a reset link when the address matches an active account. The page does not reveal whether an address is registered."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Anyone who can open the sign-in page."],
      },
      {
        heading: "Before you begin",
        paragraphs: ["Use the same email that is on the account. Check spam if the message does not arrive."],
      },
      {
        heading: "Step-by-step instructions",
        steps: [
          "On Platform sign in, choose Forgot password.",
          "Enter the registered email.",
          "Choose Send password reset link.",
          "Open the link from the email and set a new password.",
          "Choose Back to sign in, then sign in with the new password.",
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The page confirms that if the email is registered, a reset link is on its way. Staff can also send a password reset for a person from Members, Coaches, or Staff & Access."],
      },
      {
        heading: "Common issues",
        paragraphs: ["The email field must be a valid email address. If no message arrives, confirm the address with the business and try again. Do not share the reset link."],
      },
    ],
  },
  {
    product: "growventur",
    slug: "getting-started/dashboard",
    title: "Read the business dashboard",
    description: "What the dashboard shows for a business user and for a role-specific home.",
    category: "Getting started",
    roles: ["Owner", "Staff"],
    updated,
    popular: true,
    related: ["getting-started/introduction", "members/member-record", "money/ledger"],
    sections: [
      {
        heading: "Overview",
        paragraphs: [
          "Dashboard is the first item under Overview. A business home summarises activity for a date range. A person whose role has a personal home sees a greeting and the widgets and actions prepared for that role.",
        ],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["People whose workspace includes the dashboard. The numbers change with the permissions on the account."],
      },
      {
        heading: "Before you begin",
        paragraphs: ["Sign in and stay in the correct workspace. Empty charts mean there is no activity in the selected range, not that the page failed."],
      },
      {
        heading: "Step-by-step instructions",
        steps: [
          "Open Dashboard from Overview.",
          "Read the summary cards for the current range.",
          "Change the date range when a range filter is shown.",
          "Use a quick action when one is offered. It opens the related workspace page, such as members or leads.",
          "Review attention items and open the linked area if you need to act.",
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The dashboard does not change member or payment records by itself. Follow the link into Members, Tasks, or another page to do the work."],
      },
      {
        heading: "Common issues",
        paragraphs: ["A suspended workspace or a missing permission replaces the summary with an explanation. Refresh after your access is updated."],
      },
    ],
  },
  {
    product: "growventur",
    slug: "getting-started/roles",
    title: "Roles, staff access, and features",
    description: "Why two people in the same gym can see different menus.",
    category: "Getting started",
    roles: ["Owner", "Staff"],
    updated,
    related: ["staff/add-staff", "settings/configuration", "settings/plans"],
    sections: [
      {
        heading: "Overview",
        paragraphs: [
          "A person is one account. Workspace access is their role in the business. A gym membership is separate and is managed under Members.",
          "Menus are permission based. Circulars, Chats, Ledger, Payroll, and Staff Leaves also stay hidden until that feature is enabled for the business.",
        ],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Staff & Access requires permission to read workspace people. Configuration, including Roles & Permissions, requires configuration access."],
      },
      {
        heading: "Before you begin",
        bullets: [
          "Owner and staff roles are assigned in the workspace. Normal registration does not make someone a platform super administrator.",
          "A trainer who should use GrowFit still uses the same user account.",
        ],
      },
      {
        heading: "Step-by-step instructions",
        steps: [
          "Open Staff & Access to see people and their roles.",
          "Open Configuration and choose Roles & Permissions to review what a role can do.",
          "Open Plans under Licensing to see the business subscription and included features.",
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["After a role or feature change, the person may need to refresh. New menu items appear only when both the permission and the feature allow them."],
      },
      {
        heading: "Common issues",
        paragraphs: ["Tasks shows a count only when approval requests can be loaded. If Tasks says you do not have permission to review requests, your role can see the page but cannot decide those requests."],
      },
    ],
  },
];
