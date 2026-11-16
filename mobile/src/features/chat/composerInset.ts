export function composerBottomPadding(spacing: number, insetBottom: number, keyboardHeight: number, windowShrink: number) {
  const coveredByKeyboard = Math.max(0, keyboardHeight - Math.max(0, windowShrink));
  if (coveredByKeyboard > 0) {
    return spacing + coveredByKeyboard;
  }
  return spacing + Math.max(0, insetBottom);
}
