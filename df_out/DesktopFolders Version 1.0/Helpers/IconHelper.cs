using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DesktopFolders.Helpers
{
    public static class IconHelper
    {
        // ── Background worker thread ──────────────────────────────────────

        private static readonly System.Collections.Concurrent.BlockingCollection<Action>
            _queue = new();

        static IconHelper()
        {
            var t = new Thread(() =>
            {
                foreach (var work in _queue.GetConsumingEnumerable())
                    try { work(); } catch { }
            })
            { IsBackground = true, Name = "IconHelper-Worker" };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        public static void LoadIconAsync(string? path, Action<BitmapSource> onLoaded)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            _queue.Add(() =>
            {
                var src = SafeLoad(path!);
                if (src == null) return;
                src.Freeze();
                Application.Current?.Dispatcher.BeginInvoke(
                    new Action(() => onLoaded(src)));
            });
        }

        // ── Entry point ───────────────────────────────────────────────────

        private static BitmapSource? SafeLoad(string path)
        {
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".url") return LoadUrlIcon(path);
                if (ext == ".lnk") return LoadLnkIcon(path);
                if (ext == ".ico") return LoadIcoFile(path);
                // .exe and .dll: extract embedded icon from PE resources first
                if (ext == ".exe" || ext == ".dll")
                    return ExtractHighResFromPe(path) ?? ShellIcon(path);
                // All other file types: ask the shell for the associated app icon.
                // SHGetFileInfo returns the icon of the default handler (e.g. the
                // Word icon for .docx, VLC icon for .mp4, etc.)
                return ShellIcon(path);
            }
            catch { return null; }
        }

        // ── .LNK shortcuts ────────────────────────────────────────────────

        private static BitmapSource? LoadLnkIcon(string lnkPath)
        {
            // Read IconLocation ("C:\path\to\file.exe,0") from the shortcut
            try
            {
                ResolveLnkIconLocation(lnkPath, out string iconFile, out int iconIndex);
                if (!string.IsNullOrWhiteSpace(iconFile))
                {
                    iconFile = Environment.ExpandEnvironmentVariables(iconFile);
                    if (File.Exists(iconFile))
                    {
                        var src = LoadFromIconSource(iconFile, iconIndex);
                        if (src != null) return src;
                    }
                }
            }
            catch { }

            // Fall back to the target exe
            try
            {
                string target = ShortcutHelper.Resolve(lnkPath);
                if (!string.Equals(target, lnkPath, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(target))
                {
                    var src = ExtractHighResFromPe(target);
                    if (src != null) return src;
                }
            }
            catch { }

            return ShellIcon(lnkPath);
        }

        private static void ResolveLnkIconLocation(string lnkPath,
            out string iconFile, out int iconIndex)
        {
            iconFile  = "";
            iconIndex = 0;
            try
            {
                var shell    = new IWshRuntimeLibrary.WshShell();
                var shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(lnkPath);
                ParseIconLocation(shortcut.IconLocation ?? "", out iconFile, out iconIndex);
                if (!string.IsNullOrWhiteSpace(iconFile)) return;
            }
            catch { }
            try
            {
                Type? t = Type.GetTypeFromProgID("WScript.Shell");
                if (t != null)
                {
                    dynamic shell    = Activator.CreateInstance(t)!;
                    dynamic shortcut = shell.CreateShortcut(lnkPath);
                    ParseIconLocation((string)shortcut.IconLocation ?? "",
                        out iconFile, out iconIndex);
                }
            }
            catch { }
        }

        private static void ParseIconLocation(string loc, out string file, out int index)
        {
            file  = "";
            index = 0;
            if (string.IsNullOrWhiteSpace(loc)) return;
            int lastComma = loc.LastIndexOf(',');
            if (lastComma > 0)
            {
                file = loc.Substring(0, lastComma).Trim();
                int.TryParse(loc.Substring(lastComma + 1).Trim(), out index);
            }
            else
            {
                file = loc.Trim();
            }
        }

        // ── .URL internet shortcuts ───────────────────────────────────────
        //
        // Steam game shortcuts contain:
        //   IconFile=C:\Program Files (x86)\Steam\steam\games\<hash>.ico
        //   IconIndex=0
        //   URL=steam://rungameid/<gameId>
        //
        // We read IconFile= and load it directly as a .ico file.

        private static BitmapSource? LoadUrlIcon(string urlFilePath)
        {
            string? iconFile  = null;
            int     iconIndex = 0;
            string? rawUrl    = null;

            try
            {
                foreach (var line in File.ReadAllLines(urlFilePath))
                {
                    if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                        iconFile = Environment.ExpandEnvironmentVariables(
                            line.Substring(9).Trim());
                    else if (line.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(line.Substring(10).Trim(), out iconIndex);
                    else if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                        rawUrl = line.Substring(4).Trim();
                }
            }
            catch { }

            // 1. Steam shortcut: find the game exe via ACF manifest.
            //    Gives full-res icons identical to a shortcut made from the exe.
            if (!string.IsNullOrWhiteSpace(rawUrl) &&
                rawUrl!.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
            {
                var steamSrc = LoadSteamIconViaExe(rawUrl);
                if (steamSrc != null) return steamSrc;
            }

            // 2. Explicit IconFile= in the .url file
            if (!string.IsNullOrWhiteSpace(iconFile) && File.Exists(iconFile))
            {
                var src = LoadFromIconSource(iconFile, iconIndex);
                if (src != null) return src;
            }

            // 3. Shell icon
            var shellSrc = ShellIcon(urlFilePath);
            if (shellSrc != null) return shellSrc;

            // 4. Remote favicon last resort (http only)
            return LoadFavicon(urlFilePath);
        }

        // steam://rungameid/<id>
        //   -> appmanifest_<id>.acf  (installdir)
        //   -> steamapps/common/<installdir>/  (find largest root exe)
        //   -> extract PE icon  (same result as shortcut-from-exe)

        private static string? _steamPath;
        private static bool    _steamPathChecked;

        private static string? GetSteamPath()
        {
            if (_steamPathChecked) return _steamPath;
            _steamPathChecked = true;
            try
            {
                foreach (var regKey in new[]
                {
                    @"SOFTWARE\WOW6432Node\Valve\Steam",
                    @"SOFTWARE\Valve\Steam"
                })
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regKey);
                    if (key?.GetValue("InstallPath") is string val && Directory.Exists(val))
                        return _steamPath = val;
                }
            }
            catch { }
            return null;
        }

        private static BitmapSource? LoadSteamIconViaExe(string steamUrl)
        {
            try
            {
                var    uri    = new Uri(steamUrl);
                string gameId = uri.AbsolutePath.Trim('/');
                if (string.IsNullOrWhiteSpace(gameId) || !long.TryParse(gameId, out _))
                    return null;

                string? steamPath = GetSteamPath();
                if (steamPath == null) return null;

                string acfPath = Path.Combine(
                    steamPath, "steamapps", $"appmanifest_{gameId}.acf");
                if (!File.Exists(acfPath)) return null;

                // Parse "installdir" from the ACF KeyValues text format
                string? installDir = null;
                foreach (var line in File.ReadAllLines(acfPath))
                {
                    var t = line.Trim();
                    if (!t.StartsWith("\"installdir\"", StringComparison.OrdinalIgnoreCase))
                        continue;
                    int last = t.LastIndexOf('"');
                    int prev = last > 0 ? t.LastIndexOf('"', last - 1) : -1;
                    if (last > prev && prev >= 0)
                        installDir = t.Substring(prev + 1, last - prev - 1);
                    break;
                }

                if (string.IsNullOrWhiteSpace(installDir)) return null;

                string gameDir = Path.Combine(
                    steamPath, "steamapps", "common", installDir!);
                if (!Directory.Exists(gameDir)) return null;

                // Find the largest exe in the game root, skipping utility exes
                var skipWords = new[]
                {
                    "unins", "setup", "install", "redist", "crash", "report",
                    "register", "activate", "help", "update", "patch",
                    "vcredist", "directx", "dxsetup", "vc_redist"
                };

                string? bestExe  = null;
                long    bestSize = 0;

                foreach (var exe in Directory.GetFiles(
                    gameDir, "*.exe", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileNameWithoutExtension(exe).ToLowerInvariant();
                    bool   skip = false;
                    foreach (var w in skipWords)
                        if (name.Contains(w)) { skip = true; break; }
                    if (skip) continue;

                    long sz = new FileInfo(exe).Length;
                    if (sz > bestSize) { bestSize = sz; bestExe = exe; }
                }

                return bestExe != null ? ExtractHighResFromPe(bestExe) : null;
            }
            catch { return null; }
        }


        // ── Load from a known icon source file ────────────────────────────
        // Dispatches to the right loader based on extension.

        private static BitmapSource? LoadFromIconSource(string path, int index)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".ico") return LoadIcoFile(path);
            return ExtractHighResFromPe(path, index);
        }

        // ── .ICO file parser ──────────────────────────────────────────────
        //
        // Reads the .ico format directly — no LoadLibraryEx, no COM.
        // Finds the largest frame and decodes it as PNG or DIB.
        //
        // .ico layout:
        //   ICONDIR      6 bytes: reserved(2), type(2), count(2)
        //   ICONDIRENTRY 16 bytes x count:
        //     bWidth(1), bHeight(1), bColorCount(1), bReserved(1),
        //     wPlanes(2), wBitCount(2), dwBytesInRes(4), dwImageOffset(4)
        //   Image data follows

        private static BitmapSource? LoadIcoFile(string icoPath)
        {
            try
            {
                byte[] data = File.ReadAllBytes(icoPath);
                if (data.Length < 6) return null;

                int count = BitConverter.ToUInt16(data, 4);
                if (count <= 0) return null;

                int bestWidth  = -1;
                int bestOffset = -1;
                int bestSize   = -1;

                for (int i = 0; i < count; i++)
                {
                    int e         = 6 + i * 16;
                    if (e + 16 > data.Length) break;

                    int  width     = data[e];                               // 0 = 256
                    int  effective = (width == 0) ? 256 : width;
                    int  imgSize   = BitConverter.ToInt32(data, e + 8);
                    int  imgOffset = BitConverter.ToInt32(data, e + 12);

                    if (effective > bestWidth)
                    {
                        bestWidth  = effective;
                        bestSize   = imgSize;
                        bestOffset = imgOffset;
                    }
                }

                if (bestOffset < 0 || bestSize <= 0) return null;
                if (bestOffset + bestSize > data.Length) return null;

                byte[] raw = new byte[bestSize];
                Array.Copy(data, bestOffset, raw, 0, bestSize);

                // PNG frame (modern icons store 256px as raw PNG)
                if (raw.Length >= 4 &&
                    raw[0] == 0x89 && raw[1] == 0x50 &&
                    raw[2] == 0x4E && raw[3] == 0x47)
                    return DecodePngBytes(raw);

                // DIB frame
                return DecodeRawDib(raw, bestWidth);
            }
            catch { return null; }
        }

        // ── PE resource icon extraction ───────────────────────────────────
        //
        // LoadLibraryEx(LOAD_LIBRARY_AS_DATAFILE): maps PE as raw bytes only,
        // zero code execution. Reads RT_GROUP_ICON/RT_ICON resources.

        private static BitmapSource? ExtractHighResFromPe(string path, int groupIndex = 0)
        {
            if (!File.Exists(path)) return null;
            if (Path.GetExtension(path).ToLowerInvariant() == ".ico")
                return LoadIcoFile(path);

            IntPtr hModule = IntPtr.Zero;
            try
            {
                hModule = LoadLibraryEx(path, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
                if (hModule == IntPtr.Zero) return null;

                // Enumerate all RT_GROUP_ICON resources
                var groups = new System.Collections.Generic.List<IntPtr>();
                EnumResourceNames(hModule, RT_GROUP_ICON, (h, t, name, p) =>
                {
                    groups.Add(name);
                    return true;
                }, IntPtr.Zero);

                if (groups.Count == 0) return null;

                int idx = (groupIndex >= 0 && groupIndex < groups.Count)
                    ? groupIndex : 0;

                IntPtr hGroupRes = FindResource(hModule, groups[idx], RT_GROUP_ICON);
                if (hGroupRes == IntPtr.Zero)
                {
                    hGroupRes = FindResource(hModule, MAKEINTRESOURCE(1), RT_GROUP_ICON);
                    if (hGroupRes == IntPtr.Zero) return null;
                }

                IntPtr pGroup = LockResource(LoadResource(hModule, hGroupRes));
                if (pGroup == IntPtr.Zero) return null;

                // Parse GRPICONDIR to find the largest image entry
                int count   = Marshal.ReadInt16(pGroup, 4);
                int bestId  = -1;
                int bestSz  = -1;

                for (int i = 0; i < count; i++)
                {
                    int  offset    = 6 + i * 14;
                    int  width     = Marshal.ReadByte(pGroup, offset);
                    int  effective = (width == 0) ? 256 : width;
                    int  id        = Marshal.ReadInt16(pGroup, offset + 12);
                    if (effective > bestSz) { bestSz = effective; bestId = id; }
                }

                if (bestId < 0) return null;

                IntPtr hIconRes  = FindResource(hModule, MAKEINTRESOURCE(bestId), RT_ICON);
                if (hIconRes == IntPtr.Zero) return null;

                uint   sz       = SizeofResource(hModule, hIconRes);
                IntPtr pIconData = LockResource(LoadResource(hModule, hIconRes));
                if (pIconData == IntPtr.Zero || sz == 0) return null;

                byte[] raw = new byte[sz];
                Marshal.Copy(pIconData, raw, 0, (int)sz);

                if (raw.Length >= 4 &&
                    raw[0] == 0x89 && raw[1] == 0x50 &&
                    raw[2] == 0x4E && raw[3] == 0x47)
                    return DecodePngBytes(raw);

                return DecodeRawDib(raw, bestSz);
            }
            catch { return null; }
            finally { if (hModule != IntPtr.Zero) FreeLibrary(hModule); }
        }

        // ── Frame decoders ────────────────────────────────────────────────

        private static BitmapSource? DecodePngBytes(byte[] png)
        {
            try
            {
                using var ms = new MemoryStream(png);
                var frame = new PngBitmapDecoder(ms,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad).Frames[0];
                frame.Freeze();
                return frame;
            }
            catch { return null; }
        }

        private static BitmapSource? DecodeRawDib(byte[] dib, int size)
        {
            try
            {
                // Wrap the raw DIB in a minimal .ico container so
                // System.Drawing.Icon can decode it properly
                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);
                bw.Write((ushort)0);  // reserved
                bw.Write((ushort)1);  // type = icon
                bw.Write((ushort)1);  // count
                bw.Write((byte)(size >= 256 ? 0 : size));  // width
                bw.Write((byte)(size >= 256 ? 0 : size));  // height
                bw.Write((byte)0); bw.Write((byte)0);      // color count, reserved
                bw.Write((ushort)1); bw.Write((ushort)32); // planes, bit count
                bw.Write((uint)dib.Length);                // bytes in res
                bw.Write((uint)22);                        // image offset (6+16)
                bw.Write(dib);
                bw.Flush();
                ms.Position = 0;

                using var icon = new System.Drawing.Icon(ms, size, size);
                return RenderIcon(icon, size);
            }
            catch { return null; }
        }

        // ── SHGetFileInfo fallback ────────────────────────────────────────

        private static BitmapSource? ShellIcon(string path)
        {
            IntPtr hIcon = IntPtr.Zero;
            try
            {
                var  info   = new SHFILEINFO();
                bool exists = File.Exists(path) || Directory.Exists(path);

                // Without SHGFI_USEFILEATTRIBUTES the shell opens the file to read
                // its associated app icon — correct for real files.
                // With SHGFI_USEFILEATTRIBUTES it returns a generic type icon based
                // on extension only — useful for hypothetical/non-existent paths.
                uint flags = SHGFI_ICON | SHGFI_LARGEICON |
                             (exists ? 0u : SHGFI_USEFILEATTRIBUTES);

                IntPtr ret = SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL,
                                 ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
                if (ret == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;
                hIcon = info.hIcon;
                using var ico = System.Drawing.Icon.FromHandle(hIcon);
                return RenderIcon(ico, 256);
            }
            catch { return null; }
            finally { if (hIcon != IntPtr.Zero) try { DestroyIcon(hIcon); } catch { } }
        }

        private static BitmapSource? RenderIcon(System.Drawing.Icon ico, int size)
        {
            try
            {
                using var bmp = new System.Drawing.Bitmap(size, size,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.InterpolationMode  = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode    = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.SmoothingMode      = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    using var srcBmp = ico.ToBitmap();
                    g.DrawImage(srcBmp, 0, 0, size, size);
                }
                IntPtr hbmp = IntPtr.Zero;
                try
                {
                    hbmp = bmp.GetHbitmap();
                    return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hbmp, IntPtr.Zero, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
                finally { if (hbmp != IntPtr.Zero) DeleteObject(hbmp); }
            }
            catch { return null; }
        }

        // ── Custom icon override ──────────────────────────────────────────

        public static BitmapSource? LoadCustomIcon(string? iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath)) return null;
            try
            {
                string ext = Path.GetExtension(iconPath!).ToLowerInvariant();
                if (ext == ".ico") return LoadIcoFile(iconPath);
                if (ext == ".exe" || ext == ".dll")
                    return ExtractHighResFromPe(iconPath) ?? ShellIcon(iconPath);
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource        = new Uri(iconPath!, UriKind.Absolute);
                bi.CacheOption      = BitmapCacheOption.OnLoad;
                bi.DecodePixelWidth = 256;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        // ── Favicon fetch (last resort for web .url shortcuts) ────────────

        private static BitmapSource? LoadFavicon(string urlFilePath)
        {
            try
            {
                string? url = ReadUrlFromFile(urlFilePath);
                if (string.IsNullOrWhiteSpace(url)) return null;
                // Don't fetch favicons for non-http schemes (steam://, etc.)
                if (!url!.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;
                var    uri = new Uri(url);
                string fav = $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=64";
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
                byte[] bytes = client.GetByteArrayAsync(fav).GetAwaiter().GetResult();
                using var ms  = new MemoryStream(bytes);
                var frame = BitmapDecoder.Create(ms,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad).Frames[0];
                frame.Freeze();
                return frame;
            }
            catch { return null; }
        }

        public static string? ReadUrlFromFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            try
            {
                foreach (var line in File.ReadAllLines(path!))
                    if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                        return line.Substring(4).Trim();
            }
            catch { }
            return null;
        }

        public static BitmapSource? ConvertBitmap(System.Drawing.Bitmap bmp)
        {
            IntPtr hbmp = IntPtr.Zero;
            try
            {
                hbmp = bmp.GetHbitmap();
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hbmp, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            catch { return null; }
            finally { if (hbmp != IntPtr.Zero) try { DeleteObject(hbmp); } catch { } }
        }

        // ── P/Invoke ──────────────────────────────────────────────────────

        private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        private const uint SHGFI_ICON               = 0x0100;
        private const uint SHGFI_LARGEICON          = 0x0000;
        private const uint SHGFI_USEFILEATTRIBUTES  = 0x0010;
        private const uint FILE_ATTRIBUTE_NORMAL    = 0x0080;

        private static readonly IntPtr RT_ICON       = (IntPtr)3;
        private static readonly IntPtr RT_GROUP_ICON = (IntPtr)14;
        private static IntPtr MAKEINTRESOURCE(int i) => (IntPtr)i;

        private delegate bool EnumResNameProc(
            IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int    iIcon;
            public uint   dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]  public string szTypeName;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(
            string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr FindResource(
            IntPtr hModule, IntPtr lpName, IntPtr lpType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LockResource(IntPtr hResData);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool EnumResourceNames(IntPtr hModule, IntPtr lpszType,
            EnumResNameProc lpEnumFunc, IntPtr lParam);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }

    /// <summary>
    /// Returns trueBrush when the bound boolean is true, falseBrush otherwise.
    /// Drives menu-item hover highlights from IsHighlighted inside a ControlTemplate.
    /// </summary>
    public sealed class HighlightBgConverter : System.Windows.Data.IValueConverter
    {
        private readonly System.Windows.Media.Brush _trueBrush;
        private readonly System.Windows.Media.Brush _falseBrush;

        public HighlightBgConverter(
            System.Windows.Media.Brush trueBrush,
            System.Windows.Media.Brush falseBrush)
        {
            _trueBrush  = trueBrush;
            _falseBrush = falseBrush;
        }

        public object Convert(object value, Type targetType, object parameter,
                              System.Globalization.CultureInfo culture)
            => value is bool b && b ? _trueBrush : _falseBrush;

        public object ConvertBack(object value, Type targetType, object parameter,
                                  System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
}
