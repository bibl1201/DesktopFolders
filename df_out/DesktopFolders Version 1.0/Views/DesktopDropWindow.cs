using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DesktopFolders.Helpers;
using DesktopFolders.Models;

namespace DesktopFolders.Views
{
    /// <summary>
    /// An invisible fullscreen window that sits permanently at the bottom of
    /// the z-order.  When the user drags 2+ files over the desktop and drops
    /// them, it creates a new folder widget at the drop position.
    ///
    /// It does NOT intercept single-file drops (those go to Explorer as normal).
    /// </summary>
    public class DesktopDropWindow : Window
    {
        private readonly Action<AppFolder, Point> _onCreate;

        public DesktopDropWindow(Action<AppFolder, Point> onCreate)
        {
            _onCreate = onCreate;

            // Cover the full virtual desktop (multi-monitor)
            Left   = SystemParameters.VirtualScreenLeft;
            Top    = SystemParameters.VirtualScreenTop;
            Width  = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            Background         = System.Windows.Media.Brushes.Transparent;
            AllowsTransparency = true;
            WindowStyle        = WindowStyle.None;
            ShowInTaskbar      = false;
            ResizeMode         = ResizeMode.NoResize;
            Topmost            = false;
            IsHitTestVisible   = true;
            AllowDrop          = true;

            // Pin to desktop layer (z-bottom, no focus)
            SourceInitialized += (_, _) => DesktopHelper.PinToDesktop(this);

            DragEnter += OnDragEnter;
            DragOver  += OnDragOver;
            Drop      += OnDrop;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effects = IsMultiFileDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = IsMultiFileDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length < 2) return;

            var folder = new AppFolder { Name = "New Folder", Color = "#5B8CFF" };

            foreach (var file in files)
            {
                string? storable = ShortcutHelper.ResolveToStorable(file);
                if (storable == null) continue;

                folder.Apps.Add(new AppEntry
                {
                    Name           = Path.GetFileNameWithoutExtension(file),
                    ExecutablePath = storable
                });
            }

            if (folder.Apps.Count < 2) return;

            var dropPos = e.GetPosition(this);
            _onCreate(folder, dropPos);
            e.Handled = true;
        }

        private static bool IsMultiFileDrop(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            return files?.Length >= 2;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // Make the window completely click-through for mouse events
            // so it doesn't interfere with normal desktop interaction.
            var hwnd = new WindowInteropHelper(this).Handle;
            const int WS_EX_TRANSPARENT = 0x00000020;
            int exStyle = DesktopHelper.GetExStyle(hwnd);
            // Only add WS_EX_TRANSPARENT — drops still work because DragDrop
            // uses a different code path (OLE) that isn't blocked by this flag.
            DesktopHelper.SetExStyle(hwnd, exStyle | WS_EX_TRANSPARENT);
        }
    }
}
