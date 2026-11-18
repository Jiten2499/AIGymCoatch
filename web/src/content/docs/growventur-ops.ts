import type { DocArticle } from "./types";

const updated = "2026-11-18";

export const growventurOpsArticles: DocArticle[] = [
  {
    product: "growventur",
    slug: "money/payments",
    title: "Record a member payment",
    description: "Record a full or partial payment against a membership or personal-training balance.",
    category: "Money",
    roles: ["Owner", "Staff"],
    updated,
    popular: true,
    related: ["members/member-record", "money/ledger", "memberships/packages"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Payments are recorded on the member, not in a separate checkout. The Payments tab collects a pending balance for a gym membership or a personal-training package. The balance is the price minus payments already recorded."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Roles that are allowed to record payments see the Record payment form. Other roles can open the member without that form."],
      },
      {
        heading: "Before you begin",
        paragraphs: ["The member needs a membership or personal-training package that still has a balance. If nothing is pending, the tab says no package has a pending balance."],
      },
      {
        heading: "Step-by-step instructions",
        steps: [
          "Open the member and choose Payments.",
          "Choose the balance to collect.",
          "Enter the payment amount. It can be less than the outstanding amount.",
          "Choose the payment method and the payment date.",
          "Enter a reference number when the method requires one.",
          "Choose Save payment.",
        ],
      },
      {
        heading: "Understanding the fields",
        fields: [
          { name: "Amount", detail: "Cannot be above the pending balance." },
          { name: "Reference number", detail: "Required for some methods and optional for others. The field tells you which." },
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The payment is listed on the member and the outstanding balance drops. The same amount is available to the Ledger for that workspace. Growventur does not send the charge to a card processor from this form."],
      },
      {
        heading: "Common issues",
        paragraphs: ["Save payment stays unavailable when no balance is selected. If the reference is required and empty, the form asks you to enter a reference number."],
      },
    ],
  },
  {
    product: "growventur",
    slug: "money/expenses",
    title: "Record an expense",
    description: "Add an expense and, when needed, an expense category.",
    category: "Money",
    roles: ["Owner", "Staff"],
    updated,
    related: ["money/ledger", "settings/configuration"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Expenses records money the business spends. Add category creates a label you can reuse."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["People with expense access, when expense management is enabled for the business."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open Expenses.", "Choose Add category if you need a new label, and save it.", "Choose Add expense.", "Enter the expense and save."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The expense is listed and can be reviewed from the ledger side of the business."],
      },
      {
        heading: "Common issues",
        paragraphs: ["If Expenses is missing from Gym, the feature is off or the role cannot read expenses."],
      },
    ],
  },
  {
    product: "growventur",
    slug: "money/ledger",
    title: "Review the ledger",
    description: "See recorded money movement for the workspace.",
    category: "Money",
    roles: ["Owner", "Staff"],
    updated,
    related: ["money/payments", "money/expenses"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Ledger shows the financial activity recorded in the workspace, including member payments and expenses. It is a record of what staff entered. It is not a bank connection."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["People with ledger access, and only when the financial ledger feature is enabled."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open Ledger.", "Review the summary and transactions.", "Use Add expense when you need to record spending. That link opens Expenses."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["New payments and expenses show up after they are saved on their own pages."],
      },
      {
        heading: "Common issues",
        paragraphs: ["An empty ledger means nothing has been recorded for this workspace yet."],
      },
    ],
  },
  {
    product: "growventur",
    slug: "operations/attendance",
    title: "Check member attendance",
    description: "See attendance on the member record and the attendance rules in configuration.",
    category: "Operations",
    roles: ["Owner", "Staff"],
    updated,
    related: ["members/member-record", "settings/configuration"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Attendance for a person is a tab on the member record. How attendance is treated for the business is set under Configuration, on the Attendance tab."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Staff who can open the member record. Changing the attendance rules requires configuration access."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open the member.", "Choose Attendance.", "To change gym-wide attendance rules, open Configuration and choose Attendance."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The tab shows the attendance already stored for that member. Configuration changes apply to how the business records attendance afterwards."],
      },
      {
        heading: "Common issues",
        paragraphs: ["A member with no visits shows an empty attendance history. That is not an error."],
      },
    ],
  },
  {
    product: "growventur",
    slug: "operations/leaves",
    title: "Handle staff leave",
    description: "Review staff leave and how a request reaches Tasks.",
    category: "Operations",
    roles: ["Owner", "Staff"],
    updated,
    related: ["memberships/approvals", "staff/add-staff"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Staff Leaves lists leave for the business. A leave request that needs a decision also appears under Tasks as Staff leave. On GrowFit, staff with the leave feature can open My Leaves and Apply Leave."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["The web page is available to people who can read leave or apply for leave, and only when staff leave is enabled."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open Staff Leaves to see requests.", "Open Tasks, filter type to Staff leave, and choose Review to approve or reject.", "On the phone, a staff member uses Apply Leave to submit and My Leaves to look at their own requests."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["An approved or rejected request keeps that status. The person can see the result on their leave list."],
      },
      {
        heading: "Common issues",
        paragraphs: ["If Staff Leaves is missing, the leave feature is not enabled for this business."],
      },
    ],
  },
  {
    product: "growventur",
    slug: "operations/circulars",
    title: "Publish a circular",
    description: "Send an announcement or circular to the people you select.",
    category: "Operations",
    roles: ["Owner", "Staff", "Trainer"],
    updated,
    popular: true,
    related: ["operations/chat", "getting-started/roles"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Circulars publishes a message into the inbox of the selected audience. The menu shows an unread count. A circular also includes active trainers in that workspace, even when the audience starts as all members, so coaches are not left out."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["People with communication access, when gym communications are enabled. Readers need that feature as well."],
      },
      {
        heading: "Before you begin",
        paragraphs: ["Choose the audience carefully. All members does not by itself mean every staff role. Trainers are added for circulars. Check the preview counts before you publish."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open Circulars.", "Start a new circular and choose the type.", "Write the message and choose who should receive it.", "Review the audience preview.", "Publish it. Recipients see it in their inbox and, when their phone allows, as a notification."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["Each recipient gets one inbox item. Publishing again does not duplicate people who already received that circular."],
      },
      {
        heading: "Common issues",
        paragraphs: ["A trainer who still cannot see a circular may be inactive, in another workspace, or missing communication access. A phone notification also requires the GrowFit notification permission."],
      },
    ],
  },
  {
    product: "growventur",
    slug: "operations/chat",
    title: "Use workspace chat",
    description: "Open chats between people in the same business.",
    category: "Operations",
    roles: ["Owner", "Staff", "Trainer", "Member"],
    updated,
    related: ["operations/circulars"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Chats is the workspace conversation list. The same conversations are available in GrowFit when chat is enabled. Chat permissions can be adjusted under Configuration when the chat feature is on."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["People with chat access, and only when gym chat is enabled. Personal workspaces do not show the member chat tab."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open Chats.", "Select a conversation or start one with a person you are allowed to message.", "Type the message and send it.", "On the phone, open the Chat tab and the same thread."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The other person sees the message in their chat list. A phone notification is sent only when their notification settings and phone permission allow it. You are not notified about your own message."],
      },
      {
        heading: "Common issues",
        paragraphs: ["If Chat is missing, the feature is off for the business or the role cannot read chat."],
      },
    ],
  },
  {
    product: "growventur",
    slug: "money/payroll",
    title: "Run monthly payroll",
    description: "Load a month, then review, approve, and record staff pay.",
    category: "Money",
    roles: ["Owner", "Staff"],
    updated,
    related: ["money/ledger", "staff/add-staff"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Payroll prepares staff pay for one month. The page says to prepare, review, approve, and record staff pay. It appears only when payroll is enabled and your role can read it."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["People with payroll access."],
      },
      {
        heading: "Before you begin",
        paragraphs: ["Staff who should be paid need compensation details. The page includes a compensation panel before a period is loaded."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open Payroll.", "Enter the year and month.", "Choose Load period.", "Move through the steps shown for that run.", "Open a recent run from the list if you are returning to one that already exists."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The run keeps its status. Recording pay does not create a member invoice. Member payments stay on the member record."],
      },
      {
        heading: "Common issues",
        paragraphs: ["No payroll runs yet means that month has not been loaded. Choose the month and choose Load period."],
      },
    ],
  },
];
