# Akira Tools – Documentation

This document provides a deeper, task‑oriented guide for using and extending the Akira Tools package. It complements the README by covering page flows, configuration, package installation behavior (including Git support), and extensibility hooks.

## Contents
- Overview
- Opening the Tools Hub
- Pages and Workflows
  - Manage Packages
  - Asset Store Packages
  - Customize Project Folders
  - Script Importer
  - Hidden GameObjects
- Git Package Support
- TextMesh Pro (TMP) Handling
- Settings: Asset Prefixing and Recent Renames
- Extending the Tools Hub (Menu Buttons & Pages)
- Package Manager Architecture and Extension Points
- Troubleshooting
- FAQ

---

## Overview
Akira Tools streamlines project setup and routine editor tasks:
- Create opinionated folder structures.
- Install Unity registry and Git packages reliably.
- Track Asset Store items and jump to purchase/import.
- Import code templates (e.g., Singleton, asmdefs) with namespacing.
- Inspect and manage hidden GameObjects.

The Tools Hub window groups features into clean sections for quick access.

## Opening the Tools Hub
- Menu: Tools > Akira Tools Hub
- The Tools Hub preloads its menu so you don’t need to hit Refresh on first open.
- Back/Forward navigation lets you move across opened pages.

## Pages and Workflows

### Manage Packages
Location: Tools Hub > Setup > Packages > “Manage Packages”

- See essential and recommended packages.
- Toggle items to include/exclude them from installation.
- Install/Update/Remove supported packages.
- Filter by text, enabled, essential, or installed.
- Git packages are labeled [Git]. IDs supported include registry names (e.g., com.unity.cinemachine) and Git URLs.
- Tag chips clarify status/details: Essential/Recommended, Git, Installed/Not Installed, version info (vX → vY), Requires Restart when applicable, plus Asset Store, Free, Paid, Preview, Deprecated, and Owned (for Asset Store entries) where detected.

Notes:
- TextMesh Pro resources are handled via import buttons; TMP is built-in and not removed as a package.
- Burst Compiler (com.unity.burst) shows a “Requires Restart” tag. After installing Burst, a toolbar notification recommends restarting the Editor for changes to fully apply.
- Status refresh runs automatically, with non-Git checks first to keep the UI responsive.
- Asset Store entries are managed on a separate page (see below) and are filtered out here.

### Asset Store Packages
Location: Tools Hub > Setup > Packages > “Asset Store Packages”

- Track Asset Store items with a display name, description, and the Asset Store URL.
- The title links to the Asset Store page when a URL is provided.
- Sign-in indicator shows if the Unity Editor account is signed in.
- Actions:
  - Sign In: opens the Unity ID sign-in page when not signed in.
  - View Page: opens the Asset Store listing in the browser.
  - Open Package Manager: enabled when item is Free or marked Owned; otherwise shows “Purchase to Install”.
  - Mark Owned/Clear Owned: quick toggle to track ownership in settings when signed in (helps gate install).
- Tag chips: Asset Store, Free/Paid (shows Price when provided), and Owned when you’ve marked ownership.

Tip: Ownership detection isn’t exposed via a stable public Editor API. Use the “Mark Owned” toggle to track ownership locally.

### Customize Project Folders
Location: Tools Hub > Setup > Folders > “Customize…”

- Enable/disable folders and add nested subfolders quickly.
- Delete Mode allows removing folders from the preset layout (protected folders are not removable).
- Save your current structure as a preset, load presets, or import/export as JSON.
- Apply Changes to create the selected folders under “Assets/_Project”.

### Script Importer
Location examples:
- Tools Hub > Scripts > “Singleton”
- Tools Hub > Scripts > “Assembly Definition” / “Editor Assembly Definition”

- Choose a namespace and output path; preview updates live.
- ASMDEF templates support selecting default locations (Runtime/Editor/Tests).
- Existing files are compared; identical content won’t be re-imported.

### Hidden GameObjects
Location: Tools Hub > Others > “Hidden GameObjects Tool”

- Lists objects hidden from the Hierarchy (HideFlags.HideInHierarchy).
- Select, toggle hidden state, or delete hidden objects.

## Git Package Support
You can add Git packages by:
- Using npm-style prefix: git+https://github.com/username/repo.git
- Using plain URLs: https://github.com/username/repo.git
- Other schemes: ssh://, git://

