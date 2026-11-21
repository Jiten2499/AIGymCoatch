import { growfitSupportEmail, type TermsSection } from "./growfitTerms";

export const growfitPrivacyUpdated = "21 November 2026";

export { growfitSupportEmail };

export const growfitPrivacy: TermsSection[] = [
  {
    id: "introduction",
    title: "Introduction",
    blocks: [
      {
        type: "p",
        text: "GrowFit is the fitness application offered with the Growventur platform. This Privacy Policy explains what information GrowFit handles when you create an account, sign in, or use the features available to you.",
      },
      {
        type: "p",
        text: "Not every feature is available on every account. A gym, trainer, membership, subscription, or permission setting can turn a feature off. Information described below is collected only when you, your trainer, or your gym uses the feature that needs it.",
      },
    ],
  },
  {
    id: "information",
    title: "Information we collect",
    blocks: [
      {
        type: "p",
        text: "Account information. Registration and profile screens collect your name and email address. A phone number can be stored when one is provided. Your password is stored as a hash and salt, not as the password you type. The account can also store a profile photo, account status, and the time of the last sign-in.",
      },
      {
        type: "p",
        text: "Fitness and health-related information. When you use the fitness features, GrowFit can store a fitness profile (units, height, weight, fitness level, goal, activity level, training experience, and preferred training days, duration, and location), goals, body measurements (weight, body-fat percentage, chest, waist, hip, arm, thigh, and notes), workout plans, and workout sessions (exercises, sets, reps or duration, load, rest, and notes).",
      },
      {
        type: "p",
        text: "Nutrition features can store daily calorie and macro targets, a dietary preference, meal frequency, and meal logs with the food name, quantity, and calories, protein, carbohydrates, and fat.",
      },
      {
        type: "p",
        text: "The fitness assessment, when you complete it, can store date of birth, gender, country, body measurements, goals, and the health, workout, diet, and lifestyle answers you enter. Those health answers can include conditions, injuries, physical limitations, food allergies or intolerances, medications, doctor restrictions, and notes. Leave any of that blank if you do not want it stored.",
      },
      {
        type: "p",
        text: "A trainer can record a progress assessment for a member, including measurements and progress photos. The progress-photo step asks for consent before photos are saved. Photos are image files (JPEG, PNG, or WebP) stored for that assessment.",
      },
      {
        type: "p",
        text: "Communications. Where chat is enabled, GrowFit stores the message text, sender, conversation, and any image or file attached to a message. Circulars and other notices sent to you are stored so they can be shown in the app.",
      },
      {
        type: "p",
        text: "Device and technical information. If you allow notifications, GrowFit registers an installation id, an Expo push token, the platform, the app version, and the time the device was last registered. Security and audit records for some actions can include the IP address and user agent of the request. GrowFit does not include a third-party analytics SDK.",
      },
    ],
  },
  {
    id: "use",
    title: "How we use information",
    blocks: [
      {
        type: "p",
        text: "GrowFit uses this information to create and sign you in to an account, keep the session, and show the profile you saved. It uses fitness, workout, measurement, and nutrition information to provide the plans, logs, and progress screens you or your trainer open.",
      },
      {
        type: "p",
        text: "It uses membership and role information to connect you with a gym or trainer and to decide which screens and records you can open. It uses messages and circulars to deliver those communications, and push tokens to deliver notifications you have not turned off.",
      },
      {
        type: "p",
        text: "It also uses account and technical information to protect the service, investigate misuse, troubleshoot failures, send account email such as an activation or password-reset message, and respond to a support request. Gym staff with the right permission can see operational summaries of their own workspace, such as member, attendance, and membership activity. That reporting stays inside Growventur. It is not a separate advertising or analytics product.",
      },
    ],
  },
  {
    id: "health",
    title: "Health and fitness information",
    blocks: [
      {
        type: "p",
        text: "Fitness and health-related information is processed so GrowFit can show plans, logs, measurements, nutrition, assessments, and progress to the people allowed to see them. Provide only what you are comfortable storing.",
      },
      {
        type: "p",
        text: "A trainer assigned to you, and gym staff whose role allows it, can view the member fitness records their permissions cover. GrowFit does not hide those records from every other user of the same gym.",
      },
      {
        type: "p",
        text: "GrowFit is not a medical service. Workout plans, nutrition figures, assessment notes, and AI coaching text are fitness information. They are not a diagnosis, a treatment, or a substitute for advice from a qualified clinician.",
      },
    ],
  },
  {
    id: "sharing",
    title: "How information is shared",
    blocks: [
      {
        type: "p",
        text: "People using the same workspace. Trainers and gym administrators see the records their permissions allow, including member profiles, plans, measurements, nutrition logs, assessments, progress photos, attendance, and messages in conversations they belong to.",
      },
      {
        type: "p",
        text: "Service providers that process information for GrowFit. The application database runs on SQL Server. Account email is sent through the operator’s SMTP email service, which in this project is Hostinger. Push notifications are sent through Expo’s push service. On Android, delivery from that service uses Firebase Cloud Messaging. These providers act for the operation of GrowFit. GrowFit does not sell personal information and does not share it with advertising networks.",
      },
      {
        type: "p",
        text: "Optional tools. If a business connects Google Drive, progress photos for that business can be stored in the connected Drive account. If you use the AI coach, GrowFit sends a limited fitness context to the AI provider configured for that workspace. That context can include goal, fitness level, activity, units, preferred days and minutes, recent workout dates, calorie and protein totals against targets, and a challenge name. It is not a copy of every database row. The provider address is chosen in platform configuration, so this policy does not name one vendor.",
      },
      {
        type: "p",
        text: "GrowFit may disclose information when the law requires it, or when needed to protect users, the service, or the rights of others.",
      },
    ],
  },
  {
    id: "messaging",
    title: "Messaging and user content",
    blocks: [
      {
        type: "p",
        text: "Where chat is enabled for your account, messages and attachments are stored and delivered to the participants of that conversation. Who you can message depends on permissions, trainer or member relationships, and the gym’s settings.",
      },
      {
        type: "p",
        text: "Avoid putting sensitive health or account details in chat unless you need to. GrowFit does not run an automated review of messages before they are delivered. A person with access to the conversation can read what was sent.",
      },
    ],
  },
  {
    id: "notifications",
    title: "Push notifications",
    blocks: [
      {
        type: "p",
        text: "If you allow notifications, the app registers the device with an Expo push token. GrowFit can then send notices for chat messages, workout reminders, workout updates, progress, circulars, membership reminders, staff-leave updates, trainer assignment, and other account notices.",
      },
      {
        type: "p",
        text: "You can turn those categories, sound, and vibration off in GrowFit settings. The phone’s own notification permission can block delivery even when the in-app setting is on. The push request sent to Expo includes the token, a title, a short body, and a data payload that identifies the notice. It does not include your password.",
      },
    ],
  },
  {
    id: "third-parties",
    title: "Third-party services",
    blocks: [
      {
        type: "p",
        text: "These services are the ones confirmed in the current GrowFit and Growventur implementation:",
      },
      {
        type: "ul",
        items: [
          "Expo push notifications, used to deliver device notifications: https://expo.dev/privacy",
          "Firebase Cloud Messaging, used for Android notification delivery through Expo: https://firebase.google.com/support/privacy",
          "Hostinger SMTP, used to send account email: https://www.hostinger.com/legal/privacy-policy",
          "Google Drive, only when a business connects it for file storage: https://policies.google.com/privacy",
          "An operator-configured AI provider with an OpenAI-compatible API, only when someone uses the AI coach. No single vendor is fixed in the app.",
        ],
      },
      {
        type: "p",
        text: "No third-party advertising or product-analytics SDK was found in the mobile app.",
      },
    ],
  },
  {
    id: "security",
    title: "Data storage and security",
    blocks: [
      {
        type: "p",
        text: "The production app talks to the Growventur API over HTTPS. Passwords are stored as a hash and salt. Refresh tokens and password-reset tokens are stored as hashes, not as the raw token. Access to gym and fitness records is checked with permissions and limited to the current workspace.",
      },
      {
        type: "p",
        text: "These are ordinary safeguards for running the service. GrowFit does not claim encryption at rest, end-to-end encryption of chat, or a security certification. No online service can guarantee that information will never be accessed without authorization.",
      },
    ],
  },
  {
    id: "retention",
    title: "Data retention",
    blocks: [
      {
        type: "p",
        text: "Information is kept while it is needed to provide GrowFit, keep the account, support the gym workspace, meet a legal duty, resolve a dispute, or protect the service. The project does not define a single retention period for every record.",
      },
      {
        type: "p",
        text: "Some records are marked deleted rather than erased immediately. A deletion request does not mean every copy, backup, or gym operational record disappears at once.",
      },
    ],
  },
  {
    id: "deletion",
    title: "Account deletion",
    blocks: [
      {
        type: "p",
        text: "GrowFit lets you sign out. It does not currently include a self-service control that deletes your account. An authorized business administrator can remove a member, trainer, or staff profile from that gym.",
      },
      {
        type: "p",
        text: "You can ask for deletion of your account and the personal information associated with it by contacting the address below. This page does not publish a separate deletion link. There is no fixed promise that every related gym record will be erased.",
      },
    ],
  },
  {
    id: "children",
    title: "Children’s privacy",
    blocks: [
      {
        type: "p",
        text: "GrowFit is a fitness app for members, trainers, and gym staff. It is not directed at children. Registration does not apply a minimum age. Date of birth is collected only if you enter it in the fitness assessment.",
      },
      {
        type: "p",
        text: "A parent or guardian who believes a child has provided personal information can contact the address below and ask for that information to be deleted. The operator still needs to confirm the age rule that should be shown here.",
      },
    ],
  },
  {
    id: "rights",
    title: "Privacy requests",
    blocks: [
      {
        type: "p",
        text: "You can contact the address below to ask for access to, correction of, or deletion of your information. What can be done depends on applicable law and on records the gym still needs to operate, such as a membership or payment record. GrowFit does not offer a separate in-app privacy-request form.",
      },
    ],
  },
  {
    id: "international",
    title: "International processing",
    blocks: [
      {
        type: "p",
        text: "Information may be stored and processed where the Growventur operators and the service providers above run their systems. This policy does not name a country or a hosting region, because those locations are not fixed in the application code.",
      },
    ],
  },
  {
    id: "changes",
    title: "Changes to this policy",
    blocks: [
      {
        type: "p",
        text: "This policy can be updated. The current version is always the one on this page, and the “Last updated” date changes when it is revised. Continued use of GrowFit after an update is published means you accept the updated policy.",
      },
    ],
  },
  {
    id: "contact",
    title: "Contact",
    blocks: [
      {
        type: "p",
        text: "Privacy questions, access requests, and deletion requests can be sent to the official GrowFit support address shown below.",
      },
    ],
  },
];
