using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using DesktopFolders.Helpers;
using Microsoft.Win32;
using DesktopFolders.Models;

namespace DesktopFolders.Views
{
    public partial class FolderPopupWindow : Window
    {
        private readonly AppFolder _folder;
        private ThemeColors _colors;
        private bool _closing = false;

        // ── Drag-to-reorder state ──────────────────────────────────────────
        // Uses WPF DragDrop (not mouse capture) so file-drop still works
        private int    _dragSourceIndex = -1;
        private Border? _dropIndicator;

        public FolderPopupWindow(AppFolder folder)
        {
            InitializeComponent();
            _folder = folder;
            _colors = ThemeManager.GetColors();
            ApplyTheme();
            BuildAppGrid();
            Loaded      += (s, e) => PlayOpenAnimation();
            Deactivated += OnDeactivated;

            // Close on any click outside the popup — including clicks on the
            // desktop where no WPF window exists (Deactivated won't fire there).
            System.Windows.Input.InputManager.Current.PostProcessInput += OnGlobalMouseDown;
            Closed += (s, e) =>
                System.Windows.Input.InputManager.Current.PostProcessInput -= OnGlobalMouseDown;
        }

        private void OnGlobalMouseDown(object sender,
            System.Windows.Input.ProcessInputEventArgs e)
        {
            if (_closing) return;
            if (e.StagingItem.Input is System.Windows.Input.MouseButtonEventArgs mb
                && mb.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                // Use screen coordinates so this works regardless of which
                // window generated the input event.
                try
                {
                    var origin = PointToScreen(new Point(0, 0));
                    var pos    = mb.GetPosition(this);
                    // GetPosition returns coords relative to the WPF root
                    // which is reliable when the popup IS the active window,
                    // but falls back gracefully via the screen-bounds check.
                    var screenPos = PointToScreen(pos);
                    bool inside = screenPos.X >= origin.X
                               && screenPos.Y >= origin.Y
                               && screenPos.X <= origin.X + ActualWidth
                               && screenPos.Y <= origin.Y + ActualHeight;
                    if (inside) return;
                }
                catch { /* PointToScreen can throw if window not shown yet */ }

                SafeClose();
            }
        }

        // ── Animations ─────────────────────────────────────────────────────

        private void PlayOpenAnimation()
        {
            double cx = ActualWidth / 2, cy = ActualHeight / 2;
            switch (App.DataStore.Theme.PopupAnimation)
            {
                case PopupAnimation.Expand:
                {
                    var st = new ScaleTransform(0.3, 0.3, cx, cy);
                    MainPanel.RenderTransform = st; MainPanel.Opacity = 0;
                    var kf = KF(new[]{(0.3,0),(1.06,260),(0.97,340),(1.0,400)}, true);
                    var fd = KF(new[]{(0.0,0),(1.0,160)}, false);
                    st.BeginAnimation(ScaleTransform.ScaleXProperty, kf);
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, kf);
                    MainPanel.BeginAnimation(OpacityProperty, fd);
                    break;
                }
                case PopupAnimation.Slide:
                {
                    var tr = new TranslateTransform(0, 40);
                    MainPanel.RenderTransform = tr; MainPanel.Opacity = 0;
                    var sl = KF(new[]{(40.0,0),(-6.0,280),(0.0,380)}, true);
                    var fd = KF(new[]{(0.0,0),(1.0,200)}, false);
                    tr.BeginAnimation(TranslateTransform.YProperty, sl);
                    MainPanel.BeginAnimation(OpacityProperty, fd);
                    break;
                }
                case PopupAnimation.Fade:
                {
                    var fd = KF(new[]{(0.0,0),(0.0,40),(1.0,320)}, true);
                    MainPanel.Opacity = 0; MainPanel.BeginAnimation(OpacityProperty, fd);
                    break;
                }
            }
        }

