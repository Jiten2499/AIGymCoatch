import { FormField } from "../../../components/FormDialog";
import { formatWallClock } from "./schedule";

export function ScheduleFields({
  mode,
  publishAt,
  timeZone,
  onMode,
  onPublishAt,
}: {
  mode: "draft" | "now" | "later";
  publishAt: string;
  timeZone: string;
  onMode: (mode: "draft" | "now" | "later") => void;
  onPublishAt: (value: string) => void;
}) {
  return (
    <>
      <fieldset className="grid gap-2">
        <legend className="text-sm text-muted">Publish</legend>
        <label className="flex min-h-11 items-center gap-2"><input checked={mode === "now"} name="publish" onChange={() => onMode("now")} type="radio" /> Now</label>
        <label className="flex min-h-11 items-center gap-2"><input checked={mode === "later"} name="publish" onChange={() => onMode("later")} type="radio" /> Schedule for later</label>
        <label className="flex min-h-11 items-center gap-2"><input checked={mode === "draft"} name="publish" onChange={() => onMode("draft")} type="radio" /> Save as draft</label>
      </fieldset>
      {mode === "later" ? (
        <>
          <FormField label="Date and time"><input aria-label="Schedule date and time" className="field" onChange={(event) => onPublishAt(event.target.value)} type="datetime-local" value={publishAt} /></FormField>
          {publishAt ? <p className="text-sm">Scheduled for: {formatWallClock(publishAt)}<br />Timezone: {timeZone}</p> : null}
        </>
      ) : null}
    </>
  );
}
