export const muscleGroupCatalog = [
  { id: 'chest', displayName: 'Chest', anatomicalName: 'Pectoralis major' },
  { id: 'upper_back', displayName: 'Upper back', anatomicalName: 'Upper trapezius' },
  { id: 'lats', displayName: 'Lats (back width)', anatomicalName: 'Latissimus dorsi' },
  { id: 'middle_back', displayName: 'Middle back', anatomicalName: 'Rhomboids' },
  { id: 'lower_back', displayName: 'Lower back', anatomicalName: 'Erector spinae' },
  { id: 'shoulders', displayName: 'Shoulders', anatomicalName: 'Anterior, lateral and posterior deltoids' },
  { id: 'biceps', displayName: 'Biceps', anatomicalName: 'Biceps brachii' },
  { id: 'triceps', displayName: 'Triceps', anatomicalName: 'Triceps brachii' },
  { id: 'forearms', displayName: 'Forearms', anatomicalName: 'Wrist flexors and extensors' },
  { id: 'core_abs', displayName: 'Core (abs)', anatomicalName: 'Rectus abdominis' },
  { id: 'obliques', displayName: 'Obliques', anatomicalName: 'External and internal obliques' },
  { id: 'serratus_anterior', displayName: 'Serratus anterior', anatomicalName: 'Serratus anterior' },
  { id: 'quadriceps', displayName: 'Quadriceps', anatomicalName: 'Quadriceps femoris group' },
  { id: 'hamstrings', displayName: 'Hamstrings', anatomicalName: 'Hamstring muscle group' },
  { id: 'glutes', displayName: 'Glutes', anatomicalName: 'Gluteus maximus, medius and minimus' },
  { id: 'calves', displayName: 'Calves', anatomicalName: 'Gastrocnemius and soleus' },
  { id: 'tibialis_anterior', displayName: 'Tibialis anterior', anatomicalName: 'Tibialis anterior' },
  { id: 'adductors', displayName: 'Adductors', anatomicalName: 'Hip adductor muscle group' },
  { id: 'abductors', displayName: 'Abductors', anatomicalName: 'Gluteus medius and minimus / hip abductors' },
  { id: 'lower_traps', displayName: 'Lower traps', anatomicalName: 'Lower trapezius' },
] as const;

export type MuscleGroupId = (typeof muscleGroupCatalog)[number]['id'];

export type MuscleGroup = (typeof muscleGroupCatalog)[number];

const byId = new Map<string, MuscleGroup>(muscleGroupCatalog.map((group) => [group.id, group]));

export function isMuscleGroupId(value: string): value is MuscleGroupId {
  return byId.has(value);
}

export function muscleGroupById(value: string): MuscleGroup | null {
  return byId.get(value) ?? null;
}

export function muscleGroupIdFromLabel(label: string | null | undefined): MuscleGroupId | null {
  if (!label) return null;
  const trimmed = label.trim();
  if (!trimmed) return null;
  if (isMuscleGroupId(trimmed)) return trimmed;
  const lower = trimmed.toLowerCase();
  const match = muscleGroupCatalog.find((group) => group.id === lower || group.displayName.toLowerCase() === lower);
  return match?.id ?? null;
}
