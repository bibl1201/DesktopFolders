using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopFolders.Helpers
{
    /// <summary>
    /// Queries Everything (voidtools.com) via its SDK IPC.
    /// Everything must be running; if not, IsAvailable is false.
    ///
    /// Results are grouped into:
    ///   Program Files      — %ProgramFiles%
    ///   Program Files(x86) — %ProgramFiles(x86)%
    ///   Other              — everything else (excluding system paths when
    ///                        the "user installed only" filter is active)
    /// </summary>
    public static class EverythingSearch
    {
        // ── Everything64 SDK IPC ───────────────────────────────────────────

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern int  Everything_SetSearchW(string lpSearchString);
        [DllImport("Everything64.dll")]
        private static extern bool Everything_QueryW(bool bWait);
        [DllImport("Everything64.dll")]
        private static extern int  Everything_GetNumResults();
        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern void Everything_GetResultFullPathNameW(
            int nIndex, StringBuilder lpString, int nMaxCount);
        [DllImport("Everything64.dll")]
        private static extern int  Everything_GetLastError();
        [DllImport("Everything64.dll")]
        private static extern void Everything_SetMax(int dwMax);
        [DllImport("Everything64.dll")]
        private static extern void Everything_SetOffset(int dwOffset);
        [DllImport("Everything64.dll")]
        private static extern void Everything_SetRequestFlags(uint dwRequestFlags);
        [DllImport("Everything64.dll")]
        private static extern void Everything_SetSort(uint dwSortType);
        [DllImport("Everything64.dll")]
        private static extern void Everything_CleanUp();

        private const uint EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME = 0x00000004;
        private const uint EVERYTHING_SORT_PATH_ASCENDING             = 3;
        private const int  EVERYTHING_ERROR_IPC                       = 2;

        // ── Well-known path roots (resolved once at startup) ───────────────

        private static readonly string PfPath =
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        private static readonly string Pfx86Path =
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        private static readonly string LocalAppData =
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static readonly string AppData =
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private static readonly string WinDir =
            Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        // User-installed roots: apps here are almost certainly user-chosen installs
        private static readonly string[] UserRoots = BuildUserRoots();
        // System-only roots: apps here are OS components, runtimes, drivers, etc.
        private static readonly string[] SystemRoots = BuildSystemRoots();

        private static string[] BuildUserRoots() =>
            new[]
            {
                PfPath,
                Pfx86Path,
                Path.Combine(LocalAppData, "Programs"),           // e.g. VS Code, Discord
                Path.Combine(LocalAppData, "Microsoft", "WindowsApps"), // MSIX Store apps
                Path.Combine(AppData, "Microsoft", "Windows", "Start Menu", "Programs"),
            };

        private static string[] BuildSystemRoots() =>
            new[]
            {
                Path.Combine(WinDir, "System32"),
                Path.Combine(WinDir, "SysWOW64"),
                Path.Combine(WinDir, "SystemApps"),
                Path.Combine(WinDir, "WinSxS"),
                Path.Combine(WinDir, "servicing"),
                Path.Combine(WinDir, "Microsoft.NET"),
                Path.Combine(WinDir, "assembly"),
                // dotnet runtime host lives here
                Path.Combine(PfPath, "dotnet"),
                Path.Combine(Pfx86Path, "dotnet"),
                // Windows Kits / SDK tools are system, not user apps
                Path.Combine(PfPath,   "Windows Kits"),
                Path.Combine(Pfx86Path,"Windows Kits"),
                // PowerShell runtime
                Path.Combine(WinDir, "System32", "WindowsPowerShell"),
            };

        // ── Public types ───────────────────────────────────────────────────

        public class SearchResult
        {
            public bool           IsAvailable  { get; init; }
            public string         ErrorMessage { get; init; } = "";
            public List<ExeGroup> Groups       { get; init; } = new();
            public int            TotalCount   => Groups.Sum(g => g.Items.Count);
        }

        public class ExeGroup
        {
            public string        Label { get; init; } = "";
            public List<ExeItem> Items { get; init; } = new();
        }

        public class ExeItem
        {
            public string Path        { get; init; } = "";
            public string DisplayName =>
                System.IO.Path.GetFileNameWithoutExtension(Path);
            public bool   IsUserInstalled { get; init; }
        }

        // ── User-installed classification ──────────────────────────────────

        /// <summary>
        /// Returns true when the path is almost certainly a user-installed app
        /// (under Program Files, LocalAppData\Programs, etc.) rather than an
        /// OS component or runtime.  Pure string prefix matching — no I/O.
        /// </summary>
        public static bool IsUserInstalled(string path)
        {
            // Must be in a known user root …
            bool inUserRoot = false;
            foreach (var root in UserRoots)
                if (!string.IsNullOrEmpty(root) &&
                    path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                { inUserRoot = true; break; }

            if (!inUserRoot) return false;

            // … but NOT under a known system sub-path within those roots
            foreach (var sys in SystemRoots)
                if (!string.IsNullOrEmpty(sys) &&
                    path.StartsWith(sys, StringComparison.OrdinalIgnoreCase))
                    return false;

            return true;
        }

        // ── Query ──────────────────────────────────────────────────────────

        /// <param name="searchTerm">
        ///   Free text forwarded to Everything.  Empty = return all .exe files.
        /// </param>
        /// <param name="userInstalledOnly">
        ///   When true, strips results that don't pass IsUserInstalled.
        /// </param>
        public static SearchResult Query(string searchTerm = "",
                                         bool   userInstalledOnly = false)
        {
            try
            {
                // Build Everything query: always restrict to .exe extension
                string query = string.IsNullOrWhiteSpace(searchTerm)
                    ? "ext:exe"
                    : $"ext:exe {searchTerm}";

                Everything_SetSearchW(query);
                Everything_SetRequestFlags(EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME);
                Everything_SetSort(EVERYTHING_SORT_PATH_ASCENDING);
                Everything_SetMax(20000);
                Everything_SetOffset(0);

                if (!Everything_QueryW(bWait: true))
                {
                    int err = Everything_GetLastError();
                    return new SearchResult
                    {
                        IsAvailable  = false,
                        ErrorMessage = err == EVERYTHING_ERROR_IPC
                            ? "Everything is not running.\n\nPlease start Everything and try again.\nhttps://www.voidtools.com"
                            : $"Everything query failed (error {err})."
                    };
                }

                int count = Everything_GetNumResults();
                var pfItems    = new List<ExeItem>();
                var pfx86Items = new List<ExeItem>();
                var otherItems = new List<ExeItem>();

                var sb = new StringBuilder(4096);
                for (int i = 0; i < count; i++)
                {
                    sb.Clear();
                    Everything_GetResultFullPathNameW(i, sb, sb.Capacity);
                    string path = sb.ToString();
                    if (string.IsNullOrWhiteSpace(path)) continue;

                    bool userInstalled = IsUserInstalled(path);
                    if (userInstalledOnly && !userInstalled) continue;

                    var item = new ExeItem { Path = path, IsUserInstalled = userInstalled };

                    // x86 check must precede non-x86 because on 64-bit Windows
                    // PFx86 is a prefix of PF on some locale configurations
                    if (!string.IsNullOrEmpty(Pfx86Path) &&
                        !string.Equals(PfPath, Pfx86Path, StringComparison.OrdinalIgnoreCase) &&
                        path.StartsWith(Pfx86Path, StringComparison.OrdinalIgnoreCase))
                        pfx86Items.Add(item);
                    else if (!string.IsNullOrEmpty(PfPath) &&
                        path.StartsWith(PfPath, StringComparison.OrdinalIgnoreCase))
                        pfItems.Add(item);
                    else
                        otherItems.Add(item);
                }

                Everything_CleanUp();

                var groups = new List<ExeGroup>();
                if (pfItems.Count > 0)
                    groups.Add(new ExeGroup { Label = "Program Files",       Items = pfItems    });
                if (pfx86Items.Count > 0)
                    groups.Add(new ExeGroup { Label = "Program Files (x86)", Items = pfx86Items });
                if (otherItems.Count > 0)
                    groups.Add(new ExeGroup { Label = "Other",               Items = otherItems });

                return new SearchResult { IsAvailable = true, Groups = groups };
            }
            catch (DllNotFoundException)
            {
                return new SearchResult
                {
                    IsAvailable  = false,
                    ErrorMessage =
                        "Everything64.dll was not found.\n\n" +
                        "Install Everything (64-bit) from voidtools.com and make sure it is running,\n" +
                        "or copy Everything64.dll next to DesktopFolders.exe."
                };
            }
            catch (Exception ex)
            {
                return new SearchResult
                {
                    IsAvailable  = false,
                    ErrorMessage = $"Unexpected error: {ex.Message}"
                };
            }
        }
    }
}
