import { isMuscleGroupId, type MuscleGroupId } from './catalog';

const muscleGroupImages: Record<MuscleGroupId, number> = {
  chest: require('../../../assets/muscle-groups/chest.png'),
  upper_back: require('../../../assets/muscle-groups/upper_back.png'),
  lats: require('../../../assets/muscle-groups/lats.png'),
  middle_back: require('../../../assets/muscle-groups/middle_back.png'),
  lower_back: require('../../../assets/muscle-groups/lower_back.png'),
  shoulders: require('../../../assets/muscle-groups/shoulders.png'),
  biceps: require('../../../assets/muscle-groups/biceps.png'),
  triceps: require('../../../assets/muscle-groups/triceps.png'),
  forearms: require('../../../assets/muscle-groups/forearms.png'),
  core_abs: require('../../../assets/muscle-groups/core_abs.png'),
  obliques: require('../../../assets/muscle-groups/obliques.png'),
  serratus_anterior: require('../../../assets/muscle-groups/serratus_anterior.png'),
  quadriceps: require('../../../assets/muscle-groups/quadriceps.png'),
  hamstrings: require('../../../assets/muscle-groups/hamstrings.png'),
  glutes: require('../../../assets/muscle-groups/glutes.png'),
  calves: require('../../../assets/muscle-groups/calves.png'),
  tibialis_anterior: require('../../../assets/muscle-groups/tibialis_anterior.png'),
  adductors: require('../../../assets/muscle-groups/adductors.png'),
  abductors: require('../../../assets/muscle-groups/abductors.png'),
  lower_traps: require('../../../assets/muscle-groups/lower_traps.png'),
};

export function muscleGroupImage(id: string): number | undefined {
  if (!isMuscleGroupId(id)) return undefined;
  const source = muscleGroupImages[id];
  return source == null ? undefined : source;
}
