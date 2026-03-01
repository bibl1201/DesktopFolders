using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DesktopFolders.Helpers;
using DesktopFolders.Models;

namespace DesktopFolders.Views
{
    public partial class FolderWidgetWindow : Window
    {
        public AppFolder Folder { get; }
        private FolderPopupWindow? _popup;
        private bool  _isDragging;
        private Point _dragStart;
        private DispatcherTimer? _watchdog;

        public FolderWidgetWindow(AppFolder folder)
        {
            InitializeComponent();
            Folder = folder;
            Left = folder.PositionX;
            Top  = folder.PositionY;

            SourceInitialized += (s, e) =>
            {
                DesktopHelper.PinToDesktop(this);
                StartWatchdog();
            };

            App.ThemeChanged += OnThemeChanged;
            Closed += (s, e) =>
            {
                App.ThemeChanged -= OnThemeChanged;
                _watchdog?.Stop();
            };

            Refresh();

            FolderIcon.MouseLeftButtonDown += OnMouseDown;
            FolderIcon.MouseLeftButtonUp   += OnMouseUp;
            FolderIcon.MouseMove           += OnMouseMove;
        }

        private void OnThemeChanged(object? sender, EventArgs e) => Refresh();

        // Watchdog: polls every 300 ms and force-restores the widget if Windows
        // has hidden, minimized, or DWM-cloaked it (Show Desktop uses DWM cloaking
        // on Win8+, which bypasses all WndProc hooks — this is the only reliable
        // detection path for that case.
        private void StartWatchdog()
        {
            _watchdog = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _watchdog.Tick += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                // IsHiddenOrCloaked checks minimized, invisible, AND DWM-cloaked.
                // Show Desktop on Win8+ cloaks via DWM — no WndProc message fires.
                // This watchdog is the only reliable recovery path.
                if (DesktopHelper.IsHiddenOrCloaked(hwnd))
                    DesktopHelper.RestoreWindow(hwnd);
            };
            _watchdog.Start();
        }

