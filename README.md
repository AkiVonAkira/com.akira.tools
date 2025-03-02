[![openupm](https://img.shields.io/npm/v/com.akira.tools?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.akira.tools/) [![openupm](https://img.shields.io/badge/dynamic/json?color=brightgreen&label=downloads&query=%24.downloads&suffix=%2Fmonth&url=https%3A%2F%2Fpackage.openupm.com%2Fdownloads%2Fpoint%2Flast-month%2Fcom.akira.tools)](https://openupm.com/packages/com.akira.tools/)

# Akira Tools

This is my Unity package designed to streamline the organization and management of files and folders in a new Unity Project. This package provides various utilities for creating folder structures, managing Unity packages, and importing essential assets.

## Features

- **Auto Asset Prefixing**: Easily prefixes most Assets that ure used in a Unity Project. Has been tested and used by a group of 13 developers for 3 weeks.
- **Folder Creation**: Automatically create organized folder structures based on type or functionality.
- **Package Management**: Easily install and manage Unity packages through a user-friendly interface.
- **Import Helper Codes (Singleton)**: A popup interface for entering namespaces when importing scripts.

## Usage

1. **Install the Package**: Add the Akira Tools package to your Unity project.
2. **Access Tools**: Navigate to the Tools menu in Unity to access folder creation and package management options.

## Future Plans

- All-In-One Editor UI to toggle certain features, select what you want.
- Image Tool to create gradients and pixels for UI.

## Installation

### CLI:
**OpenUPM**: ```openupm add com.akira.tools```

### Manual via Package Manager
-  open **Edit/Project Settings/Package Manager**
-  add a new Scoped Registry (or edit an existing OpenUPM entry)
    - **Name** `package.openupm.com`
    - **URL** `https://package.openupm.com`
    - **Scope(s)** `com.akira.tools`

- click `Save` or `Apply`
- open **Window/Package Manager**
- click `+`
- select `Add package by name...` or `Add package from git URL...`
- paste `com.akira.tools` into name
- click `Add`

## Contribution

Contributions are welcome! Please feel free to submit issues or pull requests to enhance the functionality.

## License

This project is licensed under the MIT License. See the LICENSE file for more details.