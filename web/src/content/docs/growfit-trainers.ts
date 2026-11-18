import type { DocArticle } from "./types";

const updated = "2026-11-18";

export const growfitTrainerArticles: DocArticle[] = [
  {
    product: "growfit",
    slug: "trainers/home",
    title: "Use the trainer home and member list",
    description: "See your coaching day and open an assigned member.",
    category: "Trainers",
    roles: ["Trainer"],
    updated,
    popular: true,
    related: ["trainers/plans", "trainers/member"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Trainer Home is the coaching day. My members lists the people assigned to you. Open a member for overview, plan, and progress."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Trainers. Members do not see My members."],
      },
      {
        heading: "Before you begin",
        paragraphs: ["The gym must assign the member to you in Growventur. An unassigned member does not appear on your list."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open Home.", "Open My members.", "Choose a member.", "Use the overview, plan, and progress areas on that member."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["What you save on the member's plan or progress is what they see in GrowFit and what the gym sees on the member record."],
      },
      {
        heading: "Common issues",
        paragraphs: ["An empty member list means nobody is assigned yet. Ask the gym to set Assigned trainer."],
      },
    ],
  },
  {
    product: "growfit",
    slug: "trainers/plans",
    title: "Build a member workout plan",
    description: "Edit days and exercises, then review the plan.",
    category: "Trainers",
    roles: ["Trainer"],
    updated,
    related: ["trainers/home", "members/home-and-plans"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Plans is the trainer list of programs. Inside a member plan you edit each day, pick exercises from the library, replace an exercise, and review the plan before it is the member's current program. Plan history keeps earlier versions."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Trainers, for members they can open."],
      },
      {
        heading: "Step-by-step instructions",
        steps: [
          "Open the member and choose their plan, or start from the Plans tab.",
          "Open a day.",
          "Add or replace an exercise from the exercise library.",
          "Set the work for that exercise and save.",
          "Open review and confirm the plan.",
          "Use plan history if you need an earlier version.",
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The member sees the updated plan under Workouts. They are not notified unless workout plan updates are allowed on their phone."],
      },
      {
        heading: "Common issues",
        paragraphs: ["If an exercise will not save, a required set or repetition field is empty. Return to that exercise."],
      },
    ],
  },
  {
    product: "growfit",
    slug: "trainers/member",
    title: "Record member progress",
    description: "Open a member's progress and save an assessment.",
    category: "Trainers",
    roles: ["Trainer"],
    updated,
    related: ["trainers/home", "members/progress"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["From a member, Progress opens the assessments already stored and starts a new one. The progress wizard walks through the measures you record for that person."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Trainers, for an assigned member."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open My members and choose the person.", "Open Progress.", "Start a new record or open an existing assessment.", "Complete the wizard and save."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The assessment is stored on that member. The member can see the related progress in their Progress tab."],
      },
      {
        heading: "Common issues",
        paragraphs: ["Pull to refresh on More if a member you just received is missing. Then check My members again."],
      },
    ],
  },
  {
    product: "growfit",
    slug: "trainers/more",
    title: "Schedule, targets, alerts, and time off",
    description: "Use the extra trainer tools on More.",
    category: "Trainers",
    roles: ["Trainer"],
    updated,
    related: ["trainers/home", "account/settings"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Trainer More shows your photo, specialisation, qualifications, experience, bio, and joining date when the gym entered them. Availability explains the coaching hours stored for you. Links open Notifications, schedule, performance, alerts, and targets. My Leaves and Apply Leave appear when staff leave is enabled."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Trainers."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open More.", "Read the profile card. Ask the gym to change qualifications if they are wrong.", "Open schedule, performance, alerts, or targets.", "Choose Apply Leave to request time off when that link is shown.", "Choose Log out when you are finished on a shared phone."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["A leave request goes to the business. A manager reviews it under Tasks in Growventur. Targets show the goals the gym stored for you."],
      },
      {
        heading: "Common issues",
        paragraphs: ["Missing leave links mean the business has not enabled staff leave. None under qualifications means the gym has not entered a certification."],
      },
    ],
  },
];
