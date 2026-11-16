import { create } from 'zustand';
import type { WorkoutProgram } from '../fitness/types';
import { toDraft, type PlanDraft } from './planDraft';

type PlanEditState = {
  memberId: string;
  programId: string;
  baseline: PlanDraft | null;
  draft: PlanDraft | null;
  dirty: boolean;
  load: (memberId: string, programId: string, program: WorkoutProgram) => void;
  replace: (draft: PlanDraft) => void;
  revert: () => void;
  markPublished: () => void;
  clear: () => void;
};

export const usePlanEditStore = create<PlanEditState>((set, get) => ({
  memberId: '',
  programId: '',
  baseline: null,
  draft: null,
  dirty: false,
  load: (memberId, programId, program) => {
    const current = get();
    if (current.memberId === memberId && current.programId === programId && current.draft) {
      return;
    }
    const draft = toDraft(program);
    set({ memberId, programId, baseline: draft, draft, dirty: false });
  },
  replace: (draft) => set({ draft, dirty: true }),
  revert: () => set((state) => ({ draft: state.baseline, dirty: false })),
  markPublished: () => set((state) => ({ baseline: state.draft, dirty: false })),
  clear: () => set({ memberId: '', programId: '', baseline: null, draft: null, dirty: false }),
}));
