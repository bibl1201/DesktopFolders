using System;
using System.IO;

namespace DesktopFolders.Helpers
{
    /// <summary>
    /// Resolves Windows shortcut (.lnk) files to their target executable paths.
    /// Single authoritative implementation shared by all views.
    /// </summary>
    public static class ShortcutHelper
    {
        /// <summary>
        /// If <paramref name="path"/> is a .lnk file, returns its TargetPath;
        /// otherwise returns <paramref name="path"/> unchanged.
        /// Never throws.
        /// </summary>
        public static string Resolve(string path)
        {
            if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                return path;

            // Primary: IWshRuntimeLibrary COM (already a project reference)
            try
            {
                var shell    = new IWshRuntimeLibrary.WshShell();
                var shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(path);
                string target = shortcut.TargetPath;
                if (!string.IsNullOrWhiteSpace(target)) return target;
            }
            catch { }

            // Fallback: WScript.Shell via late-binding
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell    = Activator.CreateInstance(shellType)!;
                    dynamic shortcut = shell.CreateShortcut(path);
                    string  target   = (string)shortcut.TargetPath;
                    if (!string.IsNullOrWhiteSpace(target)) return target;
                }
            }
            catch { }

            return path;
        }

        /// <summary>
        /// Given any file drop path (.exe, .lnk, .bat, .url) returns the
        /// canonical executable/url string to store, or null if unsupported.
        /// </summary>
        public static string? ResolveToStorable(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            // Accept any file that exists (all types now supported).
            // Non-existent paths are rejected so we don't store broken entries.
            if (File.Exists(path) || Directory.Exists(path)) return path;
            return null;
        }
    }
}
