using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using System.Windows;
using DesktopFolders.Models;
using DesktopFolders.Views;

using WinForms = System.Windows.Forms;

namespace DesktopFolders
{
    public partial class App : Application
    {
        private WinForms.NotifyIcon?       _trayIcon;
        private List<FolderWidgetWindow>   _folderWidgets = new();
        private DesktopDropWindow?         _dropWindow;

        public static FolderDataStore DataStore { get; private set; } = new();

        public static event EventHandler? ThemeChanged;
        public static void NotifyThemeChanged() => ThemeChanged?.Invoke(null, EventArgs.Empty);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Log AccessViolationExceptions to Desktop before they vanish
            AppDomain.CurrentDomain.FirstChanceException += (s, ex) =>
            {
                if (ex.Exception is AccessViolationException)
                    try
                    {
                        File.AppendAllText(
                            Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                                "DesktopFolders_AV.log"),
                            $"[{DateTime.Now:HH:mm:ss.fff}] {ex.Exception.Message}\n" +
                            $"{ex.Exception.StackTrace}\n\n");
                    }
                    catch { }
            };

            DispatcherUnhandledException += (s, ex) =>
            {
                ex.Handled = true;
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n" +
                    $"{ex.Exception.GetType().Name}: {ex.Exception.Message}\n\n" +
                    "The application will continue running.",
                    "Desktop Folders — Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                var msg = ex.ExceptionObject is Exception exc
                    ? $"{exc.GetType().Name}: {exc.Message}"
                    : ex.ExceptionObject?.ToString() ?? "Unknown error";
                MessageBox.Show($"Fatal error:\n\n{msg}", "Desktop Folders",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DataStore.Load();
            CreateTrayIcon();
            ShowAllFolderWidgets();
            StartDesktopDropWindow();

            // First-run: no folders saved yet — prompt to create one
            if (DataStore.Folders.Count == 0)
                Dispatcher.BeginInvoke(new Action(PromptCreateFirstFolder),
                    System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // ── Tray icon ──────────────────────────────────────────────────────

        private void CreateTrayIcon()
        {
            // Load folder.ico from embedded resources; fall back to system icon
            System.Drawing.Icon appIcon = LoadEmbeddedIcon() ??
                                          System.Drawing.SystemIcons.Application;

            _trayIcon = new WinForms.NotifyIcon
            {
                Icon    = appIcon,
                Text    = "Desktop Folders",
                Visible = true
            };

            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("New Folder",          null, (s, e) => CreateNewFolder());
            menu.Items.Add("Appearance Settings", null, (s, e) => OpenSettings());
            menu.Items.Add("Restore All Widgets", null, (s, e) => RestoreAllWidgets());
            menu.Items.Add(new WinForms.ToolStripSeparator());

            var startupItem = new WinForms.ToolStripMenuItem("Run on Startup")
            {
                Checked      = IsStartupEnabled(),
                CheckOnClick = true
            };
            startupItem.Click += (s, e) => ToggleStartup(startupItem.Checked);
            menu.Items.Add(startupItem);

            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitApplication());

            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick     += (s, e) => ShowAllFolderWidgets();
        }

        private static System.Drawing.Icon? LoadEmbeddedIcon()
        {
            try
            {
                // folder.ico is declared as <Resource> in the csproj, so it lives
                // in the WPF pack resource stream — it is NOT a loose file on disk.
                // Use Application.GetResourceStream with the pack URI to read it.
                var uri    = new Uri("pack://application:,,,/Resources/folder.ico");
                var stream = Application.GetResourceStream(uri)?.Stream;
                if (stream != null)
                    return new System.Drawing.Icon(stream);

                return null;
            }
            catch { return null; }
        }

        // ── Desktop drop window (drag 2+ files → new folder) ──────────────

        private void StartDesktopDropWindow()
        {
            _dropWindow = new DesktopDropWindow(OnDesktopDrop);
            _dropWindow.Show();
        }

        private void OnDesktopDrop(AppFolder folder, System.Windows.Point pos)
        {
            // Place widget at drop position (clamped to screen)
            var screen = SystemParameters.WorkArea;
            folder.PositionX = Math.Max(0, Math.Min(pos.X, screen.Right  - 100));
            folder.PositionY = Math.Max(0, Math.Min(pos.Y, screen.Bottom - 100));

            DataStore.Folders.Add(folder);
            DataStore.Save();

            var widget = new FolderWidgetWindow(folder);
            widget.Show();
            _folderWidgets.Add(widget);
        }

        // ── Folder / widget management ─────────────────────────────────────

        public void OpenSettings()
        {
            var win = new ThemeSettingsWindow();
            win.ShowDialog();
            NotifyThemeChanged();
        }

        private void PromptCreateFirstFolder()
        {
            // Styled first-run dialog — no folders exist yet
            var dlg = new Views.FirstRunDialog();
            if (dlg.ShowDialog() == true)
                CreateNewFolder();
        }

        public void CreateNewFolder()
        {
            var dialog = new FolderEditorWindow(null);
            if (dialog.ShowDialog() == true && dialog.ResultFolder != null)
            {
                DataStore.Folders.Add(dialog.ResultFolder);
                DataStore.Save();
                var widget = new FolderWidgetWindow(dialog.ResultFolder);
                widget.Show();
                _folderWidgets.Add(widget);
            }
        }

        public void RemoveAllWidgets()
        {
            foreach (var w in _folderWidgets) w.Close();
            _folderWidgets.Clear();
        }

        public void ShowAllWidgets()
        {
            foreach (var folder in DataStore.Folders)
            {
                var widget = new FolderWidgetWindow(folder);
                widget.Show();
                _folderWidgets.Add(widget);
            }
        }

        private void ShowAllFolderWidgets()
        {
            foreach (var w in _folderWidgets) w.Close();
            _folderWidgets.Clear();

            foreach (var folder in DataStore.Folders)
            {
                var widget = new FolderWidgetWindow(folder);
                widget.Show();
                _folderWidgets.Add(widget);
            }
        }

        private void RestoreAllWidgets()
        {
            foreach (var w in _folderWidgets)
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(w).Handle;
                if (hwnd != IntPtr.Zero)
                    Helpers.DesktopHelper.RestoreWindow(hwnd);
            }
        }

        public void RemoveFolder(AppFolder folder)
        {
            DataStore.Folders.Remove(folder);
            DataStore.Save();
            var widget = _folderWidgets.FirstOrDefault(w => w.Folder == folder);
            if (widget != null)
            {
                widget.Close();
                _folderWidgets.Remove(widget);
            }
        }

        // ── Startup registry ───────────────────────────────────────────────

        private static bool IsStartupEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            return key?.GetValue("DesktopFolders") != null;
        }

        private static void ToggleStartup(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;
            if (enable)
            {
                var path = System.Diagnostics.Process
                    .GetCurrentProcess().MainModule?.FileName ?? "";
                key.SetValue("DesktopFolders", $"\"{path}\"");
            }
            else
                key.DeleteValue("DesktopFolders", false);
        }

        // ── Shutdown ───────────────────────────────────────────────────────

        private void ExitApplication()
        {
            _trayIcon?.Dispose();
            _dropWindow?.Close();
            foreach (var w in _folderWidgets) w.Close();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
