# Contributing to hybrID
First off, thank you for considering contributing to hybrID! This tool is designed to make hybrid identity management frictionless, and community contributions are what keep it sharp.

This document outlines the architecture, coding standards, and workflow for contributing to the project.

## Project Architecture

hybrID is built using a modular Model-View-Controller (MVC) approach. We strictly separate UI markup, application logic, and configuration state to make the codebase maintainable and scalable.

```text
hybrID/
├── assets/              # GitHub repository assets (readmes, badges)
├── build/               # Build scripts (e.g., PS2EXE wrappers)
├── docs/                # Project documentation
├── hybrID/              # Core Application Directory
│   ├── assets/          # Static app resources (icons, images)
│   ├── config/          # Application state (config.json)
│   ├── private/         # Internal PowerShell logic & helper functions
│   ├── public/          # Main controller script (hybrID.ps1)
│   └── ui/              # XAML presentation layer files
├── tests/               # Pester tests
├── .gitignore
└── README.md
```

### Component Breakdown
#### `public/hybrID.ps1`  
The main controller. It initializes the environment, loads the global variables, dynamically imports the UI, and binds the events.

#### `private/*.ps1`  
The brains of the operation. Each distinct feature (e.g., Invoke-Search.ps1, Show-Settings.ps1) lives in its own file here.

#### `ui/*.xaml`  
The presentation layer. All UI layout must be done in these files, never hardcoded as strings inside PowerShell scripts.

#### `config/config.json`  
The persistent state file that holds user preferences (themes, FQDNs).

## Design & Theming Standards
hybrID features a dynamic theming engine (Dark, Light, and System). If you are adding new UI elements or windows, you must adhere to the following UI standards:

  - **No Hardcoded Hex Colors**: Never use hardcoded colors (e.g., Background="#1E1E1E") in the XAML. You must use the application's global DynamicResource tokens:
    - `{DynamicResource WindowBg}`: Base background for windows.
    - `{DynamicResource PanelBg}`: Elevated background for inner panels or popups.
    - `{DynamicResource InputBg}`: Background for TextBoxes and ComboBoxes.
    - `{DynamicResource BorderBrush}`: Borders and dividers.
    - `{DynamicResource PrimaryText}`: Main reading text.
    - `{DynamicResource SecondaryText}`: Labels and subtitles.
    - `{DynamicResource NotesText}`: Specifically for the AD Notes field.

  - **Rounded Corners**: Modern Windows UI standards apply. 
    - Use `<Border CornerRadius="6">` for buttons and input fields
    - Use `CornerRadius="8"` for larger panels.

  - **Icons**: Use the native Windows **Segoe MDL2 Assets** font for icons instead of emojis or external images. (e.g., `&#xE713;` for the Settings gear).
  - **Window Icons (Popups & Child Windows)**: Child windows spawned by the main window should always inherit its application icon in the Title Bar:
   ```powershell
   if ($global:MainWindow.Icon) {
       $TargetWindow.Icon = $global:MainWindow.Icon
   }
   ```

## Coding Standards
  - **PowerShell Version**: Code must be compatible with Windows PowerShell 5.1 (the default on most admin workstations).

  - Variable Scope:

    - Use `$global:` for configuration arrays (`$global:Config`), directory paths (`$global:uiDir`), or brushes that must be accessed across multiple separated .ps1 files.

    - Use standard scoping (`$variable`) for temporary data within functions.

  - **Separation of Concerns**: Do not put business logic inside hybrID.ps1. Write a helper function in the private folder and bind it in the controller.

  - **Error Handling**: Use try/catch blocks for external API calls (Microsoft Graph, Active Directory) and provide clean, themed XAML popups for errors rather than writing to the console or using legacy WinForms Message Boxes.

## Development Workflow
  1. Fork the repository and clone it to your local machine.
  2. Create a feature branch (git checkout -b feature/Add-Export-Button).
  3. Make your changes within the hybrID/ directory structure.
  4. Run the application by executing hybrID/public/hybrID.ps1 in your terminal. Ensure the app launches cleanly from the terminal and that your feature respects both Dark and Light themes.
     > There currently is not a test harness, but that may be an eventual addition.
  5. Commit your changes with clean, descriptive commit messages.
  6. Push to your fork and submit a PR to the main branch.

### Adding a New Setting
If you are adding a new user-configurable setting:

  1. Add the default key/value pair to `config/config.json`.
  2. Update the `ui/settings.xaml` grid to include the new label and input control.
  3. Update the `Show-Settings.ps1` script to map the control, populate it on load, and save it to `$global:Config` on apply.

## Need Help?
If you are unsure about the architecture or how to implement a specific feature, feel free to open a Draft PR or an Issue to discuss it!