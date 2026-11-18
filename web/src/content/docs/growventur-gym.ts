import type { DocArticle } from "./types";

const updated = "2026-11-18";

export const growventurGymArticles: DocArticle[] = [
  {
    product: "growventur",
    slug: "members/add-member",
    title: "Add a member",
    description: "Create a member with the five-step Add member window.",
    category: "Members",
    roles: ["Owner", "Staff"],
    updated,
    popular: true,
    related: ["members/member-record", "memberships/packages", "coaches/add-coach"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Add member creates the gym member and, when you choose it, a membership package and app access. The window is titled Add member and has five steps."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Staff who can open Members. Saving uses the member permissions on the role."],
      },
      {
        heading: "Before you begin",
        bullets: ["Create coaches first if you want to assign a trainer.", "Create a membership package if the member should start with one.", "Use the person's existing email when they already have an account."],
      },
      {
        heading: "Step-by-step instructions",
        steps: [
          "Open Members.",
          "Choose Add member.",
          "On step 1, enter full name, mobile number, email, date of birth, and gender. Add a JPEG, PNG, or WebP profile photo if you have one.",
          "Choose Next. On step 2, enter address, city, state, postal code, and the emergency contact name, mobile, and relationship.",
          "Choose Next. On step 3, set joining date, member status, fitness goal, and assigned trainer. Turn on app access only when this person should sign in to GrowFit.",
          "Choose Next. On step 4, choose the membership package details you want to start with.",
          "Choose Next. Review step 5, then choose Save member.",
          "If a temporary password is shown, share it securely and choose Done.",
        ],
      },
      {
        heading: "Understanding the fields",
        fields: [
          { name: "Assigned trainer", detail: "Optional. Choose None when no coach is assigned yet." },
          { name: "App access", detail: "Needs an email. The page blocks an activation link when the email is empty." },
          { name: "Member status", detail: "Sets whether the member starts active or inactive." },
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The member appears in the Members list. Open the row to see Overview, Membership, PT & Training, Payments, Attendance, and Progress."],
      },
      {
        heading: "Common issues",
        paragraphs: ["Previous returns to the earlier step without saving. Cancel closes the window. If the email already belongs to someone, reuse that account instead of creating another person."],
      },
    ],
  },
  {
    product: "growventur",
    slug: "members/member-record",
    title: "Open and update a member",
    description: "Search the member list and use the member record tabs.",
    category: "Members",
    roles: ["Owner", "Staff", "Trainer"],
    updated,
    related: ["members/add-member", "money/payments", "operations/attendance"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Members lists people in the current workspace. The record shows a member code and status, then tabs for the rest of their gym activity."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Edit and Delete appear for roles that can update members. Payments on the record appear for roles that can record payments. Trainers may see a narrower record."],
      },
      {
        heading: "Before you begin",
        paragraphs: ["You must be in the member's workspace. A member from another business is not listed."],
      },
      {
        heading: "Step-by-step instructions",
        steps: [
          "Open Members.",
          "Search by name or use the status filter in the address when one is applied.",
          "Open the member.",
          "Choose Edit to change details, then Save member. Choose Delete only when the profile should leave active business views.",
          "Use the status button on the list or overview to switch Active and Inactive.",
          "Open PT & Training to assign a personal-training package or record a session.",
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["Edits stay on that member. Delete removes the profile from active business views. The product keeps the underlying record as deleted data rather than offering a public restore button on this page."],
      },
      {
        heading: "Common issues",
        paragraphs: ["This member was not found in the current workspace means the link is wrong or the workspace changed. Go back to Members and open the person again."],
      },
    ],
  },
  {
    product: "growventur",
    slug: "memberships/packages",
    title: "Create a membership package",
    description: "Add the packages a member can be sold.",
    category: "Memberships",
    roles: ["Owner", "Staff"],
    updated,
    popular: true,
    related: ["members/add-member", "memberships/approvals", "settings/configuration"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Memberships stores the packages the gym sells. A package is assigned while adding a member, from the member record, or through a later membership action."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["People who can read memberships see the page. Creating a package follows the membership permissions on the role."],
      },
      {
        heading: "Before you begin",
        paragraphs: ["Decide the name, price, and length before you start. The currency shown on the price is the one saved under Configuration, on General. Discounts and freeze rules are configured separately under Configuration."],
      },
      {
        heading: "Step-by-step instructions",
        steps: [
          "Open Memberships.",
          "Choose Add package.",
          "Enter the package details shown in the form. The currency box is filled from the business currency and cannot be typed over.",
          "Save the package.",
          "Assign it from Add member or from the member's Membership or PT & Training tab.",
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The package can be selected for new and existing members. It does not by itself charge a card. Record money on the member Payments tab."],
      },
      {
        heading: "Common issues",
        paragraphs: ["If Memberships is missing, the role lacks membership access or the membership feature is off for the business."],
      },
    ],
  },
  {
    product: "growventur",
    slug: "memberships/approvals",
    title: "Review membership and leave tasks",
    description: "Approve or reject freeze, extension, leave, and coach-target requests.",
    category: "Memberships",
    roles: ["Owner", "Staff"],
    updated,
    related: ["operations/leaves", "coaches/add-coach", "members/member-record"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Tasks lists approval requests. The sidebar name includes a count while requests are waiting. Types are membership freeze, membership extension, coach target credit, and staff leave."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["The page is shown to roles that can freeze or renew a membership, update a trainer, or approve leave. Reviewing a request still checks that permission. Other people see that they cannot review requests."],
      },
      {
        heading: "Before you begin",
        paragraphs: ["A request must already exist. Creating the request happens from the related membership, coach, or leave flow."],
      },
      {
        heading: "Step-by-step instructions",
        steps: [
          "Open Tasks.",
          "Filter by Pending, Approved, Rejected, or all statuses.",
          "Filter by type when you only want one kind of request.",
          "Choose Review on the request.",
          "Approve or reject it in the review window.",
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The request status changes. A freeze or extension does not apply until the request is approved."],
      },
      {
        heading: "Common issues",
        paragraphs: ["No approval requests match this view means the filters are too narrow. Set status to All statuses and clear the type."],
      },
    ],
  },
  {
    product: "growventur",
    slug: "coaches/add-coach",
    title: "Add a coach",
    description: "Create a coach profile and connect it to a user account.",
    category: "Coaches",
    roles: ["Owner", "Staff"],
    updated,
    related: ["staff/add-staff", "members/add-member"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Coaches holds trainer profiles: name, specialisation, qualifications, and the user account the coach uses. The same page can send a password reset."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["People with permission to read trainers see Coaches under Access."],
      },
      {
        heading: "Before you begin",
        paragraphs: ["If the coach already has a Growventur user, look them up by email or phone in the add wizard instead of creating a duplicate account."],
      },
      {
        heading: "Step-by-step instructions",
        steps: [
          "Open Coaches.",
          "Choose Add coach.",
          "Enter the coach details. If the email or phone matches an existing user, continue with that person.",
          "Save the coach.",
          "Assign the coach later from a member's Assigned trainer field.",
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The coach can appear in trainer pickers. Their GrowFit trainer home is available when the account has the trainer role and they sign in on the phone."],
      },
      {
        heading: "Common issues",
        paragraphs: ["A coach without an active profile is not offered as an assigned trainer. Inactive trainers are excluded from new assignments."],
      },
    ],
  },
  {
    product: "growventur",
    slug: "staff/add-staff",
    title: "Add staff and set access",
    description: "Invite a workspace user and give them a role.",
    category: "Staff",
    roles: ["Owner"],
    updated,
    related: ["getting-started/roles", "settings/configuration"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Staff & Access is the list of people who can open the workspace. Add staff collects their details and role. You can edit a person, send a password reset, or remove them from active access."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["People who can read workspace members. Role choices come from the roles the business is allowed to assign."],
      },
      {
        heading: "Before you begin",
        paragraphs: ["Look up the email and phone first. An existing user should be reused."],
      },
      {
        heading: "Step-by-step instructions",
        steps: [
          "Open Staff & Access.",
          "Choose Add staff.",
          "Enter the person and choose the role.",
          "Save.",
          "Ask them to sign in, or send a password reset from the list if they cannot.",
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The person sees the menus allowed by that role and by the business features. Removing them hides the profile from active access."],
      },
      {
        heading: "Common issues",
        paragraphs: ["You cannot grant platform super administrator from this screen. That access is not part of normal staff setup."],
      },
    ],
  },
];
