# 📁 Desktop Folders

**Desktop Folders** lets you create beautiful, customizable app launcher widgets that live directly on your Windows desktop. Click a widget to open a sleek popup showing all the apps inside — then launch them with a single click.

---

## Table of Contents

- [Requirements](#requirements)
- [Installation](#installation)
- [Setting Up Everything Search (Optional but Recommended)](#setting-up-everything-search)
- [First Launch](#first-launch)
- [Core Concepts](#core-concepts)
- [Creating & Managing Folders](#creating--managing-folders)
- [The Widget](#the-widget)
- [The Popup](#the-popup)
- [Settings — Look & Feel](#settings--look--feel)
- [Settings — Popup Window](#settings--popup-window)
- [Settings — Widget](#settings--widget)
- [Settings — Folder Settings](#settings--folder-settings)
- [Settings — Backup & Restore](#settings--backup--restore)
- [System Tray](#system-tray)
- [Data & Storage](#data--storage)
- [Tips & Tricks](#tips--tricks)

---

## Requirements

- Windows 10 or Windows 11 (64-bit)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) or newer

---

## Installation

1. Download the latest release and extract the zip to any folder you like (e.g. `C:\Program Files\DesktopFolders\`).
2. Run **DesktopFolders.exe** (df_out\DesktopFolders Version 1.0\bin\Debug\net8.0-windows\DesktopFolders.exe).
3. A folder icon will appear in your system tray — you're up and running.

> **Tip:** To have Desktop Folders start automatically with Windows, right-click the tray icon and tick **Run on Startup**.

---

## Setting Up Everything Search

The **Bulk Add** feature uses [Everything](https://www.voidtools.com) by voidtools — a free, lightning-fast file indexer — to let you search your entire PC for apps and add them to a folder in seconds. This is completely optional; you can always add apps one at a time without it.

### Step 1 — Install Everything

1. Go to **[voidtools.com](https://www.voidtools.com/downloads/)** and download the **64-bit installer** (Everything 1.4.x or newer).
2. Install and launch it. Everything will index your drives in the background — this usually takes under a minute.
3. Keep Everything running (it sits in your system tray).

### Step 2 — Get Everything64.dll

The SDK library `Everything64.dll` is what Desktop Folders uses to talk to Everything. You can get it in one of two ways:

**Option A — From your Everything installation (easiest):**
1. Open File Explorer and navigate to where Everything is installed.  
   The default is `C:\Program Files\Everything\`.
2. Look for a file called **`Everything64.dll`** in that folder.
3. Copy it and paste it into the same folder as **`DesktopFolders.exe`**.

**Option B — Download the SDK directly:**
1. Go to [voidtools.com/downloads/](https://www.voidtools.com/downloads/) and scroll down to **"Everything SDK"**.
2. Download the SDK zip and extract it.
3. Inside the `dll\` subfolder, grab **`Everything64.dll`**.
4. Copy it into the same folder as **`DesktopFolders.exe`**.

### Step 3 — Verify

Open a folder's settings and click **⚡ Bulk Add**. If Everything is installed, running, and the DLL is in place, you'll see a search box. If not, the window will show a clear error message explaining what's missing.

---

## First Launch

On first launch, if you have no folders yet, a welcome dialog will appear asking if you'd like to create your first folder. Click **Create Folder** to open the folder editor, or **Not Now** to dismiss it — you can always create folders later from the tray icon.

---

## Core Concepts

| Term | What it is |
|---|---|
| **Widget** | The small icon that sits permanently on your desktop |
| **Popup** | The panel that opens when you click a widget, showing all your apps |
| **Folder** | The container that groups your apps together, represented by one widget |

---

## Creating & Managing Folders

### Creating a New Folder

- **Right-click the tray icon** → **New Folder**

### The Folder Editor

When creating or editing a folder, the editor gives you full control:

**Name**
Type any name for your folder. This appears as the widget label and popup title.

**Color**
Pick any color using the interactive color wheel:
- Click and drag anywhere on the **color wheel** to pick hue and saturation.
- Drag the **brightness bar** on the right up/down to adjust brightness.
- Type a **hex code** directly (e.g. `#FF6B35`).
- Use the **RGB sliders** for precise channel control.
- Click any **preset swatch** for a one-click color.

The color you choose tints the widget and carries through to the popup's accent highlights.

**Apps**
- Click **+ Add App** to browse for an `.exe`, `.lnk` (shortcut), `.bat`, or `.url` file. You'll be prompted to confirm or edit the display name.
- Click **+ Add Folder** to nest another existing Desktop Folder inside this one (see [Nested Folders](#nested-folders) below).
- Each app row shows its display name and file path.
- Click **✕** on any row to remove it.
- Use the **Args** field to pass command-line arguments that will be used every time that app is launched (e.g. `--profile Work`).
- Click **🖼 Icon** on any app row to assign a custom icon image (`.ico`, `.png`, `.jpg`, `.bmp`). Click **✕** to clear it.

Click **Save** when done. The widget will appear on your desktop immediately.

### Deleting a Folder

Right-click a widget on the desktop and choose **Delete Folder**. You'll be asked to confirm before anything is removed.

### Nested Folders

You can place one folder inside another. When you click a nested folder entry in a popup, it opens that folder's popup in place of the current one — great for building a hierarchy (e.g. a "Work" folder containing "Design Tools" and "Dev Tools" sub-folders).

To add a nested folder: open the Folder Editor → click **+ Add Folder** → pick which folder to nest.

---

## The Widget

The widget is the small icon that lives on your desktop at all times.

### Interactions

| Action | Result |
|---|---|
| **Click** | Opens the popup |
| **Click again / click outside** | Closes the popup |
| **Click & drag** | Moves the widget to a new position (position is saved automatically) |
| **Drag files from Explorer and drop onto a widget** | Adds those files as new apps in that folder |
| **Right-click** | Opens a context menu with Edit and Delete options |

### Show Desktop

Desktop Folders widgets survive the **Show Desktop** button (Win+D). A background watchdog ensures widgets re-appear immediately if Windows tries to hide or minimize them. (EX. If you use the "Show Desktop" shortcut, when you open any other application, your widgets should reappear.)

---

## The Popup

Clicking a widget opens the popup — a floating panel listing all your apps.

### Launching Apps

Click any app icon to launch it. The popup closes automatically after launching.

### App Right-Click Menu

Right-click any app icon in the popup for extra options:

- **✏ Rename…** — Change the display name shown in the popup.
- **🖼 Set Custom Icon…** — Pick a custom icon file for this app (`.ico`, `.png`, `.jpg`). The custom icon shows in both the popup and as a mini-icon on the widget.
- **Clear Custom Icon** — Revert to the app's default icon. (Greyed out if no custom icon is set.)
- **📂 Show in Explorer** — Opens the folder containing the app's executable in Windows Explorer.

### Bulk Add with Everything

Inside the Folder Settings tab (accessible from the popup's settings button), click **⚡ Bulk Add** to open the Everything search window:

1. Type any search term (app name, keyword, or partial path) and press **Enter** or click **Search**.
2. Results are grouped into **Program Files**, **Program Files (x86)**, and **Other**.
3. Toggle **User-installed only** to filter out system executables and runtime components, showing only apps you actually installed.
4. Click any result row to select it (it highlights in blue). Click again to deselect.
5. The search box also acts as a live filter — typing will instantly narrow the results already shown without triggering a new search.
6. When you've selected everything you want, click **Add N Items** to add them all at once.

Apps already in the folder are shown with a **✓ Added** badge and cannot be selected again.

---

## Settings — Look & Feel

Open Settings from the popup (⚙ button) or from the tray icon → **Appearance Settings**.

### Color Theme

Choose from five built-in color themes that affect the settings window, popup, and widget previews:

| Theme | Description |
|---|---|
| **Dark** | Deep dark blue-grey background, the default |
| **Light** | Clean white/light grey — great for bright setups |
| **Midnight** | Near-black with cool blue-purple tones |
| **Sunset** | Warm amber and orange accent tones |
| **🎨 System Accent** | Pulls colors directly from your Windows accent color setting |

### Font Size

Use the slider (8–16 pt) to control the text size used for app labels in the popup and widget labels. The live preview in the sidebar updates as you drag.

### Built-In Style Presets

One-click presets that apply a full combination of theme, widget shape, icon style, animation, and layout — great for getting a polished look instantly:

| Preset | Style |
|---|---|
| **iOS ☀️** | Light theme · Frosted glass widget · Squircle icons · Fade animation · 3 columns |
| **iOS 🌙** | Dark theme · Frosted glass widget · Squircle icons · Fade animation · 3 columns |
| **Android ☀️** | Light theme · Circle widget · Circle icons · Expand animation · 4 columns |
| **Android 🌙** | Midnight theme · Circle widget · Circle icons · Expand animation · 4 columns |
| **Windows ☀️** | Light theme · Sharp widget · No icon background · Slide animation · 4 columns |
| **Windows 🌙** | Dark theme · Sharp widget · No icon background · Slide animation · 4 columns |

A green flash on the preset card confirms it was applied. All settings can still be fine-tuned individually after applying a preset.

---

## Settings — Popup Window

### Background

**Opacity** slider controls how transparent the popup background is (30%–100%). Lower values let the desktop show through for a frosted-glass effect.

### App Icons

**Size** — Small, Normal, or Large app icons in the popup grid.

**Icon Background Shape** — The shape behind each app icon:
- **Squircle** — Rounded square (iOS-style)
- **Circle** — Perfect circle (Android-style)
- **Square** — Sharp corners
- **None** — No background, just the icon

### Layout

**Max columns per row** — How many app icons appear per row (1–8). The popup automatically adjusts its width to match.

### Animation

Choose how the popup opens and closes:
- **None** — Instant appear/disappear
- **Expand** — Grows from the widget position
- **Slide** — Slides in from slightly below
- **Fade** — Fades in smoothly

Click **▶ Preview Animation** to see it in the live preview without opening a real popup.

### Labels & Display

- **Show app name labels** — Toggle text labels beneath each icon in the popup.
- **Rounded icon corners** — Applies rounded corners to the app icon images themselves.
- **Hide shortcut overlay arrows** — Hides the small arrow badge that Windows places on shortcut icons.

---

## Settings — Widget

### Shape

Controls the overall shape of the widget icon on your desktop:
- **Rounded** — Soft rounded square (like iOS app icons)
- **Circle** — Perfect circle
- **Sharp** — Square with no rounding (like Windows tiles)

### Fill Style

Controls how the widget's background is rendered:
- **Gradient** — A radial gradient using your folder's accent color
- **Solid** — A flat, solid-color fill
- **Glass** — A frosted, semi-transparent look with a glass sheen
- **Outline** — Transparent center with a colored border only

### Label Style

Controls how your folder name appears below the widget:
- **Shadow** — White text with a drop shadow (always readable against any wallpaper)
- **Pill** — Text inside a small rounded pill badge
- **Hidden** — No label; the widget is icon only

### Opacity & Effects

- **Widget opacity** slider (20%–100%) — Blends the widget into your wallpaper.
- **Drop shadow** — Adds a soft shadow under the widget for depth.
- **Show mini icons in widget** — Displays up to 4 small app icons inside the widget so you can see what's inside at a glance.

---

## Settings — Folder Settings

This tab lets you edit a specific folder's name, color, and app list directly from within Settings. It's the same editor as the Folder Editor, but embedded in the settings panel.

If you opened Settings from a folder's popup or widget right-click menu, that folder will be pre-selected here. Otherwise you'll see a hint to open Settings from a specific folder.

---

## Settings — Backup & Restore

### Create Backup

Click **Choose Location & Export…** to save everything to a `.dfbackup` file. The backup includes:

- All your folders and their app lists
- Widget positions on screen
- All theme and appearance settings
- Any custom icon images you've assigned

The backup is a self-contained zip archive — safe to copy between machines or store in cloud storage.

### Restore Backup

Click **Choose Backup File & Restore…** and select a `.dfbackup` file. 

> ⚠ **This will replace all current folders, settings, and icons.** A confirmation prompt appears before anything is overwritten.

After restoring, all widgets are automatically reloaded with the recovered data. Custom icons are extracted to `%AppData%\DesktopFolders\icons\` and linked automatically — no manual file copying needed.

---

## System Tray

Right-click the Desktop Folders icon in your system tray for quick access:

| Option | What it does |
|---|---|
| **New Folder** | Opens the Folder Editor to create a new folder |
| **Appearance Settings** | Opens the Settings window |
| **Restore All Widgets** | Forces all widgets back to their last saved positions — useful if they've been pushed off-screen |
| **Run on Startup** | Toggle whether Desktop Folders launches automatically when Windows starts |
| **Exit** | Closes Desktop Folders and removes all widgets |

Double-clicking the tray icon also refreshes and restores all widgets.

---

## Data & Storage

All data is saved automatically to:

```
%AppData%\DesktopFolders\
    folders.json   — Your folders and app lists
    theme.json     — All appearance settings
    icons\         — Custom icon images
```

You never need to manage these files manually — everything saves instantly as you make changes.

---

## Tips & Tricks

**Nest for organization** — Use nested folders to build a hierarchy. Create a "Dev Tools" folder and nest it inside a "Work" folder for a clean two-level launcher.

**System Accent theme** — If you change your Windows accent color in Settings → Personalization → Colors, switch to the **🎨 System Accent** theme and Desktop Folders will match it automatically. (Currently not available for widget color selection.)

**Custom icons go a long way** — Right-clicking any app in the popup and choosing **Set Custom Icon** lets you assign any `.ico` or `.png`. Great for apps with poor default icons or for making your widget's mini-icon grid look intentional.

**Arguments for power users** — Use the **Args** field (in the Folder Editor or Settings → Folder Settings) to launch apps with specific flags every time. Examples:
- `--profile Work` to open a browser with a specific profile
- `--minimized` to start an app in the background
- `-f` or `--fullscreen` for games or media players

**Backup before big changes** — Before reorganizing all your folders or changing lots of settings, pop into **Settings → Backup & Restore** and export a backup. Takes two seconds and gives you a safety net.