        private void PlayCloseAnimation(Action onComplete)
        {
            switch (App.DataStore.Theme.PopupAnimation)
            {
                case PopupAnimation.Expand:
                {
                    var st = new ScaleTransform(1,1,ActualWidth/2,ActualHeight/2);
                    MainPanel.RenderTransform = st;
                    var kf = KF(new[]{(1.0,0),(1.04,60),(0.3,200)}, false);
                    var fd = KF(new[]{(1.0,0),(1.0,60),(0.0,200)}, false);
                    fd.Completed += (s,e) => onComplete();
                    st.BeginAnimation(ScaleTransform.ScaleXProperty, kf);
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, kf);
                    MainPanel.BeginAnimation(OpacityProperty, fd);
                    break;
                }
                case PopupAnimation.Slide:
                {
                    var tr = new TranslateTransform();
                    MainPanel.RenderTransform = tr;
                    var sl = KF(new[]{(0.0,0),(30.0,200)}, false);
                    var fd = KF(new[]{(1.0,0),(0.0,200)}, false);
                    fd.Completed += (s,e) => onComplete();
                    tr.BeginAnimation(TranslateTransform.YProperty, sl);
                    MainPanel.BeginAnimation(OpacityProperty, fd);
                    break;
                }
                case PopupAnimation.Fade:
                {
                    var fd = KF(new[]{(1.0,0),(0.0,180)}, false);
                    fd.Completed += (s,e) => onComplete();
                    MainPanel.BeginAnimation(OpacityProperty, fd);
                    break;
                }
                default: onComplete(); break;
            }
        }

        // Animation helper — builds a DoubleAnimationUsingKeyFrames from (value, ms) pairs
        private static DoubleAnimationUsingKeyFrames KF((double v, int ms)[] frames, bool easeOut)
        {
            var a = new DoubleAnimationUsingKeyFrames();
            for (int i = 0; i < frames.Length; i++)
            {
                var kt = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(frames[i].ms));
                if (easeOut && i == 1 && frames.Length > 2)
                    a.KeyFrames.Add(new EasingDoubleKeyFrame(frames[i].v, kt)
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                else
                    a.KeyFrames.Add(new EasingDoubleKeyFrame(frames[i].v, kt));
            }
            return a;
        }

        // ── Theme ──────────────────────────────────────────────────────────

        private void ApplyTheme()
        {
            _colors = ThemeManager.GetColors();
            MainPanel.Background      = new SolidColorBrush(_colors.PopupBackground);
            MainPanel.BorderBrush     = new SolidColorBrush(_colors.PopupBorder);
            MainPanel.BorderThickness = new Thickness(1);
            FolderTitle.Text     = _folder.Name;
            FolderTitle.FontSize = App.DataStore.Theme.FontSize + 4;
            try { FolderTitle.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(_folder.Color)); }
            catch { FolderTitle.Foreground = new SolidColorBrush(_colors.TitleForeground); }
            SettingsBtn.Foreground = new SolidColorBrush(_colors.LabelForeground);
            DropHint.BorderBrush   = new SolidColorBrush(_colors.PopupBorder);
            DropHint.Background    = new SolidColorBrush(
                Color.FromArgb(40, _colors.PopupBorder.R, _colors.PopupBorder.G, _colors.PopupBorder.B));
        }

        // ── App grid ───────────────────────────────────────────────────────

        private void BuildAppGrid()
        {
            AppGrid.Children.Clear();
            if (_folder.Apps.Count == 0)
            {
                EmptyLabel.Visibility = Visibility.Visible;
                EmptyLabel.Foreground = new SolidColorBrush(_colors.EmptyForeground);
                return;
            }
            EmptyLabel.Visibility = Visibility.Collapsed;
            var t = App.DataStore.Theme;
            double cell = ThemeManager.GetIconSize() + 22 + 10;
            AppGrid.MaxWidth = cell * Math.Max(1, t.PopupColumns) + 8;
            for (int i = 0; i < _folder.Apps.Count; i++)
                AppGrid.Children.Add(CreateAppButton(_folder.Apps[i], i));
        }

        private UIElement CreateAppButton(AppEntry app, int index)
        {
            var t           = App.DataStore.Theme;
            double iconSize = ThemeManager.GetIconSize();
            double cr       = GetIconBgCornerRadius(iconSize);
            double containerW = iconSize + 22;
            bool isSubFolder  = !string.IsNullOrEmpty(app.SubFolderId);

            var container = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Width       = containerW,
                Margin      = new Thickness(5, 4, 5, t.ShowAppLabels ? 8 : 4),
                Cursor      = Cursors.Hand,
                Tag         = index,
                AllowDrop   = false   // file-drop handled by MainPanel, not per-cell
            };

            // ── Icon background ────────────────────────────────────────────
            var iconBorder = new Border
            {
                Width               = iconSize, Height = iconSize,
                CornerRadius        = new CornerRadius(isSubFolder ? iconSize * 0.21 : cr),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background          = isSubFolder ? BuildSubFolderBackground(app)
                                    : (t.IconBgStyle == IconBgStyle.None ? Brushes.Transparent
                                       : new SolidColorBrush(_colors.AppIconBackground)),
                Margin              = new Thickness(0, 0, 0, t.ShowAppLabels ? 4 : 0)
            };
            if (t.IconBgStyle != IconBgStyle.None || isSubFolder)
                iconBorder.Effect = new DropShadowEffect
                    { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 1, Opacity = 0.4 };

            var iconImg = new System.Windows.Controls.Image
            {
                Width  = isSubFolder ? iconSize * 0.55 : (t.IconBgStyle == IconBgStyle.None ? iconSize : iconSize * 0.65),
                Height = isSubFolder ? iconSize * 0.55 : (t.IconBgStyle == IconBgStyle.None ? iconSize : iconSize * 0.65),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Stretch             = Stretch.Uniform
            };
            RenderOptions.SetBitmapScalingMode(iconImg, BitmapScalingMode.HighQuality);
            // Note: iconBorder.Child is NOT set here — it's always assigned inside
            // the async callback once we know what we're placing, preventing the
            // "already the logical child of another element" InvalidOperationException.

            if (isSubFolder)
            {
                // Sub-folder: build composite content synchronously — no iconImg involved
                iconBorder.Child = BuildSubFolderIconContent(app, iconSize);
            }
            else if (!string.IsNullOrEmpty(app.IconPath) && File.Exists(app.IconPath))
            {
                // Custom icon override: load on background thread, assign in callback
                var capturedIconPath = app.IconPath!;
                IconHelper.LoadIconAsync(capturedIconPath, src =>
                {
                    // iconImg has no parent yet — safe to set Source and parent it now
                    iconImg.Source   = src;
                    iconBorder.Child = iconImg;
                });
                // Fallback: if custom icon fails, LoadIconAsync won't call back at all,
                // so kick off an exe-icon load that will parent iconImg itself
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    if (iconBorder.Child != null) return;   // custom icon already loaded
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (iconBorder.Child == null)
                            LoadExeIconAsync(app.ExecutablePath, iconImg, iconBorder, iconSize, t);
                    }));
                });
            }
            else
            {
                // Normal app: async load, callback parents iconImg
                LoadExeIconAsync(app.ExecutablePath, iconImg, iconBorder, iconSize, t);
            }

            container.Children.Add(iconBorder);

            // ── Label ──────────────────────────────────────────────────────
            if (t.ShowAppLabels)
            {
                var label = new TextBlock
                {
                    Text              = app.Name,
                    Foreground        = new SolidColorBrush(_colors.LabelForeground),
                    FontSize          = t.FontSize,
                    FontFamily        = new FontFamily("Segoe UI"),
                    TextAlignment     = TextAlignment.Center,
                    TextWrapping      = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MaxWidth          = containerW
                };
                label.Effect = new DropShadowEffect
                    { Color = Colors.Black, BlurRadius = 3, ShadowDepth = 1, Opacity = 0.6 };
                container.Children.Add(label);
            }

            // ── Hover ──────────────────────────────────────────────────────
            container.MouseEnter += (s, e) =>
            {
                iconBorder.Opacity = 0.78;
                iconBorder.RenderTransform = new ScaleTransform(1.1, 1.1, iconSize / 2, iconSize / 2);
            };
            container.MouseLeave += (s, e) =>
            {
                iconBorder.Opacity = 1.0;
                iconBorder.RenderTransform = null;
            };

            // ── Context menu ──────────────────────────────────────────────
            var ctx = new ContextMenu();
            ApplyContextMenuStyle(ctx);

            // Suppress global-click-to-close while the menu is open
            ctx.Opened += (s, e) =>
                System.Windows.Input.InputManager.Current.PostProcessInput -= OnGlobalMouseDown;
            ctx.Closed += (s, e) =>
                System.Windows.Input.InputManager.Current.PostProcessInput += OnGlobalMouseDown;

            // ── Rename ──────────────────────────────────────────────────────
            ctx.Items.Add(MakeMenuItem("✏  Rename…",           () => RenameApp(app)));

            // ── Icon management ────────────────────────────────────────────
            ctx.Items.Add(MakeMenuItem("🖼  Set Custom Icon…",   () => SetCustomIcon(app)));
            var clearIconItem = MakeMenuItem("↺  Reset to Default Icon", () =>
            {
                app.IconPath = null;
                App.DataStore.Save();
                BuildAppGrid();
            });
            clearIconItem.IsEnabled = !string.IsNullOrEmpty(app.IconPath);
            ctx.Items.Add(clearIconItem);

            ctx.Items.Add(MakeSeparator());

            // ── File location items ──────────────────────────────────────────
            // "Open File Location" — highlights the shortcut/exe itself in Explorer
            var locItem = MakeMenuItem("📂  Open File Location", () => OpenFileLocation(app));
            locItem.IsEnabled = !string.IsNullOrEmpty(app.ExecutablePath)
                             && File.Exists(app.ExecutablePath);
            ctx.Items.Add(locItem);

            // "Open Target Path" — resolves .lnk target and shows that file.
            // Only added for .lnk shortcuts; uses IWshRuntimeLibrary already in project.
            if (app.ExecutablePath?.EndsWith(".lnk",
                    StringComparison.OrdinalIgnoreCase) == true)
            {
                string resolved = Helpers.ShortcutHelper.Resolve(app.ExecutablePath);
                var targetItem  = MakeMenuItem("🔗  Open Target Path", () =>
                {
                    string r = Helpers.ShortcutHelper.Resolve(app.ExecutablePath!);
                    if (!string.IsNullOrEmpty(r) && File.Exists(r))
                        Process.Start(new ProcessStartInfo
                        {
                            FileName        = "explorer.exe",
                            Arguments       = $"/select,\"{r}\"",
                            UseShellExecute = true
                        });
                });
                targetItem.IsEnabled = !string.IsNullOrEmpty(resolved)
                                    && File.Exists(resolved);
                ctx.Items.Add(targetItem);
            }

            ctx.Items.Add(MakeSeparator());

            // ── Remove ────────────────────────────────────────────────────────
            var removeItem = MakeMenuItem($"✕  Remove \"{app.Name}\" from folder", () =>
            {
                _folder.Apps.Remove(app);
                App.DataStore.Save();
                BuildAppGrid();
            });
            removeItem.Foreground = new SolidColorBrush(Color.FromRgb(220, 80, 80));
            ctx.Items.Add(removeItem);

            container.ContextMenu = ctx;

            // ── Drag-to-reorder: use WPF DragDrop, NOT mouse capture ───────
            // MouseDown starts a potential drag; PreviewMouseMove initiates DragDrop
            container.PreviewMouseLeftButtonDown += (s, e) =>
            {
                _dragSourceIndex = index;
            };

            container.PreviewMouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed && _dragSourceIndex == index)
                {
                    var delta = e.GetPosition(container) - new Point(containerW / 2, iconSize / 2);
                    if (Math.Abs(delta.X) > 8 || Math.Abs(delta.Y) > 8)
                    {
                        // Initiate a WPF drag. This releases mouse capture automatically.
                        var data = new DataObject("AppEntryIndex", index);
                        DragDrop.DoDragDrop(container, data, DragDropEffects.Move);
                    }
                }
            };

            // ── Click: launch or open sub-folder ──────────────────────────
            container.MouseLeftButtonUp += (s, e) =>
            {
                // Only fires when it was a click (not a drag)
                if (_dragSourceIndex == index)
                {
                    _dragSourceIndex = -1;
                    if (!string.IsNullOrEmpty(app.SubFolderId)) OpenSubFolder(app);
                    else LaunchApp(app);
                }
            };

            // ── Drop target for reorder ────────────────────────────────────
            container.AllowDrop = true;
            container.DragOver += (s, e) =>
            {
                if (e.Data.GetDataPresent("AppEntryIndex"))
                {
                    e.Effects = DragDropEffects.Move;
                    int hoveredIndex = index;
                    ShowDropIndicator(hoveredIndex);
                    e.Handled = true;
                }
            };
            container.Drop += (s, e) =>
            {
                RemoveDropIndicator();
                if (!e.Data.GetDataPresent("AppEntryIndex")) return;
                int from = (int)e.Data.GetData("AppEntryIndex");
                int to   = index;
                if (from != to && from >= 0 && from < _folder.Apps.Count)
                {
                    var item = _folder.Apps[from];
                    _folder.Apps.RemoveAt(from);
                    int insertAt = to > from ? to - 1 : to;
                    insertAt = Math.Max(0, Math.Min(_folder.Apps.Count, insertAt));
                    _folder.Apps.Insert(insertAt, item);
                    App.DataStore.Save();
                    BuildAppGrid();
                }
                e.Handled = true;
            };

            return container;
        }

        // ── Drop indicator ─────────────────────────────────────────────────

        private void ShowDropIndicator(int beforeIndex)
        {
            RemoveDropIndicator();
            _dropIndicator = new Border
            {
                Width           = 3,
                Background      = new SolidColorBrush(_colors.TitleForeground),
                CornerRadius    = new CornerRadius(2),
                IsHitTestVisible = false,
                Margin          = new Thickness(0, 4, 0, 4)
            };
            int insertIdx = Math.Min(beforeIndex, AppGrid.Children.Count);
            AppGrid.Children.Insert(insertIdx, _dropIndicator);
        }

        private void RemoveDropIndicator()
        {
            if (_dropIndicator != null)
            {
                AppGrid.Children.Remove(_dropIndicator);
                _dropIndicator = null;
            }
        }

        // ── Sub-folder icon helpers ────────────────────────────────────────

        private Brush BuildSubFolderBackground(AppEntry app)
        {
            var target = App.DataStore.Folders.Find(f => f.Id == app.SubFolderId);
            string color = target?.Color ?? _folder.Color;
            try
            {
                var c    = (Color)ColorConverter.ConvertFromString(color);
                var dark = Color.FromArgb(255, (byte)Math.Max(c.R-45,0), (byte)Math.Max(c.G-45,0), (byte)Math.Max(c.B-45,0));
                var brush = new LinearGradientBrush { StartPoint = new Point(0,0), EndPoint = new Point(0,1) };
                brush.GradientStops.Add(new GradientStop(c,    0));
                brush.GradientStops.Add(new GradientStop(dark, 1));
                return brush;
            }
            catch { return new SolidColorBrush(_colors.AppIconBackground); }
        }

        private UIElement BuildSubFolderIconContent(AppEntry app, double iconSize)
        {
            var target = App.DataStore.Folders.Find(f => f.Id == app.SubFolderId);
            var grid = new Grid();
            grid.Children.Add(new TextBlock
            {
                Text = "📁", FontSize = iconSize * 0.48,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, iconSize * 0.05)
            });
            if (target != null && target.Apps.Count > 0)
            {
                var miniPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Bottom,
                    Margin              = new Thickness(0, 0, 0, 2)
                };
                int shown = Math.Min(target.Apps.Count, 2);
                for (int m = 0; m < shown; m++)
                {
                    var miniImg = new System.Windows.Controls.Image
                    {
                        Width = iconSize * 0.22, Height = iconSize * 0.22,
                        Margin = new Thickness(1, 0, 1, 0), Stretch = Stretch.Uniform
                    };
                    RenderOptions.SetBitmapScalingMode(miniImg, BitmapScalingMode.HighQuality);
                    var capturedApp = target.Apps[m];
                    IconHelper.LoadIconAsync(capturedApp.ExecutablePath, src => miniImg.Source = src);
                    miniPanel.Children.Add(miniImg);
                }
                grid.Children.Add(miniPanel);
            }
            return grid;
        }

        // ── Icon loading ───────────────────────────────────────────────────

        private static void LoadExeIconAsync(string path,
            System.Windows.Controls.Image iconImg,
            Border iconBorder, double iconSize, ThemeSettings t)
        {
            // iconImg must NOT be parented before this is called.
            // This method is the sole place that parents it.
            IconHelper.LoadIconAsync(path, src =>
            {
                // Defensive: if something else already parented iconImg, bail
                if (iconImg.Parent != null) return;

                iconImg.Source = src;

                if (t.HideShortcutArrows)
                {
                    // Overlay a matching-colour patch to hide the shortcut arrow
                    var grid = new Grid();
                    grid.Children.Add(iconImg);          // iconImg parented to grid
                    grid.Children.Add(new Border
                    {
                        Width               = iconSize * 0.38,
                        Height              = iconSize * 0.38,
                        Background          = iconBorder.Background ?? Brushes.Transparent,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment   = VerticalAlignment.Bottom
                    });
                    iconBorder.Child = grid;             // grid parented to border
                }
                else
                {
                    iconBorder.Child = iconImg;          // iconImg parented directly to border
                }
            });
        }

        private double GetIconBgCornerRadius(double sz) =>
            App.DataStore.Theme.IconBgStyle switch
            {
                IconBgStyle.Circle => sz / 2,
                IconBgStyle.Square => 4,
                IconBgStyle.None   => 0,
                _                  => sz * 0.22
            };

        // ── Sub-folder navigation ──────────────────────────────────────────

        private void OpenSubFolder(AppEntry app)
        {
            var target = App.DataStore.Folders.Find(f => f.Id == app.SubFolderId);
            if (target == null)
            {
                MessageBox.Show("The linked folder no longer exists.", "Folder Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Deactivated -= OnDeactivated;
            var sub = new FolderPopupWindow(target);
            sub.Left = Left + 20; sub.Top = Top + 20;
            sub.Closed += (s, e) => { if (!_closing) Deactivated += OnDeactivated; };
            sub.Show();
        }

        // ── Launch / close ─────────────────────────────────────────────────

        private void OnDeactivated(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_closing) SafeClose();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void SafeClose()
        {
            if (_closing) return;
            _closing = true;
            Deactivated -= OnDeactivated;
            PlayCloseAnimation(() => { try { Close(); } catch { } });
        }

        private void LaunchApp(AppEntry app)
        {
            // Sub-folder entries should never reach here, but guard anyway.
            // An empty ExecutablePath would crash Process.Start.
            if (string.IsNullOrWhiteSpace(app.ExecutablePath))
            {
                if (!string.IsNullOrEmpty(app.SubFolderId)) OpenSubFolder(app);
                return;
            }

            Deactivated -= OnDeactivated;
            _closing = true;
            try
            {
                string path = app.ExecutablePath;
                string ext  = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".url")
                {
                    string? url = IconHelper.ReadUrlFromFile(path);
                    Process.Start(new ProcessStartInfo(
                        string.IsNullOrEmpty(url) ? path : url)
                        { UseShellExecute = true });
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = path,
                        Arguments       = app.Arguments ?? "",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not launch {app.Name}:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { Close(); } catch { }
                }), System.Windows.Threading.DispatcherPriority.Normal);
            }
        }

        // ── File-drop onto popup ────────────────────────────────────────────
        // NOTE: MainPanel handles file-drop. Individual app cells use AllowDrop=true
        // only for the internal AppEntryIndex format, and fall through for FileDrop.

        private void Panel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                DropHint.Visibility = Visibility.Visible;
            }
            else if (e.Data.GetDataPresent("AppEntryIndex"))
            {
                e.Effects = DragDropEffects.Move;  // reorder in progress
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Panel_DragLeave(object sender, DragEventArgs e)
        {
            DropHint.Visibility = Visibility.Collapsed;
            // Only remove indicator if this is a file drop leaving (not a reorder)
            if (!e.Data.GetDataPresent("AppEntryIndex"))
                RemoveDropIndicator();
        }

        private void Panel_Drop(object sender, DragEventArgs e)
        {
            DropHint.Visibility = Visibility.Collapsed;
            RemoveDropIndicator();

            // File drop from Explorer
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                bool added = false;
                foreach (var file in (string[])e.Data.GetData(DataFormats.FileDrop))
                {
                    string? storable = ShortcutHelper.ResolveToStorable(file);
                    if (storable == null) continue;
                    if (_folder.Apps.Exists(a => string.Equals(a.ExecutablePath, storable,
                        StringComparison.OrdinalIgnoreCase))) continue;
                    _folder.Apps.Add(new AppEntry
                    {
                        Name           = Path.GetFileNameWithoutExtension(file),
                        ExecutablePath = storable
                    });
                    added = true;
                }
                if (added) { App.DataStore.Save(); BuildAppGrid(); }
                e.Handled = true;
            }
        }

        // ── Settings button ────────────────────────────────────────────────

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            Deactivated -= OnDeactivated;

            // The popup is Topmost=True, which means it lives in the OS topmost
            // z-band and would always render above the settings window.
            // Drop it out of that band while settings is open, then restore.
            Topmost = false;

            var win = new ThemeSettingsWindow(_folder);
            win.Closed += (_, _) =>
            {
                // Restore popup to topmost band once settings is dismissed.
                Topmost = true;
                Deactivated += OnDeactivated;
                _colors = ThemeManager.GetColors();
                ApplyTheme();
                BuildAppGrid();
            };
            win.Show();   // non-blocking — lets WPF process messages normally
        }
        // ── Rename ────────────────────────────────────────────────────────

        private void RenameApp(AppEntry app)
        {
            // Temporarily detach the close-on-deactivate handler so the
            // rename dialog doesn't cause the popup to vanish.
            Deactivated -= OnDeactivated;

            var dlg = new RenameDialog(app.Name, _colors)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.NewName))
            {
                app.Name = dlg.NewName.Trim();
                App.DataStore.Save();
                BuildAppGrid();
            }

            Deactivated += OnDeactivated;
        }

        // ── Set Custom Icon ────────────────────────────────────────────────

        private void SetCustomIcon(AppEntry app)
        {
            Deactivated -= OnDeactivated;

            var dlg = new OpenFileDialog
            {
                Title       = $"Choose icon for \"{app.Name}\"",
                Filter      = "Image & Icon files|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe;*.dll" +
                              "|ICO files (*.ico)|*.ico" +
                              "|PNG / JPEG (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg" +
                              "|Executables (*.exe;*.dll)|*.exe;*.dll" +
                              "|All files (*.*)|*.*",
                FilterIndex = 1
            };

            if (dlg.ShowDialog() == true)
            {
                app.IconPath = dlg.FileName;
                App.DataStore.Save();
                BuildAppGrid();
            }

            Deactivated += OnDeactivated;
        }

        // ── Open File Location ─────────────────────────────────────────────

        private static void OpenFileLocation(AppEntry app)
        {
            if (string.IsNullOrEmpty(app.ExecutablePath)
                || !File.Exists(app.ExecutablePath))
                return;

            // /select,<path> opens Explorer with the file highlighted
            Process.Start(new ProcessStartInfo
            {
                FileName        = "explorer.exe",
                Arguments       = $"/select,\"{app.ExecutablePath}\"",
                UseShellExecute = true
            });
        }


        // ══ Context menu theming helpers ═══════════════════════════════════════════

        private Color _menuFg = Colors.White;

        private MenuItem MakeMenuItem(string header, Action onClick)
        {
            var item = new MenuItem { Header = header };
            ApplyMenuItemStyle(item);
            item.Click += (_, _) => onClick();
            return item;
        }

        private Separator MakeSeparator()
        {
            var sep = new Separator();
            var col = Color.FromArgb(40, _colors.PopupBorder.R,
                                         _colors.PopupBorder.G,
                                         _colors.PopupBorder.B);
            sep.Background = new SolidColorBrush(col);
            sep.Height     = 1;
            sep.Margin     = new Thickness(8, 3, 8, 3);
            return sep;
        }

        /// <summary>
        /// Templates the ContextMenu outer border to be rounded and theme-coloured.
        /// Sets _menuFg as a side-effect so MakeMenuItem can use the right text colour.
        /// </summary>
        private void ApplyContextMenuStyle(ContextMenu menu)
        {
            var c  = _colors;
            var bg = Color.FromRgb(
                (byte)Math.Max(c.PopupBackground.R - 8, 0),
                (byte)Math.Max(c.PopupBackground.G - 8, 0),
                (byte)Math.Max(c.PopupBackground.B - 8, 0));

            // Perceived luminance determines text colour
            double lum = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B;
            _menuFg = lum > 140 ? Color.FromRgb(20, 20, 30) : Colors.White;

            var tpl    = new ControlTemplate(typeof(ContextMenu));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty,
                new SolidColorBrush(bg));
            border.SetValue(Border.BorderBrushProperty,
                new SolidColorBrush(c.PopupBorder));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.CornerRadiusProperty,    new CornerRadius(10));
            border.SetValue(Border.PaddingProperty,         new Thickness(4, 6, 4, 6));
            border.SetValue(Border.EffectProperty,
                new DropShadowEffect
                {
                    Color       = Colors.Black,
                    BlurRadius  = 20,
                    ShadowDepth = 5,
                    Opacity     = 0.45
                });
            border.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
            tpl.VisualTree = border;
            menu.Template  = tpl;

            // Also set these for any fallback rendering path
            menu.Background = new SolidColorBrush(bg);
            menu.Foreground = new SolidColorBrush(_menuFg);
        }

        private void ApplyMenuItemStyle(MenuItem item)
        {
            var hoverBrush = new SolidColorBrush(
                Color.FromArgb(45,
                    _colors.AccentColor.R,
                    _colors.AccentColor.G,
                    _colors.AccentColor.B));

            var tpl    = new ControlTemplate(typeof(MenuItem));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.PaddingProperty,      new Thickness(12, 7, 12, 7));
            border.SetValue(Border.MarginProperty,       new Thickness(2, 1, 2, 1));
            border.SetBinding(Border.BackgroundProperty,
                new System.Windows.Data.Binding("IsHighlighted")
                {
                    RelativeSource = new System.Windows.Data.RelativeSource(
                        System.Windows.Data.RelativeSourceMode.TemplatedParent),
                    Converter = new Helpers.HighlightBgConverter(
                        hoverBrush, Brushes.Transparent)
                });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.ContentSourceProperty,   "Header");
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty,
                VerticalAlignment.Center);
            border.AppendChild(cp);
            tpl.VisualTree = border;

            item.Template   = tpl;
            item.Foreground = new SolidColorBrush(_menuFg);
            item.FontSize   = 12.5;
            item.Cursor     = Cursors.Hand;
        }


    }
}