Behavior:
- Installation: The Package Manager normalizes IDs (strips git+) for Client.Add.
- Status: Installed Git packages are matched by normalized repo name or URL fragments.
- Removal: Git inputs are resolved to the canonical installed package name before calling Client.Remove.

Tip: For faster status checks, registry packages are processed first, then Git/URL packages.

## TextMesh Pro (TMP) Handling
- TMP is built into Unity; the page provides Import/Reimport TMP Resources.
- “Remove TMP Resources” deletes imported Assets/TextMesh Pro folders (not the built-in package).
- Conflicting TMP entries in the package list are removed automatically.

## Settings: Asset Prefixing and Recent Renames
- “Enable Asset Prefixing” toggle controls the auto-prefix behavior.
- When disabled, the “Recent Asset Renames” panel is hidden.
- You can configure how many recent renames to display.

## Extending the Tools Hub (Menu Buttons & Pages)
Add a button to the Tools Hub by annotating a static method with:

[akira.ToolsHub.MenuButtonItem(path, buttonText, tooltip = "", isPage = false)]

- path: e.g., "Setup/Packages" (nodes will be created as needed).
- buttonText: label shown in the grid.
- tooltip: optional hover text.
- isPage: set true to open a custom page instead of running an action.

Page wiring options (static methods on the same type):
- Draw(): If present, invoked to render content.
- <YourMethod>_Page(): Alternative page draw function.
- DrawFolderCustomizationPage(): Supported in FolderCustomizationPage for back-compat.

## Package Manager Architecture and Extension Points
Namespace: akira.Packages.PackageManager

Key capabilities:
- InstallPackages(IEnumerable<string>, IProgress<float>)
- InstallPackage(string, Action<float>)
- RemovePackage(string, Action<bool>)
- IsPackageInstalled(string): Task<bool>
- GetPackageInfo(string): Task<PackageInfo>
- HasPackageUpdate(string): Task<bool>
- GetInstalledPackages(): Task<Dictionary<string, PackageInfo>>
- RefreshPackageCache()/RefreshPackageCacheIfNeeded()

Events:
- OnPackageInstallProgress(packageId, progress)
- OnPackageInstallComplete(packageId, success)
- OnPackageInstallError(packageId)
- OnAllPackagesInstallComplete()

Design notes:
- Git/URL packages are normalized to support install/remove and status reliably.
- The cache refresh is throttled; a timeout guards against stalled list requests.

New: PackageEntry metadata for UI tags
- PackageEntry is now a first-class model (Editor/Packages/PackageEntry.cs) with optional metadata used to render tag chips:
  - IsAssetStore: forces an “Asset Store” tag
  - IsFree: true shows “Free”, false shows “Paid”; if Price is provided, a “Price $X” chip is shown
  - RequiresRestart: shows “Requires Restart”
  - IsOwned: shows “Owned” (used by Asset Store page to gate actions)
  - ExtraTags: free-form labels (e.g., “DOTS”, “Editor Only”)
- These augment built-in heuristics and known metadata; use ToolsHubSettings.AddOrUpdatePackage to persist them.

Future-friendly ideas:
- Abstract sources (Registry, Git, Local, Asset Store) via providers and an interface to plug new sources.
- Optional background worker for serial queueing, retry policies, and cancellation.
- User-visible logs/history for installs and removals.

## Troubleshooting
- “Loading menu…” persists: the window rebuilds its tree after domain reloads. If it doesn’t update, click the Refresh button once or reopen the window.
- Git install fails: verify the URL (ensure the repo is public or credentials are configured), and that Unity’s Git support is available.
- TMP buttons missing: ensure TextMesh Pro is present under Packages and the editor scripts are compiled.
- Asset Store Owned state: use “Mark Owned/Clear Owned” to toggle ownership for gating when signed in.

## FAQ
Q: Can I add my own page UI?
A: Yes. Use MenuButtonItem with isPage = true and implement one of the supported Draw methods on the declaring type.

Q: How can I add my organization’s internal Git packages?
A: Use a Git URL as the package ID. They’ll appear with the [Git] tag, and removal will resolve to the canonical name automatically.

Q: How do presets for folders work?
A: Use the Customize Folders page to save, export, import, and load JSON-based presets. Non-removable folders are protected.
