import { useState } from 'react';
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
import { activeWorkspaces, canSwitchWorkspaces } from './activeWorkspaces';

export function WorkspaceSwitcher({ onSelect }: { onSelect: (workspace: WorkspaceSummary) => void }) {
  const theme = useTheme();
  const { t } = useTranslation();
  const current = useSessionStore((state) => state.currentTenant);
  const accent = usePreferencesStore((state) => state.accent);
  const workspaces = useWorkspaceStore((state) => state.workspaces);
  const logoUrl = useWorkspaceStore((state) => state.logoUrl);
  const [open, setOpen] = useState(false);
  const choices = activeWorkspaces(workspaces);

  if (!canSwitchWorkspaces(workspaces)) {
    return null;
  }

  const currentWorkspace = choices.find((workspace) => workspace.id === current?.id) ?? choices[0];
  const currentBrand = resolveBrand({
    tenantId: currentWorkspace.id,
    tenantName: currentWorkspace.name,
    tenantType: currentWorkspace.type,
    logoUrl: currentWorkspace.id === current?.id ? logoUrl : null,
    userAccent: accent,
  });

  return (
    <View style={{ gap: theme.spacing.sm, zIndex: theme.zIndex.dropdown }}>
      <AppText variant="label">{t('workspace.switch')}</AppText>
      <Pressable
        accessibilityHint={t('workspace.openList')}
        accessibilityLabel={`${t('workspace.switch')}: ${currentBrand.organizationName}`}
        accessibilityRole="button"
        accessibilityState={{ expanded: open }}
        onPress={() => setOpen((value) => !value)}
        testID="workspace-switcher"
        style={{
          alignItems: 'center',
          backgroundColor: theme.colors.surface,
          borderColor: theme.colors.border,
          borderRadius: theme.radius.md,
          borderWidth: 1,
          flexDirection: 'row',
          gap: theme.spacing.md,
          minHeight: theme.control.minTouch,
          paddingHorizontal: theme.spacing.md,
          paddingVertical: theme.spacing.sm,
        }}
      >
        <TenantLogo decorative name={currentBrand.organizationName} uri={currentBrand.logoUrl} />
        <AppText style={{ flex: 1 }}>{currentBrand.organizationName}</AppText>
        <AppText tone="muted">{open ? '▴' : '▾'}</AppText>
      </Pressable>
      {open ? (
        <View
          accessibilityRole="menu"
          style={{
            backgroundColor: theme.colors.surface,
            borderColor: theme.colors.border,
            borderRadius: theme.radius.md,
            borderWidth: 1,
            gap: theme.spacing.xs,
            padding: theme.spacing.sm,
          }}
        >
          {choices.map((workspace) => {
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
                accessibilityRole="menuitem"
                accessibilityState={{ selected }}
                key={workspace.id}
                onPress={() => {
                  setOpen(false);
                  if (!selected) {
                    onSelect(workspace);
                  }
                }}
                style={{
                  alignItems: 'center',
                  backgroundColor: selected ? theme.colors.surfaceMuted : theme.colors.surface,
                  borderRadius: theme.radius.md,
                  flexDirection: 'row',
                  gap: theme.spacing.md,
                  minHeight: theme.control.minTouch,
                  padding: theme.spacing.md,
                }}
              >
                <TenantLogo decorative name={brand.organizationName} uri={brand.logoUrl} />
                <AppText style={{ flex: 1 }}>{brand.organizationName}</AppText>
              </Pressable>
            );
          })}
        </View>
      ) : null}
    </View>
  );
}
