const pillars = [
  {
    title: "Tenants",
    copy: "Personal coaches, gyms, yoga studios, and CrossFit boxes on one model.",
  },
  {
    title: "Operations",
    copy: "Branches, memberships, trainers, and attendance prepared as the domain core.",
  },
  {
    title: "Coaching",
    copy: "Workouts, nutrition, and AI assistance are reserved for the product slices ahead.",
  },
];

export default function Home() {
  return (
    <div className="flex flex-1 flex-col">
      <header className="mx-auto flex w-full max-w-6xl items-center justify-between px-6 py-6">
        <span className="text-sm font-semibold tracking-[0.18em] text-accent uppercase">
          LifeFit AI
        </span>
        <a className="text-sm text-accent" href="/login">
          Platform admin
        </a>
      </header>

      <main className="mx-auto flex w-full max-w-6xl flex-1 flex-col px-6 pb-20">
        <section className="max-w-3xl pt-16 pb-20">
          <p className="text-sm font-semibold tracking-[0.16em] text-accent uppercase">
            Fitness operations
          </p>
          <h1 className="mt-4 text-5xl leading-tight font-semibold tracking-tight sm:text-6xl">
            The operating system for modern fitness businesses.
          </h1>
          <p className="mt-6 max-w-2xl text-lg leading-8 text-muted">
            LifeFit AI is the platform shell for gyms, studios, and coaches. SuperAdmin platform
            administration is separate from the member application.
          </p>
        </section>

        <section className="grid gap-4 md:grid-cols-3">
          {pillars.map((pillar) => (
            <article key={pillar.title} className="rounded-3xl border border-line bg-surface p-6">
              <h2 className="text-xl font-semibold">{pillar.title}</h2>
              <p className="mt-3 leading-7 text-muted">{pillar.copy}</p>
            </article>
          ))}
        </section>
      </main>

      <footer className="border-t border-line px-6 py-6 text-sm text-muted">
        <div className="mx-auto w-full max-w-6xl">LifeFit AI</div>
      </footer>
    </div>
  );
}
