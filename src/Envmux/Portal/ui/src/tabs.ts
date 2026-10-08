/** A tab identity survives a refresh, but belongs only to this browser tab. */
export interface ShellTab {
  id: string
  kind: "shell"
  tool?: string
  label: string
}

export function restoreShellTabs(storage: Pick<Storage, "getItem">, key: string, navigation: PerformanceNavigationTiming["type"]): ShellTab[] {
  // Browsers can copy sessionStorage when duplicating a tab. A fresh navigation
  // must choose fresh terminal IDs rather than attach to the original page.
  if (navigation !== "reload" && navigation !== "back_forward") return []
  try {
    const value: unknown = JSON.parse(storage.getItem(key) ?? "[]")
    if (!Array.isArray(value)) return []
    const seen = new Set<string>()
    return value.filter((tab): tab is ShellTab => {
      if (!tab || typeof tab !== "object" || tab.kind !== "shell" ||
          typeof tab.id !== "string" || !tab.id.startsWith("shell:") || tab.id.length > 128 ||
          typeof tab.label !== "string" || tab.label.length > 128 ||
          (tab.tool !== undefined && typeof tab.tool !== "string") || seen.has(tab.id)) return false
      seen.add(tab.id)
      return true
    })
  } catch {
    // Private browsing or an unavailable storage area must not prevent shells.
    return []
  }
}

export function saveShellTabs(storage: Pick<Storage, "setItem">, key: string, tabs: ShellTab[]): void {
  try {
    storage.setItem(key, JSON.stringify(tabs))
  } catch {
    // Reconnecting within the page still works when refresh persistence cannot.
  }
}
