export const growfitTermsUpdated = "21 November 2026";

/** Set this to the official public GrowFit support address before store submission. */
export const growfitSupportEmail = "";

export type TermsBlock =
  | { type: "p"; text: string }
  | { type: "ul"; items: string[] };

export type TermsSection = {
  id: string;
  title: string;
  blocks: TermsBlock[];
};

export const growfitTerms: TermsSection[] = [
  {
    id: "acceptance",
    title: "Introduction and acceptance",
    blocks: [
      {
        type: "p",
        text: "These Terms of Service (“Terms”) govern your access to and use of the GrowFit mobile application and the related services made available through it (together, “GrowFit”). GrowFit is offered in connection with the Growventur platform.",
      },
      {
        type: "p",
        text: "By downloading, creating an account, signing in, or otherwise using GrowFit, you agree to these Terms. If you do not agree, do not use GrowFit.",
      },
    ],
  },
  {
    id: "about",
    title: "About GrowFit",
    blocks: [
      {
        type: "p",
        text: "GrowFit is a fitness application that can support workout plans, exercise tracking, progress tracking, nutrition-related information, communication with trainers, and other fitness features that are turned on for your account.",
      },
      {
        type: "p",
        text: "Not every feature is available to every person. What you can see and use may depend on your gym or other organization, your trainer, your membership or subscription, and the permissions on your account.",
      },
    ],
  },
  {
    id: "eligibility",
    title: "Eligibility and accounts",
    blocks: [
      {
        type: "p",
        text: "You agree to give accurate account information and to keep it up to date. You are responsible for protecting your login credentials and for activity carried out through your account.",
      },
      {
        type: "p",
        text: "Tell us promptly if you believe someone has accessed your account without permission. Use the contact method in these Terms, or any in-app option that is available to you.",
      },
      {
        type: "p",
        text: "Access to particular features may be limited or removed based on your gym, trainer, membership, subscription, or account permissions. A gym or organization may also control whether your account can use its workspace.",
      },
    ],
  },
  {
    id: "health",
    title: "Fitness and health disclaimer",
    blocks: [
      {
        type: "p",
        text: "GrowFit is not a medical service. Workout plans, nutrition information, and fitness suggestions in the app are informational and fitness-oriented. They are not medical advice, diagnosis, or treatment.",
      },
      {
        type: "p",
        text: "Consult a qualified healthcare professional before you start an exercise program, change how you train, or rely on nutrition information, especially if you are pregnant, have an injury, or have a medical condition. You exercise at your own risk.",
      },
      {
        type: "p",
        text: "Stop exercising and seek appropriate medical help if you feel pain, dizziness, shortness of breath, chest discomfort, or any other symptom that concerns you.",
      },
    ],
  },
  {
    id: "workouts",
    title: "Workout plans and fitness content",
    blocks: [
      {
        type: "p",
        text: "Workout plans may be assigned by a trainer or gym, or created with the planning tools that are available on your account. Exercises, recommendations, and other content can change, and a plan that exists for one person may not be offered to another.",
      },
      {
        type: "p",
        text: "You are responsible for performing exercises safely, including choosing an appropriate load, range of motion, and environment. Follow a plan only to the extent it is suitable for you.",
      },
      {
        type: "p",
        text: "GrowFit does not guarantee any fitness outcome, including weight loss, muscle gain, performance improvement, or a particular health result.",
      },
    ],
  },
  {
    id: "trainers",
    title: "Trainers, gyms, and organizations",
    blocks: [
      {
        type: "p",
        text: "Trainers may use GrowFit to provide plans, guidance, and feedback on progress. A gym or other organization may decide who can use its features and what those people can see.",
      },
      {
        type: "p",
        text: "GrowFit provides software tools for fitness management and communication. It does not replace your own judgment about a trainer’s advice, a gym’s rules, or whether an activity is safe for you. Seek medical advice when that is appropriate.",
      },
      {
        type: "p",
        text: "Guidance from a trainer or gym is their guidance. GrowFit is not automatically responsible for the professional services those people or organizations choose to provide.",
      },
    ],
  },
  {
    id: "conduct",
    title: "User conduct",
    blocks: [
      {
        type: "p",
        text: "Use GrowFit lawfully and with respect for other people. You must not:",
      },
      {
        type: "ul",
        items: [
          "Abuse, threaten, harass, or discriminate against another person.",
          "Attempt to access an account, workspace, or system without authorization.",
          "Share malware or other harmful content.",
          "Impersonate another person or organization.",
          "Interfere with the app or misuse its features.",
          "Violate another person’s privacy.",
          "Use GrowFit for an illegal purpose.",
        ],
      },
    ],
  },
  {
    id: "messaging",
    title: "Communication and messaging",
    blocks: [
      {
        type: "p",
        text: "Where chat or another messaging feature is enabled for your account, use it respectfully. Do not send content that is unlawful, abusive, threatening, harassing, or otherwise harmful.",
      },
      {
        type: "p",
        text: "Whether you can message a particular person can depend on your permissions, your relationship with a trainer or member, and settings chosen by your gym or organization. Messaging may be absent entirely when the feature is not enabled.",
      },
      {
        type: "p",
        text: "GrowFit does not promise that messages are reviewed before they are delivered. If misuse comes to its attention, GrowFit may remove content, limit messaging, or restrict the account involved, according to these Terms and its operational ability to do so.",
      },
    ],
  },
  {
    id: "privacy",
    title: "Data privacy",
    blocks: [
      {
        type: "p",
        text: "Information you provide through GrowFit, including account and fitness information, is handled in accordance with the GrowFit Privacy Policy.",
      },
    ],
  },
  {
    id: "availability",
    title: "Availability and changes to the service",
    blocks: [
      {
        type: "p",
        text: "GrowFit may add, change, or discontinue features. A feature may be unavailable because of your internet connection, maintenance, your account status, or permissions set by your gym or organization.",
      },
      {
        type: "p",
        text: "The service can be interrupted. GrowFit does not promise uninterrupted or error-free operation.",
      },
    ],
  },
  {
    id: "suspension",
    title: "Suspension, termination, and leaving",
    blocks: [
      {
        type: "p",
        text: "Access may be limited, suspended, or ended if you misuse GrowFit, violate these Terms, create a security risk, or if there is another legitimate reason. A legitimate reason can include protecting other users or applying an access change requested by your gym or organization.",
      },
      {
        type: "p",
        text: "You may stop using GrowFit at any time. You may request deletion of your account through any account-deletion control the app makes available to you. This page does not provide a separate deletion link.",
      },
    ],
  },
  {
    id: "intellectual-property",
    title: "Intellectual property",
    blocks: [
      {
        type: "p",
        text: "The GrowFit application, the Growventur name and branding, the software, the visual design, and other proprietary content are protected by intellectual property laws. Those rights stay with their owners.",
      },
      {
        type: "p",
        text: "You receive a limited, personal, non-exclusive, revocable right to use GrowFit for its intended fitness purpose. You may not copy, redistribute, reverse engineer except where applicable law allows it, or commercially exploit the proprietary software, branding, or content without permission.",
      },
      {
        type: "p",
        text: "You keep any rights you already have in content you submit, such as a message or a progress note. You give the operators of GrowFit permission to host and display that content as needed to provide the features you use.",
      },
    ],
  },
  {
    id: "liability",
    title: "Limitation of liability",
    blocks: [
      {
        type: "p",
        text: "To the fullest extent permitted by applicable law, GrowFit and the people and organizations that operate it are not liable for indirect, incidental, special, consequential, or punitive damages, or for loss of profits, data, goodwill, or expected fitness results, arising out of your use of or inability to use GrowFit.",
      },
      {
        type: "p",
        text: "Physical training has inherent risks. You are responsible for deciding whether an exercise is appropriate and for how you perform it. GrowFit is made available on an “as available” basis.",
      },
      {
        type: "p",
        text: "Nothing in these Terms excludes or limits liability that applicable law does not allow to be excluded or limited. That includes liability for fraud, and for death or personal injury caused by negligence, where the law does not allow that liability to be excluded.",
      },
    ],
  },
  {
    id: "indemnification",
    title: "Indemnification",
    blocks: [
      {
        type: "p",
        text: "To the extent permitted by applicable law, you agree to indemnify and hold harmless the operators of GrowFit from claims, losses, damages, and reasonable expenses that arise from your misuse of GrowFit, content you submit, or your violation of these Terms or of another person’s rights.",
      },
      {
        type: "p",
        text: "This section does not require you to cover another party’s fraud or wilful misconduct, or any liability that applicable law does not allow to be shifted to you.",
      },
    ],
  },
  {
    id: "changes",
    title: "Changes to these terms",
    blocks: [
      {
        type: "p",
        text: "These Terms may be updated. The latest version will be published on this page, and the “Last updated” date will change when that happens.",
      },
      {
        type: "p",
        text: "If you keep using GrowFit after an updated version is published, you accept the updated Terms. If you do not agree to an update, stop using GrowFit.",
      },
    ],
  },
  {
    id: "governing-law",
    title: "Governing law",
    blocks: [
      {
        type: "p",
        text: "These Terms are governed by the law that applies to the legal entity which operates GrowFit, together with any law that applies to you and cannot be waived. The courts or other forums that can hear a dispute are those permitted by that law.",
      },
      {
        type: "p",
        text: "The operating legal entity, its registered office, and a named jurisdiction are not stated on this page. Those details must be added by the operator before publication. Nothing here chooses a country, state, or court in their place.",
      },
    ],
  },
  {
    id: "contact",
    title: "Contact",
    blocks: [
      {
        type: "p",
        text: "Questions about these Terms can be sent to the official GrowFit support address shown below.",
      },
    ],
  },
];
