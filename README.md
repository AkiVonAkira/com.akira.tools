
# Akira Tools

A Unity package designed to streamline your development workflow with package management, folder organization, and powerful utilities.

![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue)
![License](https://img.shields.io/badge/license-MIT-green)
[![openupm](https://img.shields.io/npm/v/com.akira.tools?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.akira.tools/) [![openupm](https://img.shields.io/badge/dynamic/json?color=brightgreen&label=downloads&query=%24.downloads&suffix=%2Fmonth&url=https%3A%2F%2Fpackage.openupm.com%2Fdownloads%2Fpoint%2Flast-month%2Fcom.akira.tools)](https://openupm.com/packages/com.akira.tools/)


## Features

- **Auto Asset Prefixing**: Easily prefix most assets that are used in a Unity Project
- **Folder Creation**: Automatically create organized folder structures with presets (Standard, Mobile, VR, etc.)
- **Package Management**: Install and manage Unity packages through a user-friendly interface with Git URL support
- **Import Helper Codes**: A popup interface for entering namespaces when importing scripts
- **Error Handling**: Comprehensive error logging with retry logic
- **MVVM Architecture**: Clean separation of concerns for maintainability

## Usage

1. **Install the Package**: Add Akira Tools to your Unity project (see installation options below)
2. **Access Tools**: Navigate to `Window > Akira Tools > Tools Hub`
3. **Install Packages**: Use the Package Manager tab to install packages from Git URLs
4. **Create Folders**: Use the Folder Templates tab to quickly set up your project structure

## Installation

### via OpenUPM (Recommended)

The package is available on the [openupm registry](https://openupm.com). You can install it via [openupm-cli](https://github.com/openupm/openupm-cli).

```bash
openupm add com.akira.tools
```

### via Unity Package Manager

1. Open **Edit/Project Settings/Package Manager**
2. Add a new Scoped Registry (or edit an existing OpenUPM entry):
   - **Name**: `package.openupm.com`
   - **URL**: `https://package.openupm.com`
   - **Scope(s)**: `com.akira.tools`
3. Click **Save** or **Apply**
4. Open **Window/Package Manager**
5. Click **+**
6. Select **Add package by name...**
7. Paste `com.akira.tools` into name field
8. Click **Add**

### via Git URL

1. Open **Window/Package Manager**
2. Click **+** button
3. Select **Add package from git URL...**
4. Enter: `https://github.com/AkiVonAkira/com.akira.tools.git`
5. Click **Add**

### via Local Package (Development)

1. Clone or download this repository
2. Open your Unity project's `Packages/manifest.json`
3. Add the following line to the `dependencies` section:
   ```json
   "com.akira.tools": "file:C:/Path/To/com.akira.tools"
   ```
4. Save the file and return to Unity

## Requirements

- **Unity Version**: 6000.0 or higher
- **Dependencies**: None (standalone package)

---

## Quick Start

### Opening the Tools Hub

Navigate to `Window > Akira Tools > Tools Hub` in Unity Editor

### Installing a Package

1. Go to the **📦 Package Manager** tab
2. Enter a Git URL (e.g., `https://github.com/KyryloKuzyk/PrimeTween.git`)
3. Click **Install Package**

### Creating Folder Structure

1. Go to the **📁 Folder Templates** tab
2. Select a preset from the dropdown (e.g., "Standard Project")
3. Set the root path (default: `Assets/_Project`)
4. Click **Create Folder Structure**

### Available Presets

- **Standard Project**: Complete Unity project structure (15 folders)
- **Mobile Game**: Optimized for mobile development (13 folders)
- **VR Project**: Virtual reality project setup (11 folders)
- **2D Platformer**: 2D game structure (12 folders)
- **Minimal**: Basic project structure (4 folders)

---

## Troubleshooting

### Package won't install
- Check your internet connection
- Verify the Git URL is correct
- Enable Debug Mode in Settings tab and check Console for detailed logs

### Folders not created
- Ensure the root path exists
- Check you have write permissions
- Make sure folders don't already exist

### Tools Hub won't open
- Restart Unity Editor
- Check Console for compilation errors
- Reimport the package if needed

---

## Future Plans

- All-In-One Editor UI to toggle certain features
- Package version comparison and updates
- Custom folder preset editor with visual tree builder
- Image Tool to create gradients and pixels for UI
- Dependency graph visualization

---

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests to enhance the functionality.

See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

1. Open the Tools Hub
2. Navigate to the **Folder Templates** tab
3. Select a preset template (e.g., "Standard Project")
4. Choose the root folder location
5. Click **Create Structure**
6. Your folder hierarchy will be created instantly

---

## 📸 Feature Overview

### Tools Hub Dashboard

The main hub provides quick access to:
- **Package Manager**: Install and manage packages
- **Folder Generator**: Create project structures
- **Settings**: Configure tool preferences
- **Asset Store Browser**: Browse and install Asset Store packages

```
┌─────────────────────────────────────┐
│         AKIRA TOOLS HUB            │
├─────────────────────────────────────┤
│ 📦 Package Manager                  │
│ 📁 Folder Templates                 │
│ ⚙️  Settings                        │
│ 🛒 Asset Store Browser              │
└─────────────────────────────────────┘
```

### Package Manager Features

- **Install from Git URL**: Direct installation from repositories
- **Install from Asset Store URL**: Parse and install Asset Store packages
- **Package List**: View all installed packages
- **Dependency Resolution**: Automatic handling of package dependencies
- **Error Recovery**: Retry failed installations automatically

### Folder Structure Presets

**Standard Project Template**
```
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Core/
│   │   ├── Utilities/
│   │   └── Gameplay/
│   ├── Prefabs/
│   ├── Materials/
│   ├── Textures/
│   ├── Models/
│   ├── Audio/
│   │   ├── Music/
│   │   └── SFX/
│   ├── Scenes/
│   └── Resources/
└── Plugins/
```

**Mobile Game Template**
```
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Managers/
│   │   ├── UI/
│   │   ├── Gameplay/
│   │   └── Data/
│   ├── UI/
│   │   ├── Sprites/
│   │   ├── Prefabs/
│   │   └── Fonts/
│   ├── Levels/
│   └── Resources/
```

---

## 💡 Usage Examples

### Example 1: Installing a Package from Git

```csharp
using Akira.Tools.Services;

public class PackageInstallExample
{
    public void InstallPrimeTween()
    {
        var packageService = new PackageService();
        packageService.InstallPackageFromGit(
            "https://github.com/KyryloKuzyk/PrimeTween.git",
            (success, message) => {
                if (success)
                    Debug.Log("Package installed successfully!");
                else
                    Debug.LogError($"Installation failed: {message}");
            }
        );
    }
}
```

### Example 2: Creating Custom Folder Structure

```csharp
using Akira.Tools.Services;
using System.Collections.Generic;

public class FolderStructureExample
{
    public void CreateCustomStructure()
    {
        var folderService = new FolderService();
        var folders = new List<string>
        {
            "Assets/_MyGame/Scripts",
            "Assets/_MyGame/Prefabs",
            "Assets/_MyGame/Scenes",
            "Assets/_MyGame/Audio/Music",
            "Assets/_MyGame/Audio/SFX"
        };
        
        folderService.CreateFolderStructure(folders, (success, message) => {
            if (success)
                Debug.Log("Folder structure created!");
        });
    }
}
```

### Example 3: Using Error Handling

```csharp
using Akira.Tools.Core;

public class ErrorHandlingExample
{
    public void SafeOperation()
    {
        ErrorHandler.Try(() =>
        {
            // Your risky operation here
            PerformComplexOperation();
        },
        (exception) =>
        {
            ErrorHandler.LogError($"Operation failed: {exception.Message}");
        });
    }
}
```

---

## 🔧 Troubleshooting

### Common Issues

#### Issue: Package Installation Fails

**Symptoms**: Error message when trying to install a package
**Solutions**:
1. Check your internet connection
2. Verify the Git URL is correct
3. Check Unity Console for detailed error messages
4. Try restarting Unity Editor
5. Clear Package Manager cache: `Edit > Preferences > Package Manager > Reset Packages to defaults`

#### Issue: Folder Creation Fails

**Symptoms**: Folders not created or permission errors
**Solutions**:
1. Ensure you have write permissions in the project directory
2. Check if folders already exist (tool won't overwrite)
3. Close Unity Hub if it's locking folders
4. Run Unity as Administrator (Windows)

#### Issue: Tools Hub Window Won't Open

**Symptoms**: Menu item doesn't respond or window doesn't appear
**Solutions**:
1. Restart Unity Editor
2. Reimport the package: Right-click on package in Project > Reimport
3. Check Console for script compilation errors
4. Verify package installation in Package Manager

#### Issue: Asset Store URL Not Parsing

**Symptoms**: Asset Store links not recognized
**Solutions**:
1. Ensure URL is from the Unity Asset Store domain
2. Copy the full URL including the package ID
3. Try using the package ID directly
4. Check if the package is publicly available

### Error Messages

| Error Code | Message | Solution |
|-----------|---------|----------|
| PKG001 | "Failed to resolve package dependencies" | Check manifest.json for conflicts |
| PKG002 | "Git URL is invalid" | Verify Git URL format |
| FLD001 | "Failed to create folder structure" | Check write permissions |
| FLD002 | "Folder already exists" | Use a different root path |
| NET001 | "Network operation timed out" | Check internet connection |
| NET002 | "Maximum retry attempts reached" | Try again later |

### Getting Help

- **Documentation**: [Full Documentation](https://github.com/yourusername/com.akira.tools/wiki)
- **Issues**: [GitHub Issues](https://github.com/yourusername/com.akira.tools/issues)
- **Discussions**: [Community Forum](https://github.com/yourusername/com.akira.tools/discussions)
- **Email**: support@akiratools.com

### Debug Mode

Enable debug logging for detailed troubleshooting:

1. Open `Window > Akira Tools > Settings`
2. Enable `Debug Mode`
3. Reproduce the issue
4. Check Console for detailed logs prefixed with `[AkiraTools]`

---

## 📚 API Reference

### PackageService

```csharp
public class PackageService
{
    // Install package from Git URL
    public void InstallPackageFromGit(string gitUrl, Action<bool, string> callback);
    
    // Install package by name
    public void InstallPackage(string packageName, Action<bool, string> callback);
    
    // Remove package
    public void RemovePackage(string packageName, Action<bool, string> callback);
    
    // Get installed packages
    public List<PackageInfo> GetInstalledPackages();
}
```

### FolderService

```csharp
public class FolderService
{
    // Create folder structure from list
    public void CreateFolderStructure(List<string> folders, Action<bool, string> callback);
    
    // Create from preset
    public void CreateFromPreset(FolderPreset preset, string rootPath);
    
    // Save custom preset
    public void SavePreset(FolderPreset preset);
}
```

### ErrorHandler

```csharp
public static class ErrorHandler
{
    // Execute with error handling
    public static void Try(Action action, Action<Exception> onError = null);
    
    // Log error with prefix
    public static void LogError(string message);
    
    // Log warning with prefix
    public static void LogWarning(string message);
    
    // Log info with prefix
    public static void Log(string message);
}
```

---

## 🤝 Contributing

Contributions are welcome! Please read our [Contributing Guide](CONTRIBUTING.md) for details on:
- Code of Conduct
- Development process
- Submitting pull requests
- Coding standards

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- Unity Technologies for the Unity Engine
- The Unity community for inspiration and feedback
- All contributors who help improve this project

---

## 📮 Contact

- **Author**: Akira Tools Team
- **GitHub**: [@yourusername](https://github.com/yourusername)
- **Website**: [akiratools.com](https://akiratools.com)
- **Discord**: [Join our community](https://discord.gg/akiratools)

---

**Made with ❤️ for the Unity Community**

