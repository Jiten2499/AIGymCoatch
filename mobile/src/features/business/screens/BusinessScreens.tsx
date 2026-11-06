import { FoundationScreen } from '../../shell/FoundationScreen';
import { BusinessDashboardScreen } from './BusinessDashboardScreen';

export { BusinessDashboardScreen };

export function StaffScreen() {
  return <FoundationScreen titleKey="nav.staff" />;
}

export function TrainersScreen() {
  return <FoundationScreen titleKey="nav.trainers" />;
}

export function AttendanceScreen() {
  return <FoundationScreen titleKey="nav.attendance" />;
}

export function BusinessSettingsScreen() {
  return <FoundationScreen titleKey="nav.businessSettings" />;
}
