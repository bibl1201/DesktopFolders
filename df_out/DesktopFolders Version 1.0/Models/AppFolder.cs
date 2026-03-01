using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace DesktopFolders.Models
{
    public class AppEntry
    {
        public string  Name           { get; set; } = "";
        public string  ExecutablePath { get; set; } = "";
        public string? IconPath       { get; set; }   // custom icon override (.ico / .png / .jpg)
        public string? Arguments      { get; set; }   // command-line arguments
        public string? SubFolderId    { get; set; }   // if set, this entry opens another folder
    }

    public class AppFolder
    {
        public string         Id        { get; set; } = Guid.NewGuid().ToString();
        public string         Name      { get; set; } = "New Folder";
        public string         Color     { get; set; } = "#5B8CFF";
        public List<AppEntry> Apps      { get; set; } = new();
        public double         PositionX { get; set; } = 100;
        public double         PositionY { get; set; } = 100;
    }

    public enum AppTheme       { Dark, Light, Midnight, Sunset, Accent }
    public enum IconSize       { Small, Normal, Large }
    public enum WidgetShape    { RoundedSquare, Circle, Sharp }
    public enum WidgetStyle    { Gradient, Solid, Glass, Outline }
    public enum LabelStyle     { Shadow, Pill, Hidden }
    public enum IconBgStyle    { Squircle, Circle, Square, None }
    public enum PopupAnimation { None, Expand, Slide, Fade }

    public class ThemeSettings
    {
        public AppTheme Theme        { get; set; } = AppTheme.Dark;
        public int      FontSize     { get; set; } = 11;

        public double        PopupOpacity       { get; set; } = 0.85;
        public IconSize      IconSize           { get; set; } = IconSize.Normal;
        public bool          ShowAppLabels      { get; set; } = true;
        public bool          RoundedIcons       { get; set; } = true;
        public IconBgStyle   IconBgStyle        { get; set; } = IconBgStyle.Squircle;
        public PopupAnimation PopupAnimation    { get; set; } = PopupAnimation.Expand;
        public bool          HideShortcutArrows { get; set; } = false;
        public int           PopupColumns       { get; set; } = 4;

        public WidgetShape WidgetShape   { get; set; } = WidgetShape.RoundedSquare;
        public WidgetStyle WidgetStyle   { get; set; } = WidgetStyle.Gradient;
        public LabelStyle  LabelStyle    { get; set; } = LabelStyle.Shadow;
        public double      WidgetOpacity { get; set; } = 1.0;
        public bool        WidgetShadow  { get; set; } = true;
        public bool        ShowMiniIcons { get; set; } = true;
    }

    public class FolderDataStore
    {
        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopFolders");
        private static readonly string DataPath  = Path.Combine(DataDir, "folders.json");
        private static readonly string ThemePath = Path.Combine(DataDir, "theme.json");

        public List<AppFolder> Folders { get; set; } = new();
        public ThemeSettings   Theme   { get; set; } = new();

        public void Load()
        {
            try
            {
                if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
                if (File.Exists(DataPath))
                    Folders = JsonSerializer.Deserialize<List<AppFolder>>(
                        File.ReadAllText(DataPath)) ?? new();
                if (File.Exists(ThemePath))
                    Theme = JsonSerializer.Deserialize<ThemeSettings>(
                        File.ReadAllText(ThemePath)) ?? new();
            }
            catch { Folders = new(); Theme = new(); }
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
                var opts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(DataPath,  JsonSerializer.Serialize(Folders, opts));
                File.WriteAllText(ThemePath, JsonSerializer.Serialize(Theme,   opts));
            }
            catch { }
        }

        /// <summary>
        /// Exports a .dfbackup zip containing folders.json, theme.json, and any
        /// referenced custom icon files (copied to icons/ subfolder with relative paths
        /// rewritten in the embedded folders.json so they survive a restore to any machine).
        /// Returns null on success or an error message string.
        /// </summary>
        public string? ExportBackup(string destZipPath)
        {
            try
            {
                if (File.Exists(destZipPath)) File.Delete(destZipPath);

                // Deep-clone Folders list so we can rewrite IconPath values
                // without mutating the live data
                var opts        = new JsonSerializerOptions { WriteIndented = true };
                var foldersCopy = JsonSerializer.Deserialize<List<AppFolder>>(
                                      JsonSerializer.Serialize(Folders, opts)) ?? new();

                // Collect all unique icon files that exist on disk
                var iconFiles = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);   // original path → archive entry name

                foreach (var folder in foldersCopy)
                    foreach (var app in folder.Apps)
                    {
                        if (string.IsNullOrEmpty(app.IconPath)
                            || !File.Exists(app.IconPath)) continue;

                        if (!iconFiles.ContainsKey(app.IconPath))
                        {
                            // Unique entry name inside the zip
                            string entryName = $"icons/{Path.GetFileName(app.IconPath)}";
                            // Handle duplicates (same filename, different dir)
                            int suffix = 1;
                            string baseName = entryName;
                            while (iconFiles.ContainsValue(entryName))
                                entryName = $"{Path.GetFileNameWithoutExtension(baseName)}" +
                                            $"_{suffix++}{Path.GetExtension(baseName)}";
                            iconFiles[app.IconPath] = entryName;
                        }
                        // Rewrite path to the relative entry name
                        app.IconPath = iconFiles[app.IconPath];
                    }

                using var zip = ZipFile.Open(destZipPath, ZipArchiveMode.Create);

                // Write rewritten folders.json
                var foldersJson = JsonSerializer.Serialize(foldersCopy, opts);
                var fEntry = zip.CreateEntry("folders.json", CompressionLevel.Optimal);
                using (var w = new StreamWriter(fEntry.Open()))
                    w.Write(foldersJson);

                // Write theme.json
                var themeJson = JsonSerializer.Serialize(Theme, opts);
                var tEntry = zip.CreateEntry("theme.json", CompressionLevel.Optimal);
                using (var w = new StreamWriter(tEntry.Open()))
                    w.Write(themeJson);

                // Pack icon files
                foreach (var (src, entryName) in iconFiles)
                {
                    try { zip.CreateEntryFromFile(src, entryName, CompressionLevel.Optimal); }
                    catch { /* Skip unreadable icon — not fatal */ }
                }

                return null; // success
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>
        /// Restores from a .dfbackup zip.  Custom icons are extracted to
        /// %AppData%\DesktopFolders\icons\ and IconPath values are rewritten
        /// to the absolute paths on the current machine.
        /// Returns null on success or an error message string.
        /// </summary>
        public string? ImportBackup(string srcZipPath)
        {
            try
            {
                var iconsDir = Path.Combine(DataDir, "icons");
                Directory.CreateDirectory(iconsDir);

                string? newFoldersJson = null;
                string? newThemeJson   = null;

                using (var zip = ZipFile.OpenRead(srcZipPath))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.FullName == "folders.json")
                        {
                            using var r = new StreamReader(entry.Open());
                            newFoldersJson = r.ReadToEnd();
                        }
                        else if (entry.FullName == "theme.json")
                        {
                            using var r = new StreamReader(entry.Open());
                            newThemeJson = r.ReadToEnd();
                        }
                        else if (entry.FullName.StartsWith("icons/",
                                     StringComparison.OrdinalIgnoreCase))
                        {
                            string dest = Path.Combine(iconsDir,
                                Path.GetFileName(entry.FullName));
                            // Overwrite if already there
                            entry.ExtractToFile(dest, overwrite: true);
                        }
                    }
                }

                if (newFoldersJson == null || newThemeJson == null)
                    return "Backup file is missing folders.json or theme.json.";

                // Rewrite relative icon paths to absolute
                var opts    = new JsonSerializerOptions { WriteIndented = true };
                var folders = JsonSerializer.Deserialize<List<AppFolder>>(newFoldersJson, opts)
                              ?? new();
                foreach (var folder in folders)
                    foreach (var app in folder.Apps)
                        if (!string.IsNullOrEmpty(app.IconPath)
                            && app.IconPath.StartsWith("icons/",
                                   StringComparison.OrdinalIgnoreCase))
                        {
                            app.IconPath = Path.Combine(iconsDir,
                                Path.GetFileName(app.IconPath));
                        }

                // Persist
                if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
                File.WriteAllText(DataPath,
                    JsonSerializer.Serialize(folders, opts));
                File.WriteAllText(ThemePath, newThemeJson);

                // Reload live state
                Load();
                return null; // success
            }
            catch (Exception ex) { return ex.Message; }
        }
    }
}
