import type { DocArticle } from "./types";

const updated = "2026-11-18";

export const growfitArticles: DocArticle[] = [
  {
    product: "growfit",
    slug: "getting-started/sign-in",
    title: "Sign in to GrowFit",
    description: "Open the phone app with the account your gym created.",
    category: "Getting started",
    roles: ["Member", "Trainer"],
    updated,
    popular: true,
    related: ["getting-started/navigation", "account/profile"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["GrowFit is the LifeFit phone app. Sign in uses the email and password for your existing account. Create account is only for a new personal registration. A gym should not ask a member to register twice."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Members and trainers whose workspace has given them app access."],
      },
      {
        heading: "Before you begin",
        bullets: ["Install GrowFit from the store build your gym uses.", "Allow notifications later if you want chat and circular alerts. The app still works if you skip that."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open GrowFit.", "Enter Email and Password.", "Choose Sign in.", "If you forgot the password, choose Forgot password, enter the email, and follow the message. The app says instructions are sent only if an account exists."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["A member lands on the member tabs. A trainer in a gym workspace lands on the trainer tabs: Home, My members, Plans, Chat when enabled, and More."],
      },
      {
        heading: "Common issues",
        paragraphs: ["Passwords need at least 12 characters with uppercase, lowercase, a number, and a symbol when you create or reset one. You do not have access to this area means the screen is for a different role."],
      },
    ],
  },
  {
    product: "growfit",
    slug: "getting-started/navigation",
    title: "Find your way around GrowFit",
    description: "Member tabs, trainer tabs, and the More menu.",
    category: "Getting started",
    roles: ["Member", "Trainer"],
    updated,
    popular: true,
    related: ["members/home-and-plans", "trainers/home", "account/settings"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["The tabs you see depend on whether you are signed in as a member or as a trainer. Chat is hidden when the business has not enabled it."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Every signed-in GrowFit user, with a different set of tabs."],
      },
      {
        heading: "Member tabs",
        bullets: ["Home", "Workouts", "Nutrition", "Progress", "Chat, when enabled", "More"],
      },
      {
        heading: "Trainer tabs",
        bullets: ["Home", "My members", "Plans", "Chat, when enabled", "More"],
      },
      {
        heading: "More menu",
        paragraphs: ["Members open Fitness, AI coach, Challenges, Profile, and Settings from More. Trainers open Profile, Notifications, Settings, schedule, performance, alerts, and targets. Leave items appear only when staff leave is enabled. Business tools such as Members, Staff, Leads, and Expenses appear on More only for people who also have business access."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["Switch workspace from the workspace control when you belong to more than one business. The current workspace decides which members and plans you see."],
      },
    ],
  },
  {
    product: "growfit",
    slug: "members/home-and-plans",
    title: "Use Home, workouts, and your plan",
    description: "See today's training and open the plan your coach or assessment created.",
    category: "Members",
    roles: ["Member"],
    updated,
    popular: true,
    related: ["members/assessment", "members/nutrition", "trainers/plans"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Home shows your current training snapshot. Workouts lists the plan. A plan is a set of days. Each day lists exercises. You can open an exercise, start a session, and finish on the summary."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Members. Trainers build plans from their own Plans tab. They do not use the member Workouts tab to edit someone else's plan."],
      },
      {
        heading: "Before you begin",
        paragraphs: ["You need a plan. Complete the fitness assessment and generate a plan, or wait until a trainer assigns one."],
      },
      {
        heading: "Step-by-step instructions",
        steps: [
          "Open Home to see what is scheduled.",
          "Open Workouts and choose the plan.",
          "Open a day, then an exercise, to read the sets and how to perform it.",
          "Start the session when you are ready to train.",
          "Finish the session. The summary records what you completed.",
          "Open workout history from the plan area when you want past sessions.",
        ],
      },
      {
        heading: "What happens next?",
        paragraphs: ["Completed work stays in history. Your trainer can see progress when they open you under My members."],
      },
      {
        heading: "Common issues",
        paragraphs: ["An empty Workouts tab means no plan is assigned yet. Nothing here yet is the normal empty state, not a broken screen."],
      },
    ],
  },
  {
    product: "growfit",
    slug: "members/assessment",
    title: "Complete the fitness assessment",
    description: "Answer the assessment so a plan can be personalised.",
    category: "Members",
    roles: ["Member"],
    updated,
    related: ["members/home-and-plans", "members/nutrition"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["The fitness and nutrition assessment asks about your body, goals, health, training, and diet. Steps are Personal, Goals, Health, Workout, Diet, Lifestyle, Measures, and Review."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Members. Open it from the plan introduction or Fitness on More."],
      },
      {
        heading: "Before you begin",
        paragraphs: ["Have your date of birth and recent measures if you want them included. You can save a draft and return."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open the assessment.", "Choose metric or imperial units.", "Answer each step. Use Previous to go back.", "Choose Save draft if you need to stop.", "On Review, choose Submit assessment.", "Continue to plan generation when the app offers it, and choose the plan length."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["The app confirms the assessment was submitted. A generated plan becomes the plan you open under Workouts. A trainer can still replace or edit that plan."],
      },
      {
        heading: "Common issues",
        paragraphs: ["If submit stays unavailable, a required answer on an earlier step is missing. Go back through the steps."],
      },
    ],
  },
  {
    product: "growfit",
    slug: "members/nutrition",
    title: "View nutrition and log a meal",
    description: "Open the nutrition plan, profile, history, and meal log.",
    category: "Members",
    roles: ["Member"],
    updated,
    related: ["members/assessment", "members/ai-coach"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Nutrition shows the plan prepared for you. From there you can open your nutrition profile, log a meal, review history, and open AI nutrition when that screen is available for your account."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Members, from the Nutrition tab."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open Nutrition.", "Read the daily plan, including the calories and meals it lists.", "Open Log meal to record what you ate.", "Open history to see earlier logs.", "Update the nutrition profile if your diet details changed."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["Logged meals stay in history. The plan itself changes when a new plan is generated or a trainer updates it. Logging food does not edit the plan."],
      },
      {
        heading: "Common issues",
        paragraphs: ["An empty plan means the assessment or trainer has not produced nutrition yet."],
      },
    ],
  },
  {
    product: "growfit",
    slug: "members/progress",
    title: "Track progress and measurements",
    description: "Record measurements and review progress.",
    category: "Members",
    roles: ["Member"],
    updated,
    related: ["members/home-and-plans", "trainers/member"],
    sections: [
      {
        heading: "Overview",
        paragraphs: ["Progress shows how training is going. Measurements is the place to add body measures. An insight screen explains progress when enough information exists."],
      },
      {
        heading: "Who can use this feature?",
        paragraphs: ["Members use the Progress tab. Trainers record a coached assessment from the member's progress screen, not from this tab."],
      },
      {
        heading: "Step-by-step instructions",
        steps: ["Open Progress.", "Review the charts and history.", "Open Measurements and save the new figures.", "Return to Progress to see the update."],
      },
      {
        heading: "What happens next?",
        paragraphs: ["Saved measures stay on your profile. Your trainer can see the progress recorded for you."],
      },
      {
        heading: "Common issues",
        paragraphs: ["Early on, progress can look empty. Complete a few sessions or add a measurement first."],
      },
    ],
  }
];
