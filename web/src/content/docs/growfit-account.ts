import type { DocArticle } from "./types";

const updated = "2026-11-18";

export const growfitAccountArticles: DocArticle[] = [
  {
    product: "growfit",
    slug: "members/ai-coach",
    title: "Ask the AI coach",
    description: "Start a coaching conversation from More.",
    category: "Members",
    roles: ["Member"],
    updated,
    related: ["members/home-and-plans", "members/nutrition"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["AI coach is a conversation in the app. It can help you think about training and nutrition. It does not replace your trainer and it does not change your membership."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Members, from More, then AI coach. The business must allow the AI feature. If it is off, the screen is not a working coach."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open More.", "Choose AI coach.", "Start a conversation or open an existing one.", "Read the reply. Use your trainer or a clinician for medical decisions."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The conversation stays in the app. A reply is guidance. It is not saved as your official workout plan unless you generate a plan from the assessment flow."],
        warning: "Do not treat an AI reply as medical advice.",
      },
      {
        heading: "Common issues",
        paragraphs: ["If the coach cannot reply, check that you are online. The app shows when you appear to be offline."],
      },
    ],
  },
  {
    product: "growfit",
    slug: "members/messages",
    title: "Read chat, circulars, and notifications",
    description: "See messages from the gym and control which alerts the phone delivers.",
    category: "Members",
    roles: ["Member", "Trainer"],
    updated,
    popular: true,
    related: ["account/settings", "getting-started/navigation"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Chat is a direct conversation. Circulars are published announcements. Notifications lists alerts already created for you. Each one can also appear on the phone when permission and preferences allow it."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Members and trainers in a business that has chat or communications enabled. Personal workspaces do not show member chat."],
      },
      {
        heading: "Step-by-step instructions",
        steps: [
          "Open the Chat tab and choose a conversation.",
          "Open a circular from its notification or from the circulars list when it is shown.",
          "Open Notifications to review older alerts.",
          "Open Settings and set Chat, workout reminders, workout plan updates, progress, and account notifications.",
          "Allow notifications in the phone settings if the app says phone notifications are off.",
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["Preferences stay with your account. Appearance, accent, and language stay on this device. Sound and vibration follow the switches in Settings."],
      },
      {
        heading: "Common issues",
        paragraphs: ["A missing Chat tab means the business has not enabled chat. A circular that never arrives was not addressed to you, or the phone blocked the alert. The inbox item can still exist when the banner does not."],
      },
    ],
  },
  {
    product: "growfit",
    slug: "account/profile",
    title: "Update your profile and sign out",
    description: "Change the details GrowFit shows for you.",
    category: "Account",
    roles: ["Member", "Trainer"],
    updated,
    related: ["account/settings", "getting-started/sign-in"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Profile holds the name and photo for the signed-in person. Trainers also see qualifications, experience, bio, and joining date on More."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Members and trainers."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open More.", "Choose Profile.", "Update the details the screen allows and save.", "To leave the app, trainers choose Log out on More. Members sign out from the account area on Profile or Settings."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The photo and name update for people who can see your profile in this workspace. Signing out clears the session on this phone."],
      },
      {
        heading: "Common issues",
        paragraphs: ["If a field is missing, the business edits that detail in Growventur, not on the phone. Membership status is managed by the gym."],
      },
    ],
  },
  {
    product: "growfit",
    slug: "account/settings",
    title: "Change appearance and notification settings",
    description: "Set theme, language, and which alerts you want.",
    category: "Account",
    roles: ["Member", "Trainer"],
    updated,
    related: ["members/messages", "account/profile"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Settings controls appearance and notifications. Theme choices are System, Light, and Dark. Language can be changed on the device. Hindi and Nepali stay in English until those translations are reviewed."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Members and trainers, from More, then Settings."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open More, then Settings.", "Choose appearance, accent, and language.", "Under Notifications, turn chat, workout reminders, plan updates, progress, and account alerts on or off.", "Turn sound or vibration on or off.", "If the phone blocked alerts, open system settings and allow them for GrowFit."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["Appearance stays on this phone. Notification switches are used the next time the gym or the app sends an alert."],
      },
      {
        heading: "Common issues",
        paragraphs: ["Turning a switch on does nothing until the phone permission is allowed. The screen says GrowFit will deliver alerts after you allow them."],
      },
    ],
  }
];