        public void Refresh()
        {
            var t = App.DataStore.Theme;

            double iconSize = ThemeManager.GetWidgetIconSize();
            FolderIcon.Width  = iconSize;
            FolderIcon.Height = iconSize;

            double labelSpace = t.LabelStyle == LabelStyle.Hidden ? 0 : 18;
            Width  = iconSize + 20;
            Height = iconSize + labelSpace + 4;

            FolderIcon.CornerRadius    = ThemeManager.GetWidgetCornerRadius(iconSize);
            FolderIcon.Background      = ThemeManager.GetWidgetBackground(Folder.Color);
            FolderIcon.BorderBrush     = ThemeManager.GetWidgetBorderBrush(Folder.Color);
            FolderIcon.BorderThickness = new Thickness(ThemeManager.GetWidgetBorderThickness());
            FolderIcon.Effect          = ThemeManager.GetWidgetShadow();

            // Clip FolderIcon itself so its children (MiniGrid + icons) can never
            // bleed outside the widget shape. We set the clip on the Border directly,
            // in FolderIcon's own coordinate space (0,0 = top-left of the Border).
            // Clipping MiniGrid instead is wrong because MiniGrid has a margin offset
            // so its coordinate origin doesn't align with FolderIcon's corners.
            var cr = ThemeManager.GetWidgetCornerRadius(iconSize);

            if (cr.TopLeft >= iconSize / 2 - 1)
            {
                // Circle — EllipseGeometry centred on the icon
                FolderIcon.Clip = new System.Windows.Media.EllipseGeometry(
                    new System.Windows.Point(iconSize / 2, iconSize / 2),
                    iconSize / 2, iconSize / 2);
            }
            else if (cr.TopLeft > 0)
            {
                // Rounded rectangle — matches CornerRadius exactly
                FolderIcon.Clip = new System.Windows.Media.RectangleGeometry(
                    new System.Windows.Rect(0, 0, iconSize, iconSize),
                    cr.TopLeft, cr.TopLeft);
            }
            else
            {
                // Sharp — no clip needed, rectangles don't overflow
                FolderIcon.Clip = null;
            }

            // Clear any stale clip on MiniGrid from previous versions
            MiniGrid.Clip = null;

            MiniGrid.Margin = t.WidgetStyle == WidgetStyle.Glass
                ? new Thickness(6, 10, 6, 6) : new Thickness(8);

            // ── Label ──────────────────────────────────────────────────────
            FolderLabel.Visibility = Visibility.Collapsed;
            LabelPill.Visibility   = Visibility.Collapsed;
            switch (t.LabelStyle)
            {
                case LabelStyle.Shadow:
                    FolderLabel.Text       = Folder.Name;
                    FolderLabel.FontSize   = t.FontSize;
                    FolderLabel.Visibility = Visibility.Visible;
                    break;
                case LabelStyle.Pill:
                    FolderLabelPill.Text     = Folder.Name;
                    FolderLabelPill.FontSize = t.FontSize;
                    LabelPill.Visibility     = Visibility.Visible;
                    break;
            }

            // ── Mini icons (high-res, loop-variable capture safe) ──────────
            var minis = new[] { MiniIcon0, MiniIcon1, MiniIcon2, MiniIcon3 };

            // Clear all first
            foreach (var m in minis) { m.Opacity = 0; m.Background = null; }

            if (!t.ShowMiniIcons) return;

            for (int i = 0; i < minis.Length; i++)
            {
                if (i >= Folder.Apps.Count) break;

                // Capture loop variables for the async callbacks
                var capturedMini  = minis[i];
                var app           = Folder.Apps[i];
                double miniOpacity = t.WidgetStyle == WidgetStyle.Outline ? 0.6 : 0.88;
                capturedMini.Opacity = miniOpacity;

                // 1. Custom icon override
                if (!string.IsNullOrEmpty(app.IconPath) && File.Exists(app.IconPath))
                {
                    var capturedIconPath = app.IconPath!;
                    System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                    {
                        var src = IconHelper.LoadCustomIcon(capturedIconPath);
                        if (src != null)
                        {
                            src.Freeze();
                            Dispatcher.BeginInvoke(new Action(() =>
                                capturedMini.Background = new ImageBrush(src)));
                        }
                    });
                    continue;
                }

                // 2. Sub-folder entry — show the nested folder's accent colour
                if (!string.IsNullOrEmpty(app.SubFolderId))
                {
                    var nested = App.DataStore.Folders.Find(f => f.Id == app.SubFolderId);
                    var col = Colors.CornflowerBlue;
                    if (nested != null)
                        try { col = (Color)ColorConverter.ConvertFromString(nested.Color); } catch { }
                    capturedMini.Background = new SolidColorBrush(col);
                    continue;
                }

                // 3. Normal app — load high-res icon (path guard is inside LoadIconAsync)
                var capturedPath = app.ExecutablePath;
                IconHelper.LoadIconAsync(capturedPath, src =>
                    capturedMini.Background = new ImageBrush(src));
            }
        }

        // ── Drag to reposition ─────────────────────────────────────────────
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _dragStart  = e.GetPosition(this);
            FolderIcon.CaptureMouse();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || !FolderIcon.IsMouseCaptured) return;
            var delta = e.GetPosition(this) - _dragStart;
            if (!_isDragging && (Math.Abs(delta.X) > 5 || Math.Abs(delta.Y) > 5))
            {
                _isDragging = true;
                _popup?.Close();
            }
            if (_isDragging) { Left += delta.X; Top += delta.Y; }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            FolderIcon.ReleaseMouseCapture();
            if (_isDragging)
            {
                Folder.PositionX = Left;
                Folder.PositionY = Top;
                App.DataStore.Save();
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero) DesktopHelper.SendToBottom(hwnd);
            }
            else
            {
                if (_popup != null && _popup.IsVisible) { _popup.Close(); _popup = null; }
                else OpenPopup();
            }
            _isDragging = false;
        }

        // ── Drop from Explorer ─────────────────────────────────────────────
        private void Grid_DragEnter(object sender, DragEventArgs e)
        {
            if (IsValidDrop(e)) { e.Effects = DragDropEffects.Copy; AnimateDropHighlight(true); }
            else e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void Grid_DragLeave(object sender, DragEventArgs e) => AnimateDropHighlight(false);

        private void Grid_Drop(object sender, DragEventArgs e)
        {
            AnimateDropHighlight(false);
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            bool added = false;
            foreach (var file in (string[])e.Data.GetData(DataFormats.FileDrop))
            {
                string? storable = ShortcutHelper.ResolveToStorable(file);
                if (storable == null) continue;
                if (Folder.Apps.Exists(a => string.Equals(a.ExecutablePath, storable,
                    StringComparison.OrdinalIgnoreCase))) continue;
                Folder.Apps.Add(new AppEntry
                {
                    Name           = Path.GetFileNameWithoutExtension(file),
                    ExecutablePath = storable
                });
                added = true;
            }
            if (added) { App.DataStore.Save(); Refresh(); FlashConfirmation(); }
            e.Handled = true;
        }

        private static bool IsValidDrop(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            // Accept any file or folder — all types are now supported
            return files != null && files.Length > 0;
        }

        private void AnimateDropHighlight(bool show)
        {
            DropHighlight.BeginAnimation(OpacityProperty,
                new DoubleAnimation(show ? 1.0 : 0.0, TimeSpan.FromMilliseconds(150)));
            if (show)
                try { DropHighlight.BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(Folder.Color)); } catch { }
        }

        private void FlashConfirmation()
        {
            var t = new ScaleTransform(1, 1, FolderIcon.Width / 2, FolderIcon.Height / 2);
            FolderIcon.RenderTransform = t;
            foreach (var prop in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
            {
                var a = new DoubleAnimationUsingKeyFrames();
                a.KeyFrames.Add(new LinearDoubleKeyFrame(1.00, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                a.KeyFrames.Add(new LinearDoubleKeyFrame(1.12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
                a.KeyFrames.Add(new LinearDoubleKeyFrame(1.00, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
                t.BeginAnimation(prop, a);
            }
        }

        // ── Popup ──────────────────────────────────────────────────────────
        private void OpenPopup()
        {
            _popup = new FolderPopupWindow(Folder);
            _popup.Closed += (s, e) => _popup = null;

            var screen = SystemParameters.WorkArea;
            double left = Left + Width + 8;
            double top  = Top;
            if (left + 340 > screen.Right)  left = Left - 340 - 8;
            if (top  + 420 > screen.Bottom) top  = screen.Bottom - 420;

            _popup.Left = left;
            _popup.Top  = Math.Max(top, 0);
            _popup.Show();
            // Steal focus from the widget so the popup's Deactivated
            // event fires correctly when the user clicks elsewhere.
            _popup.Activate();
        }

        private void EditFolder_Click(object sender, RoutedEventArgs e)
        {
            var win = new ThemeSettingsWindow(Folder);
            win.ShowDialog();
            App.DataStore.Save();
            Refresh();
        }

        private void DeleteFolder_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show($"Delete folder \"{Folder.Name}\"?", "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                ((App)Application.Current).RemoveFolder(Folder);
        }
    }
}
