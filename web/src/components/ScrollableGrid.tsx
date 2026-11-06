export function ScrollableGrid({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={`max-h-[min(28rem,55vh)] min-w-0 w-full overflow-auto overscroll-contain ${className ?? ""}`}>
      <div className="w-max min-w-full">{children}</div>
    </div>
  );
}
