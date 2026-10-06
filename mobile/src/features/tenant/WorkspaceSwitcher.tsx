import { Pressable, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../app/providers/ThemeProvider';
import { AppText } from '../../components/ui/AppText';
import { TenantLogo } from '../../components/ui/TenantLogo';
import { resolveBrand } from '../../services/branding/branding';
import { usePreferencesStore } from '../../store/preferencesStore';
import { useSessionStore } from '../../store/sessionStore';
import { useWorkspaceStore } from '../../store/workspaceStore';
import type { WorkspaceSummary } from '../../types/api';

export function WorkspaceSwitcher({ onSelect }: { onSelect: (workspace: WorkspaceSummary) => void }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const current = useSessionStore((state) => state.currentTenant);
  const accent = usePreferencesStore((state) => state.accent);
  const workspaces = useWorkspaceStore((state) => state.workspaces);
  const logoUrl = useWorkspaceStore((state) => state.logoUrl);
  return (
    <View accessibilityRole="list" style={{ gap: theme.spacing.md }}>
      <AppText variant="label">{t('workspace.switch')}</AppText>
      {workspaces.map((workspace) => {
        const brand = resolveBrand({
          tenantId: workspace.id,
          tenantName: workspace.name,
          tenantType: workspace.type,
          logoUrl: workspace.id === current?.id ? logoUrl : null,
          userAccent: accent,
        });
        const selected = workspace.id === current?.id;
        return (
          <Pressable
            accessibilityLabel={brand.organizationName}
            accessibilityRole="button"
            accessibilityState={{ selected }}
            key={workspace.id}
            onPress={() => onSelect(workspace)}
            style={{ alignItems: 'center', backgroundColor: selected ? theme.colors.surfaceMuted : theme.colors.surface, borderRadius: theme.radius.md, flexDirection: 'row', gap: theme.spacing.md, minHeight: theme.control.minTouch, padding: theme.spacing.md }}
          >
            <TenantLogo name={brand.organizationName} uri={brand.logoUrl} />
            <AppText>{brand.organizationName}</AppText>
          </Pressable>
        );
      })}
    </View>
  );
}
