Native Asset Store integration via Unity Package Manager internals

Overview
- Uses reflection to call UnityEditor.PackageManager.UI.Internal services for:
  - Listing owned Asset Store products
  - Checking ownership for a product ID
  - Getting a time-limited download URL and importing .unitypackage
  - Installing UPM packages (id/git) via Client.Add

Files
- ReflectionUtil.cs: helpers for safe reflection
- AssetStoreBridge.cs: reflection bridge into internal Asset Store REST API
- AssetStoreService.cs: public, editor-only API your UI can call
- UpmInstaller.cs: lightweight UPM installer wrapper
- AssetStoreWindow.cs: test window (Tools/Akira/Asset Store (Native))

Usage
- Open the test window under Tools/Akira/Asset Store (Native)
- Click "List Owned Assets" (requires Unity sign-in)
- Enter a Product ID to fetch details or to download/import the legacy package

Notes
- This relies on internal Unity APIs and can break across Editor versions.
- Requires the user to be signed in (Unity Connect).
- Downloaded .unitypackage files are saved under Temp/AssetStoreDownloads before import.
- For full ownership, use AssetStoreService.GetAllOwnedProducts to auto-page.

