# LifeFit AI design system

The mobile UI uses one token set in `mobile/src/theme`. Screens read the resolved theme through `useTheme`. They do not invent a second palette. Fitness, nutrition, coach, challenge, and notification screens reuse the same cards, buttons, and `ScreenState` loading, empty, and error treatment. Unread notifications include the word Unread as well as the visual state.

## Identity

The product should feel like a fitness studio that is also a careful piece of software: deep green surfaces, a lime accent, plain language, and enough space to scan a number or a name. Cards are flat with a hairline border. Shadows stay at elevation 1. Motion is short, and it is removed when the system asks for reduced motion.

Personal workspaces show the LifeFit name and a monogram. A business workspace shows that tenant's name and, when supplied, its remote logo. A broken logo falls back to the monogram. Logos are not bundled per tenant.

## Color

Dark surfaces start at `#07110d`, with cards at `#10211a`, text at `#f3f7f2`, and muted text at `#9aab9f`. Light surfaces are `#f4f7f4` and `#ffffff`, with ink `#10211a`.

Accents are a closed set:

| Token | Dark appearance | Light appearance |
| --- | --- | --- |
| lime | `#c8f54e` | `#4f6b10` |
| teal | `#14b8a6` | `#0f766e` |
| blue | `#3b82f6` | `#1d4ed8` |
| violet | `#8b5cf6` | `#6d28d9` |
| coral | `#f43f5e` | `#be123c` |

The light accent is darker so text and controls stay readable. Button labels use black or off-white based on the accent luminance. Users can pick one of these accents. They cannot supply CSS or an arbitrary hex value.

Appearance is `system`, `light`, or `dark`. The choice is stored on device until a user-preference API exists.

## Typography

The system font is used so the platform can scale it. Sizes are caption 12, label 13, body 16, title 22, and display 32. Weights are regular, medium, and bold. Body line height is 24.

## Space, radius, and elevation

Spacing is 4, 8, 12, 16, 24, 32, and 48. Radius is 8, 12, 16, or a pill. Touch targets are at least 44. Inputs are 48 tall. Icons are 16, 20, 24, or 32.

Z-index layers are base, sticky, dropdown, modal, and toast. Durations are 120, 200, and 320 milliseconds, or 0 when reduced motion is on.

Breakpoints for future layouts are 0, 600, and 840. The current shell is a phone portrait layout that also runs on tablets.

## Components

Shared pieces live under `mobile/src/components`:

- `AppText`, `AppButton`, `AppTextInput`, `AppCard`, `AppBadge`, `AppAvatar`, `TenantLogo`
- `FormField`
- `ScreenState`, `AppSkeleton`
- `AppSearch`, `AppList`
- `RequireAccess`

`ScreenState` covers loading, error with retry, and empty. `AppList` virtualizes rows and uses that state. `RequireAccess` hides a block when a UX gate is closed and shows a short denial. It is not authorization.

## Tenant branding

`resolveBrand` takes the active tenant id, name, type, optional logo URL, and the user's accent. A personal tenant always resolves to LifeFit AI and no remote logo, even if a logo URL was passed. A business tenant keeps its own name and URL. Switching workspaces clears the stored logo before the next tenant's profile can load, so one gym's mark cannot remain on another workspace.
