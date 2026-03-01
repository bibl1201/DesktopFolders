using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using DesktopFolders.Models;
using Microsoft.Win32;

namespace DesktopFolders.Views
{
    public partial class ThemeSettingsWindow : Window
    {
        private bool       _loading = true;
        private AppFolder? _activeFolder;
        public ThemeSettingsWindow(AppFolder? folder = null)
        {
            InitializeComponent();
            _activeFolder = folder;
            LoadCurrentSettings();
            ApplyWindowTheme();
            _loading = false;
            ShowSection(_activeFolder != null ? "Folder" : "Theme");

            // Stay topmost for the window's full lifetime — the popup lowers
            // itself out of the topmost z-band before opening settings, so there
            // is no conflict. Topmost=true here ensures settings sits above every
            // other regular (non-topmost) application window, matching the
            // expected "above popup, below other focused apps" behaviour.
            Topmost = true;
            Loaded += (_, _) => { Activate(); Focus(); };
        }

        // ── Navigation ────────────────────────────────────────────────────

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string section)
                ShowSection(section);
        }

        // Sections under Appearance — keeps sub-nav expanded when any of them is active
        private static readonly string[] AppearanceSections = { "Theme", "Popup", "Widget" };

        private void ShowSection(string section)
        {
            // "Theme" is the combined Look & Feel section (theme + presets)
            PanelLookFeel.Visibility = section == "Theme"   ? Visibility.Visible : Visibility.Collapsed;
            PanelPopup.Visibility    = section == "Popup"   ? Visibility.Visible : Visibility.Collapsed;
            PanelWidget.Visibility   = section == "Widget"  ? Visibility.Visible : Visibility.Collapsed;
            PanelFolder.Visibility   = section == "Folder"  ? Visibility.Visible : Visibility.Collapsed;
            PanelBackup.Visibility   = section == "Backup"  ? Visibility.Visible : Visibility.Collapsed;

            (PageIcon.Text, PageTitle.Text, PageSubtitle.Text) = section switch
            {
                "Theme"      => ("🎨", "Look & Feel",     "Color theme, font size and style presets"),

                "Popup"      => ("📂", "Popup Window",     "Icons, layout, animation and display"),
                "Widget"     => ("🗂",  "Widget",           "Shape, style and opacity"),
                "Folder"     => ("📁", "Folder Settings",  _activeFolder != null
                                                               ? $"Editing: {_activeFolder.Name}"
                                                               : "Select a folder to edit"),
                "Appearance" => ("🎨", "Appearance",       "Theme, presets, popup and widget options"),
                "Backup"     => ("💾", "Backup & Restore", "Export and import all your data"),
                _            => ("⚙",  section, "")
            };

            var c        = ThemeManager.GetColors();
            var cc       = GetContrastForeground(c);
            var fg       = new SolidColorBrush(cc);
            var dim      = new SolidColorBrush(Color.FromArgb(100, cc.R, cc.G, cc.B));
            var accentBg = new SolidColorBrush(Color.FromArgb(35,
                               c.AccentColor.R, c.AccentColor.G, c.AccentColor.B));

            // Appearance parent button — active/highlighted when any sub-section is open
            bool appearanceActive = AppearanceSections.Contains(section);
            NavAppearance.Foreground = appearanceActive ? fg : dim;
            NavAppearance.Background = appearanceActive ? accentBg : Brushes.Transparent;
            NavAppearance.FontWeight = appearanceActive ? FontWeights.SemiBold : FontWeights.Normal;
            AppearanceChevron.Text   = appearanceActive ? " ∨" : " ›";

            // Show sub-nav if Appearance section is active or if clicking the parent
            AppearanceSubNav.Visibility =
                (appearanceActive || section == "Appearance")
                    ? Visibility.Visible : Visibility.Collapsed;

            // Style sub-nav buttons
            foreach (var (btn, name) in new[]
            {
                (NavTheme,  "Theme"),
                (NavPopup,  "Popup"),
                (NavWidget, "Widget"),
            })
            {
                bool active = name == section;
                btn.Foreground = active ? fg : new SolidColorBrush(Color.FromArgb(140, cc.R, cc.G, cc.B));
                btn.Background = active ? accentBg : Brushes.Transparent;
                btn.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            }

            // Folder nav button
            bool folderActive = section == "Folder";
            NavFolder.Foreground = folderActive ? fg : dim;
            NavFolder.Background = folderActive ? accentBg : Brushes.Transparent;
            NavFolder.FontWeight = folderActive ? FontWeights.SemiBold : FontWeights.Normal;

            // Backup nav button
            bool backupActive = section == "Backup";
            NavBackup.Foreground = backupActive ? fg : dim;
            NavBackup.Background = backupActive ? accentBg : Brushes.Transparent;
            NavBackup.FontWeight = backupActive ? FontWeights.SemiBold : FontWeights.Normal;

            // If clicking top-level Appearance with no sub-selection, default to Theme
            if (section == "Appearance")
            {
                ShowSection("Theme");
                return;
            }

            UpdateLivePreview();
            if (section == "Folder") LoadFolderTab();
            if (section == "Backup") LoadBackupSection();
        }

        // ── Theme-aware colour helper ─────────────────────────────────────
        // Returns a foreground colour with sufficient contrast against the
        // current editor background — white on dark, near-black on light.

        private static Color GetContrastForeground(ThemeColors c)
        {
            // Perceived luminance of the background
            double lum = 0.299 * c.EditorBackground.R
                       + 0.587 * c.EditorBackground.G
                       + 0.114 * c.EditorBackground.B;
            return lum > 140 ? Color.FromRgb(20, 20, 30) : Colors.White;
        }

        // ── Live preview ──────────────────────────────────────────────────

        private void UpdateLivePreview()
        {
            var c = ThemeManager.GetColors();
            var t = App.DataStore.Theme;

            PrevWidget.Background   = ThemeManager.GetWidgetBackground("#5B8CFF");
            PrevWidget.CornerRadius = ThemeManager.GetWidgetCornerRadius(60);
            PrevWidget.Effect       = ThemeManager.GetWidgetShadow();

            var labelFg = new SolidColorBrush(c.LabelForeground);
            PrevWidgetLabel.Foreground = labelFg;
            PrevWidgetLabel.FontSize   = Math.Max(t.FontSize - 2, 8);

            PrevPopup.Background      = new SolidColorBrush(c.PopupBackground);
            PrevPopup.BorderBrush     = new SolidColorBrush(c.PopupBorder);
            PrevPopup.BorderThickness = new Thickness(1);
            PrevPopupTitle.Foreground = new SolidColorBrush(c.TitleForeground);
            PrevPopupTitle.FontSize   = Math.Max(t.FontSize, 10);

            var iconBg = new SolidColorBrush(c.AppIconBackground);
            double sz  = 28;
            double cr  = t.IconBgStyle switch
            {
                IconBgStyle.Circle  => sz / 2,
                IconBgStyle.Square  => 4,
                IconBgStyle.None    => 0,
                _                   => sz * 0.22
            };
            var radii = new CornerRadius(cr);
            var bg    = t.IconBgStyle == IconBgStyle.None ? Brushes.Transparent : (Brush)iconBg;
            PrevIcon0.CornerRadius = radii; PrevIcon0.Background = bg;
            PrevIcon1.CornerRadius = radii; PrevIcon1.Background = bg;
            PrevIcon2.CornerRadius = radii; PrevIcon2.Background = bg;

            var previewBg = new SolidColorBrush(Color.FromArgb(22,
                c.EditorForeground.R, c.EditorForeground.G, c.EditorForeground.B));
            PreviewStrip.Background  = previewBg;
            PreviewStrip.BorderBrush = new SolidColorBrush(Color.FromArgb(30,
                c.EditorForeground.R, c.EditorForeground.G, c.EditorForeground.B));

            // "PREVIEW" label and "Widget/Popup" sublabels use contrast foreground
            var contrastFg = new SolidColorBrush(GetContrastForeground(c));
            PreviewLabel.Foreground = contrastFg;
        }

        // ── Theme application ──────────────────────────────────────────────

        private void ApplyWindowTheme()
        {
            var c       = ThemeManager.GetColors();
            var contrastColor = GetContrastForeground(c);
            var fg      = new SolidColorBrush(contrastColor);
            var fgDim   = new SolidColorBrush(Color.FromArgb(140,
                              contrastColor.R, contrastColor.G, contrastColor.B));

            Background      = new SolidColorBrush(c.EditorBackground);
            NavPanel.Background = new SolidColorBrush(Color.FromArgb(255,
                (byte)Math.Max(c.EditorBackground.R - 8, 0),
                (byte)Math.Max(c.EditorBackground.G - 8, 0),
                (byte)Math.Max(c.EditorBackground.B - 8, 0)));

            NavDivider.Background = new SolidColorBrush(Color.FromArgb(30,
                contrastColor.R, contrastColor.G, contrastColor.B));
            TitleBarBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(25,
                contrastColor.R, contrastColor.G, contrastColor.B));
            FooterBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(25,
                contrastColor.R, contrastColor.G, contrastColor.B));

            AppTitleLabel.Foreground = fg;
            PageTitle.Foreground     = fg;
            PageSubtitle.Foreground  = fgDim;
            NavLabelAppearance.Foreground = fgDim;
            NavLabelFolders.Foreground    = fgDim;
            NavLabelBackup.Foreground     = fgDim;
            AppearanceChevron.Foreground  = fgDim;

            // All interactive controls get contrast-aware foreground
            foreach (var el in new FrameworkElement[]
            {
                FontSizeLabel, OpacityLabel, WidgetOpacityLabel, ColumnsLabel,
                FolderTabHint, FolderColorHex, AccentInfoLabel,
                ThemeDark, ThemeLight, ThemeMidnight, ThemeSunset, ThemeAccent,
                SizeSmall, SizeNormal, SizeLarge,
                IconBgSquircle, IconBgCircle, IconBgSquare, IconBgNone,
                AnimNone, AnimExpand, AnimSlide, AnimFade,
                ShapeRounded, ShapeCircle, ShapeSharp,
                StyleGradient, StyleSolid, StyleGlass, StyleOutline,
                LabelShadow, LabelPill, LabelHidden,
                ShowLabelsCheck, RoundedIconsCheck, WidgetShadowCheck,
                ShowMiniIconsCheck, HideArrowsCheck,
                PresetiOSLightTitle, PresetiOSDarkTitle,
                PresetAndroidLightTitle, PresetAndroidDarkTitle,
                PresetWindowsLightTitle, PresetWindowsDarkTitle
            })
            {
                if (el is TextBlock tb)    tb.Foreground = fg;
                else if (el is RadioButton rb) rb.Foreground = fg;
                else if (el is CheckBox cb)    cb.Foreground = fg;
            }

            // Section headings (SH style: FontSize≤10, SemiBold) get dimmed
            foreach (var panel in new Panel[] { PanelLookFeel, PanelPopup, PanelWidget, PanelFolder, PanelBackup })
                ApplyHeadingFg(panel, fgDim, fg);

            // Preset card description text
            foreach (var pb in new[] {
                PresetiOSLight, PresetiOSDark,
                PresetAndroidLight, PresetAndroidDark,
                PresetWindowsLight, PresetWindowsDark })
            {
                var descTb = FindDescriptionTextBlock(pb);
                if (descTb != null) descTb.Foreground = fgDim;
            }

            CloseBtn.Background        = new SolidColorBrush(c.ButtonBackground);
            CloseBtn.Foreground        = fg;
            PreviewAnimBtn.Background  = new SolidColorBrush(c.ButtonSecondary);
            PreviewAnimBtn.Foreground  = fg;
            FolderAddAppBtn.Background  = new SolidColorBrush(c.ButtonSecondary);
            FolderAddAppBtn.Foreground  = fg;
            FolderBulkAddBtn.Background = new SolidColorBrush(c.ButtonSecondary);
            FolderBulkAddBtn.Foreground = fg;
            FolderSaveBtn.Background    = new SolidColorBrush(c.ButtonBackground);
            FolderSaveBtn.Foreground    = fg;
            BtnExport.Background = new SolidColorBrush(c.ButtonSecondary);
            BtnExport.Foreground = fg;
            BtnImport.Background = new SolidColorBrush(c.ButtonSecondary);
            BtnImport.Foreground = fg;

            FolderNameBox.Background  = new SolidColorBrush(c.EditorInputBackground);
            FolderNameBox.Foreground  = fg;
            FolderNameBox.BorderBrush = new SolidColorBrush(c.EditorBorder);
            FolderNameBox.CaretBrush  = fg;

            FolderAppsBorder.Background  = new SolidColorBrush(Color.FromArgb(20,
                contrastColor.R, contrastColor.G, contrastColor.B));
            FolderAppsBorder.BorderBrush = new SolidColorBrush(c.EditorBorder);

            // Preset card borders
            var presetBorder = new SolidColorBrush(Color.FromArgb(40,
                contrastColor.R, contrastColor.G, contrastColor.B));
            foreach (var pb in new[] {
                PresetiOSLight, PresetiOSDark,
                PresetAndroidLight, PresetAndroidDark,
                PresetWindowsLight, PresetWindowsDark })
                pb.BorderBrush = presetBorder;

            // Accent preview row
            bool isAccent = App.DataStore.Theme.Theme == AppTheme.Accent;
            AccentPreviewRow.Visibility = isAccent ? Visibility.Visible : Visibility.Collapsed;
            if (isAccent)
            {
                var accent = ThemeManager.GetWindowsAccentColor();
                AccentSwatch.Background = new SolidColorBrush(accent);
                AccentInfoLabel.Text    = $"System accent: #{accent.R:X2}{accent.G:X2}{accent.B:X2}";
            }

            UpdateLivePreview();
        }

        /// <summary>
        /// Recursively walks every TextBlock, RadioButton and CheckBox in the
        /// visual tree rooted at <paramref name="root"/> and applies the
        /// theme-appropriate foreground.  Heading-style elements (FontSize≤10,
        /// SemiBold) get the dimmed brush; everything else gets the full-contrast
        /// foreground.  This is necessary because some panels (e.g. PanelFolder)
        /// contain deeply-nested children that a shallow loop would miss.
        /// </summary>
        private static void ApplyTextTheme(DependencyObject root,
                                           Brush headingBrush, Brush defaultBrush)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                switch (child)
                {
                    case TextBlock tb:
                        tb.Foreground = (tb.FontSize <= 10 &&
                                         tb.FontWeight == FontWeights.SemiBold)
                            ? headingBrush : defaultBrush;
                        break;
                    case RadioButton rb:
                        rb.Foreground = defaultBrush;
                        break;
                    case CheckBox cb:
                        cb.Foreground = defaultBrush;
                        break;
                }
                // Recurse — but skip the live-preview and the FolderAppsPanel
                // (those are themed separately by UpdateLivePreview / RefreshFolderAppList)
                if (child is FrameworkElement fe &&
                    fe.Name != "PrevPopup" &&
                    fe.Name != "FolderAppsPanel")
                    ApplyTextTheme(child, headingBrush, defaultBrush);
            }
        }

        // Kept for the single remaining call-site that passes a Panel directly.
        private static void ApplyHeadingFg(Panel panel, Brush headingBrush, Brush defaultBrush)
            => ApplyTextTheme(panel, headingBrush, defaultBrush);

        private static TextBlock? FindDescriptionTextBlock(Button btn)
        {
            if (btn.Content is Grid g && g.Children.Count > 1 &&
                g.Children[1] is StackPanel sp && sp.Children.Count > 1 &&
                sp.Children[1] is TextBlock tb)
                return tb;
            return null;
        }

        // ── Presets ────────────────────────────────────────────────────────

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tag) return;
            var t = App.DataStore.Theme;

            // Tag format: "Style_Mode" e.g. "iOS_Light", "Android_Dark"
            string[] parts = tag.Split('_');
            if (parts.Length != 2) return;
            string style = parts[0];   // iOS | Android | Windows
            string mode  = parts[1];   // Light | Dark

            // Apply color theme
            t.Theme = mode == "Light" ? AppTheme.Light
                : style == "Android"  ? AppTheme.Midnight
                : AppTheme.Dark;

            // Apply style settings common to all variants of each OS style
            switch (style)
            {
                case "iOS":
                    t.WidgetShape    = WidgetShape.RoundedSquare;
                    t.WidgetStyle    = WidgetStyle.Glass;
                    t.LabelStyle     = LabelStyle.Pill;
                    t.IconBgStyle    = IconBgStyle.Squircle;
                    t.PopupAnimation = PopupAnimation.Fade;
                    t.PopupColumns   = 3;
                    t.ShowAppLabels  = true;
                    t.RoundedIcons   = true;
                    t.WidgetOpacity  = 0.92;
                    t.WidgetShadow   = false;
                    t.ShowMiniIcons  = true;
                    break;

                case "Android":
                    t.WidgetShape    = WidgetShape.Circle;
                    t.WidgetStyle    = WidgetStyle.Gradient;
                    t.LabelStyle     = LabelStyle.Pill;
                    t.IconBgStyle    = IconBgStyle.Circle;
                    t.PopupAnimation = PopupAnimation.Expand;
                    t.PopupColumns   = 4;
                    t.ShowAppLabels  = true;
                    t.RoundedIcons   = true;
                    t.WidgetOpacity  = 1.0;
                    t.WidgetShadow   = true;
                    t.ShowMiniIcons  = true;
                    break;

                case "Windows":
                    t.WidgetShape    = WidgetShape.Sharp;
                    t.WidgetStyle    = WidgetStyle.Solid;
                    t.LabelStyle     = LabelStyle.Pill;
                    t.IconBgStyle    = IconBgStyle.None;
                    t.PopupAnimation = PopupAnimation.Slide;
                    t.PopupColumns   = 4;
                    t.ShowAppLabels  = true;
                    t.RoundedIcons   = false;
                    t.WidgetOpacity  = 1.0;
                    t.WidgetShadow   = true;
                    t.ShowMiniIcons  = true;
                    break;

                default: return;
            }

            Save();
            _loading = true;
            LoadCurrentSettings();
            _loading = false;
            ApplyWindowTheme();
            ShowSection("Theme");

            // Flash card border green as confirmation
            var orig = btn.BorderBrush;
            btn.BorderBrush = new SolidColorBrush(Color.FromRgb(50, 190, 90));
            var timer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromSeconds(1.2) };
            timer.Tick += (s, _) => { timer.Stop(); btn.BorderBrush = orig; };
            timer.Start();
        }

        // ── Folder tab ─────────────────────────────────────────────────────

        private void LoadFolderTab()
        {
            if (_activeFolder == null)
            {
                FolderTabHint.Visibility   = Visibility.Visible;
                FolderEditPanel.Visibility = Visibility.Collapsed;
                return;
            }
            FolderTabHint.Visibility   = Visibility.Collapsed;
            FolderEditPanel.Visibility = Visibility.Visible;
            _loading = true;
            FolderNameBox.Text = _activeFolder.Name;
            _loading = false;
            UpdateFolderColorSwatch();
            RefreshFolderAppList();
        }

        private void UpdateFolderColorSwatch()
        {
            if (_activeFolder == null) return;
            try
            {
                var col = (Color)ColorConverter.ConvertFromString(_activeFolder.Color);
                FolderColorSwatch.Background = new SolidColorBrush(col);
                FolderColorHex.Text          = _activeFolder.Color.ToUpperInvariant();
            }
            catch { }
        }

        private void RefreshFolderAppList()
        {
            var c     = ThemeManager.GetColors();
            var cc    = GetContrastForeground(c);
            var fg    = new SolidColorBrush(cc);
            var fgDim = new SolidColorBrush(Color.FromArgb(140, cc.R, cc.G, cc.B));

            FolderAppsPanel.Children.Clear();
            if (_activeFolder == null) return;

            for (int i = 0; i < _activeFolder.Apps.Count; i++)
            {
                var app = _activeFolder.Apps[i];
                var idx = i;

                var row = new Border
                {
                    Padding      = new Thickness(10, 7, 10, 7),
                    Margin       = new Thickness(0, 0, 0, 2),
                    Background   = new SolidColorBrush(Color.FromArgb(22,
                                       cc.R, cc.G, cc.B)),
                    CornerRadius = new CornerRadius(6)
                };

                var outer = new StackPanel();

                // Main row: name + delete button
                var dp = new DockPanel();
                var del = new Button
                {
                    Content         = "✕",
                    Background      = Brushes.Transparent,
                    Foreground      = new SolidColorBrush(Color.FromArgb(150, 255, 80, 80)),
                    BorderThickness = new Thickness(0),
                    FontSize        = 13,
                    Cursor          = Cursors.Hand,
                    Padding         = new Thickness(4, 0, 0, 0)
                };
                del.Click += (s, e) => { _activeFolder!.Apps.RemoveAt(idx); RefreshFolderAppList(); };
                DockPanel.SetDock(del, Dock.Right);

                // Advanced toggle button
                var advBtn = new Button
                {
                    Content         = "⋯",
                    Background      = Brushes.Transparent,
                    Foreground      = fgDim,
                    BorderThickness = new Thickness(0),
                    FontSize        = 13,
                    Cursor          = Cursors.Hand,
                    Padding         = new Thickness(4, 0, 8, 0),
                    ToolTip         = "Advanced options"
                };
                DockPanel.SetDock(advBtn, Dock.Right);

                var info = new StackPanel();
                info.Children.Add(new TextBlock
                {
                    Text         = string.IsNullOrEmpty(app.SubFolderId) ? app.Name : $"📁 {app.Name}",
                    Foreground   = fg,
                    FontSize     = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                info.Children.Add(new TextBlock
                {
                    Text       = string.IsNullOrEmpty(app.SubFolderId)
                                     ? Path.GetFileName(app.ExecutablePath)
                                     : "Nested folder",
                    Foreground = fgDim,
                    FontSize   = 10,
                    Margin     = new Thickness(0, 1, 0, 0)
                });

                dp.Children.Add(del);

                // Only show advanced button for real apps (not sub-folders)
                if (string.IsNullOrEmpty(app.SubFolderId))
                    dp.Children.Add(advBtn);

                dp.Children.Add(info);

                // Advanced panel (hidden by default) — contains Args field
                var advPanel = new Border
                {
                    Visibility      = Visibility.Collapsed,
                    Background      = new SolidColorBrush(Color.FromArgb(18,
                                          cc.R, cc.G, cc.B)),
                    CornerRadius    = new CornerRadius(4),
                    Padding         = new Thickness(8, 6, 8, 6),
                    Margin          = new Thickness(0, 6, 0, 0)
                };

                var argsRow = new DockPanel();
                argsRow.Children.Add(new TextBlock
                {
                    Text              = "Launch arguments:",
                    Foreground        = fgDim,
                    FontSize          = 10,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(0, 0, 8, 0)
                });
                var argsBox = new TextBox
                {
                    Text            = app.Arguments ?? "",
                    Background      = new SolidColorBrush(Color.FromArgb(35,
                                          cc.R, cc.G, cc.B)),
                    Foreground      = fg,
                    BorderThickness = new Thickness(1),
                    BorderBrush     = new SolidColorBrush(Color.FromArgb(45,
                                          cc.R, cc.G, cc.B)),
                    Padding         = new Thickness(4, 2, 4, 2),
                    FontSize        = 11,
                    CaretBrush      = fg,
                    FontFamily      = new System.Windows.Media.FontFamily("Consolas"),
                    Tag             = app,
                    ToolTip         = "Command-line arguments passed to the app when launched.\nExample: --profile Work   or   -fullscreen"
                };
                argsBox.TextChanged += (s, e) =>
                {
                    if (argsBox.Tag is AppEntry a) a.Arguments = argsBox.Text;
                };

                var hintTb = new TextBlock
                {
                    Text       = "Arguments are passed to the app on launch (e.g. --profile Work)",
                    Foreground = fgDim,
                    FontSize   = 10,
                    Margin     = new Thickness(0, 4, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };

                DockPanel.SetDock(argsBox, Dock.Right);
                argsRow.Children.Add(argsBox);
                var advContent = new StackPanel();
                advContent.Children.Add(argsRow);
                advContent.Children.Add(hintTb);
                advPanel.Child = advContent;

                // Toggle advanced panel on ⋯ click
                advBtn.Click += (s, e) =>
                {
                    advPanel.Visibility = advPanel.Visibility == Visibility.Collapsed
                        ? Visibility.Visible : Visibility.Collapsed;
                    advBtn.Foreground = advPanel.Visibility == Visibility.Visible
                        ? new SolidColorBrush(Color.FromArgb(255, 91, 140, 255)) : fgDim;
                };

                outer.Children.Add(dp);
                outer.Children.Add(advPanel);
                row.Child = outer;
                FolderAppsPanel.Children.Add(row);
            }
        }

        private void FolderName_Changed(object sender, TextChangedEventArgs e)
        {
            if (_loading || _activeFolder == null) return;
            _activeFolder.Name = FolderNameBox.Text;
        }

        private void FolderColorSwatch_Click(object sender, MouseButtonEventArgs e)
        {
            if (_activeFolder == null) return;
            var dlg = new FolderEditorWindow(_activeFolder);
            dlg.Title = $"Edit Color — {_activeFolder.Name}";
            // Set Owner so the editor appears above settings, not behind it.
            // ThemeSettingsWindow is a normal (non-transparent) window so Owner is safe.
            dlg.Owner = this;
            dlg.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            if (dlg.ShowDialog() == true)
            {
                _activeFolder = App.DataStore.Folders
                    .FirstOrDefault(f => f.Id == _activeFolder.Id) ?? _activeFolder;
                UpdateFolderColorSwatch();
                App.NotifyThemeChanged();
            }
        }

        private void FolderAddApp_Click(object sender, RoutedEventArgs e)
        {
            if (_activeFolder == null) return;
            var dlg = new OpenFileDialog
            {
                Title           = "Select Application or Shortcut",
                Filter          = "All Files (*.*)|*.*|Applications (*.exe;*.lnk;*.bat;*.url)|*.exe;*.lnk;*.bat;*.url",
                CheckFileExists = true
            };
            if (dlg.ShowDialog() != true) return;
            string  path     = dlg.FileName;
            string? storable = Helpers.ShortcutHelper.ResolveToStorable(path) ?? path;
            string  appName  = Path.GetFileNameWithoutExtension(path);
            var     nameDlg  = new AppNameDialog(appName);
            if (nameDlg.ShowDialog() == true) appName = nameDlg.AppName;
            _activeFolder.Apps.Add(new AppEntry { Name = appName, ExecutablePath = storable });
            RefreshFolderAppList();
        }

        private void FolderBulkAdd_Click(object sender, RoutedEventArgs e)
        {
            if (_activeFolder == null) return;
            var dlg = new BulkAddWindow(_activeFolder);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                App.DataStore.Save();
                App.NotifyThemeChanged();
                LoadFolderTab();
            }
        }


        private void FolderSave_Click(object sender, RoutedEventArgs e)
        {
            if (_activeFolder == null) return;
            if (string.IsNullOrWhiteSpace(FolderNameBox.Text))
            {
                MessageBox.Show("Please enter a folder name.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _activeFolder.Name = FolderNameBox.Text.Trim();
            App.DataStore.Save();
            App.NotifyThemeChanged();
            var orig = FolderSaveBtn.Background;
            FolderSaveBtn.Content    = "✓ Saved!";
            FolderSaveBtn.Background = new SolidColorBrush(Color.FromRgb(50, 180, 90));
            var timer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromSeconds(1.5) };
            timer.Tick += (s2, e2) =>
            {
                timer.Stop();
                FolderSaveBtn.Content    = "✓ Save Changes";
                FolderSaveBtn.Background = orig;
            };
            timer.Start();
        }

        // ── Animation preview ──────────────────────────────────────────────

        private void PreviewAnim_Click(object sender, RoutedEventArgs e)
        {
            var target = PrevPopup;
            switch (App.DataStore.Theme.PopupAnimation)
            {
                case PopupAnimation.Expand:
                    var st = new ScaleTransform(0.5, 0.5,
                        target.ActualWidth / 2, target.ActualHeight / 2);
                    target.RenderTransform = st; target.Opacity = 0;
                    var sx = new DoubleAnimationUsingKeyFrames();
                    sx.KeyFrames.Add(new EasingDoubleKeyFrame(0.5, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                    sx.KeyFrames.Add(new EasingDoubleKeyFrame(1.08,
                        KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220)))
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                    sx.KeyFrames.Add(new EasingDoubleKeyFrame(1.0,
                        KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))));
                    st.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, sx);
                    target.BeginAnimation(OpacityProperty,
                        new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
                    break;
                case PopupAnimation.Slide:
                    var tr = new TranslateTransform(0, 20);
                    target.RenderTransform = tr; target.Opacity = 0;
                    var slide = new DoubleAnimationUsingKeyFrames();
                    slide.KeyFrames.Add(new EasingDoubleKeyFrame(20, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                    slide.KeyFrames.Add(new EasingDoubleKeyFrame(0,
                        KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280)))
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                    tr.BeginAnimation(TranslateTransform.YProperty, slide);
                    target.BeginAnimation(OpacityProperty,
                        new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
                    break;
                case PopupAnimation.Fade:
                    target.Opacity = 0;
                    target.BeginAnimation(OpacityProperty,
                        new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350)));
                    break;
                default:
                    target.Opacity = 0;
                    target.BeginAnimation(OpacityProperty,
                        new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(80)));
                    break;
            }
        }

        // ── Load settings ──────────────────────────────────────────────────

        private void LoadCurrentSettings()
        {
            var t = App.DataStore.Theme;
            ThemeDark.IsChecked       = t.Theme == AppTheme.Dark;
            ThemeLight.IsChecked      = t.Theme == AppTheme.Light;
            ThemeMidnight.IsChecked   = t.Theme == AppTheme.Midnight;
            ThemeSunset.IsChecked     = t.Theme == AppTheme.Sunset;
            ThemeAccent.IsChecked     = t.Theme == AppTheme.Accent;
            FontSlider.Value          = t.FontSize;
            OpacitySlider.Value       = t.PopupOpacity;
            SizeSmall.IsChecked       = t.IconSize == IconSize.Small;
            SizeNormal.IsChecked      = t.IconSize == IconSize.Normal;
            SizeLarge.IsChecked       = t.IconSize == IconSize.Large;
            IconBgSquircle.IsChecked  = t.IconBgStyle == IconBgStyle.Squircle;
            IconBgCircle.IsChecked    = t.IconBgStyle == IconBgStyle.Circle;
            IconBgSquare.IsChecked    = t.IconBgStyle == IconBgStyle.Square;
            IconBgNone.IsChecked      = t.IconBgStyle == IconBgStyle.None;
            AnimNone.IsChecked        = t.PopupAnimation == PopupAnimation.None;
            AnimExpand.IsChecked      = t.PopupAnimation == PopupAnimation.Expand;
            AnimSlide.IsChecked       = t.PopupAnimation == PopupAnimation.Slide;
            AnimFade.IsChecked        = t.PopupAnimation == PopupAnimation.Fade;
            ShowLabelsCheck.IsChecked   = t.ShowAppLabels;
            RoundedIconsCheck.IsChecked = t.RoundedIcons;
            HideArrowsCheck.IsChecked   = t.HideShortcutArrows;
            ColumnsSlider.Value         = t.PopupColumns;
            ShapeRounded.IsChecked      = t.WidgetShape == WidgetShape.RoundedSquare;
            ShapeCircle.IsChecked       = t.WidgetShape == WidgetShape.Circle;
            ShapeSharp.IsChecked        = t.WidgetShape == WidgetShape.Sharp;
            StyleGradient.IsChecked     = t.WidgetStyle == WidgetStyle.Gradient;
            StyleSolid.IsChecked        = t.WidgetStyle == WidgetStyle.Solid;
            StyleGlass.IsChecked        = t.WidgetStyle == WidgetStyle.Glass;
            StyleOutline.IsChecked      = t.WidgetStyle == WidgetStyle.Outline;
            LabelShadow.IsChecked       = t.LabelStyle == LabelStyle.Shadow;
            LabelPill.IsChecked         = t.LabelStyle == LabelStyle.Pill;
            LabelHidden.IsChecked       = t.LabelStyle == LabelStyle.Hidden;
            WidgetOpacitySlider.Value   = t.WidgetOpacity;
            WidgetShadowCheck.IsChecked = t.WidgetShadow;
            ShowMiniIconsCheck.IsChecked = t.ShowMiniIcons;
            UpdateAllLabels();
        }

        private void UpdateAllLabels()
        {
            OpacityLabel.Text       = $"Popup background opacity  —  {OpacitySlider.Value:P0}";
            FontSizeLabel.Text      = $"Font size  —  {(int)FontSlider.Value}pt";
            WidgetOpacityLabel.Text = $"Widget opacity  —  {WidgetOpacitySlider.Value:P0}";
            ColumnsLabel.Text       = $"Max columns per row  —  {(int)ColumnsSlider.Value}";
        }

        // ── Event handlers ────────────────────────────────────────────────

        private void Theme_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if      (ThemeDark.IsChecked == true)     App.DataStore.Theme.Theme = AppTheme.Dark;
            else if (ThemeLight.IsChecked == true)    App.DataStore.Theme.Theme = AppTheme.Light;
            else if (ThemeMidnight.IsChecked == true) App.DataStore.Theme.Theme = AppTheme.Midnight;
            else if (ThemeSunset.IsChecked == true)   App.DataStore.Theme.Theme = AppTheme.Sunset;
            else if (ThemeAccent.IsChecked == true)   App.DataStore.Theme.Theme = AppTheme.Accent;
            Save(); ApplyWindowTheme(); ShowSection("Theme");
        }
        private void Size_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if      (SizeSmall.IsChecked == true)  App.DataStore.Theme.IconSize = IconSize.Small;
            else if (SizeNormal.IsChecked == true) App.DataStore.Theme.IconSize = IconSize.Normal;
            else if (SizeLarge.IsChecked == true)  App.DataStore.Theme.IconSize = IconSize.Large;
            Save(); UpdateLivePreview();
        }
        private void IconBg_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if      (IconBgSquircle.IsChecked == true) App.DataStore.Theme.IconBgStyle = IconBgStyle.Squircle;
            else if (IconBgCircle.IsChecked == true)   App.DataStore.Theme.IconBgStyle = IconBgStyle.Circle;
            else if (IconBgSquare.IsChecked == true)   App.DataStore.Theme.IconBgStyle = IconBgStyle.Square;
            else if (IconBgNone.IsChecked == true)     App.DataStore.Theme.IconBgStyle = IconBgStyle.None;
            Save(); UpdateLivePreview();
        }
        private void Anim_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if      (AnimNone.IsChecked == true)   App.DataStore.Theme.PopupAnimation = PopupAnimation.None;
            else if (AnimExpand.IsChecked == true) App.DataStore.Theme.PopupAnimation = PopupAnimation.Expand;
            else if (AnimSlide.IsChecked == true)  App.DataStore.Theme.PopupAnimation = PopupAnimation.Slide;
            else if (AnimFade.IsChecked == true)   App.DataStore.Theme.PopupAnimation = PopupAnimation.Fade;
            Save();
        }
        private void Shape_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if      (ShapeRounded.IsChecked == true) App.DataStore.Theme.WidgetShape = WidgetShape.RoundedSquare;
            else if (ShapeCircle.IsChecked == true)  App.DataStore.Theme.WidgetShape = WidgetShape.Circle;
            else if (ShapeSharp.IsChecked == true)   App.DataStore.Theme.WidgetShape = WidgetShape.Sharp;
            Save(); UpdateLivePreview();
        }
        private void Style_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if      (StyleGradient.IsChecked == true) App.DataStore.Theme.WidgetStyle = WidgetStyle.Gradient;
            else if (StyleSolid.IsChecked == true)    App.DataStore.Theme.WidgetStyle = WidgetStyle.Solid;
            else if (StyleGlass.IsChecked == true)    App.DataStore.Theme.WidgetStyle = WidgetStyle.Glass;
            else if (StyleOutline.IsChecked == true)  App.DataStore.Theme.WidgetStyle = WidgetStyle.Outline;
            Save(); UpdateLivePreview();
        }
        private void LabelStyle_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if      (LabelShadow.IsChecked == true) App.DataStore.Theme.LabelStyle = LabelStyle.Shadow;
            else if (LabelPill.IsChecked == true)   App.DataStore.Theme.LabelStyle = LabelStyle.Pill;
            else if (LabelHidden.IsChecked == true) App.DataStore.Theme.LabelStyle = LabelStyle.Hidden;
            Save(); UpdateLivePreview();
        }
        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            App.DataStore.Theme.PopupOpacity = OpacitySlider.Value;
            Save(); UpdateAllLabels(); UpdateLivePreview();
        }
        private void FontSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            App.DataStore.Theme.FontSize = (int)FontSlider.Value;
            Save(); UpdateAllLabels(); UpdateLivePreview();
        }
        private void WidgetOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            App.DataStore.Theme.WidgetOpacity = WidgetOpacitySlider.Value;
            Save(); UpdateAllLabels(); UpdateLivePreview();
        }
        private void Columns_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            App.DataStore.Theme.PopupColumns = (int)ColumnsSlider.Value;
            Save(); UpdateAllLabels(); UpdateLivePreview();
        }
        private void Toggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            App.DataStore.Theme.ShowAppLabels      = ShowLabelsCheck.IsChecked == true;
            App.DataStore.Theme.RoundedIcons       = RoundedIconsCheck.IsChecked == true;
            App.DataStore.Theme.WidgetShadow       = WidgetShadowCheck.IsChecked == true;
            App.DataStore.Theme.ShowMiniIcons      = ShowMiniIconsCheck.IsChecked == true;
            App.DataStore.Theme.HideShortcutArrows = HideArrowsCheck.IsChecked == true;
            Save(); UpdateLivePreview();
        }


        private void Save()
        {
            App.DataStore.Save();
            App.NotifyThemeChanged();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        // ══ Backup & Restore ══════════════════════════════════════════════

        private void LoadBackupSection()
        {
            // Apply theme colours to the two cards
            var c  = ThemeManager.GetColors();
            var cc = GetContrastForeground(c);

            var cardBg = Color.FromArgb(
                30,
                c.AccentColor.R,
                c.AccentColor.G,
                c.AccentColor.B);

            ExportCard.Background = new SolidColorBrush(
                Color.FromArgb(25, c.PopupBorder.R, c.PopupBorder.G, c.PopupBorder.B));
            ExportCard.BorderBrush = new SolidColorBrush(
                Color.FromArgb(35, c.PopupBorder.R, c.PopupBorder.G, c.PopupBorder.B));
            ExportCard.BorderThickness = new Thickness(1);

            ImportCard.Background = new SolidColorBrush(
                Color.FromArgb(25, c.PopupBorder.R, c.PopupBorder.G, c.PopupBorder.B));
            ImportCard.BorderBrush = new SolidColorBrush(
                Color.FromArgb(35, c.PopupBorder.R, c.PopupBorder.G, c.PopupBorder.B));
            ImportCard.BorderThickness = new Thickness(1);

            var fg     = new SolidColorBrush(cc);
            var fgDim  = new SolidColorBrush(Color.FromArgb(160, cc.R, cc.G, cc.B));

            BackupTitle.Foreground    = fg;
            ExportCardTitle.Foreground = fg;
            ImportCardTitle.Foreground = fg;
            ExportCardSub.Foreground  = fgDim;
            ImportCardSub.Foreground  = fgDim;

            BackupStatus.Visibility = Visibility.Collapsed;
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title            = "Save Desktop Folders Backup",
                Filter           = "Desktop Folders Backup (*.dfbackup)|*.dfbackup",
                DefaultExt       = ".dfbackup",
                FileName         = $"DesktopFolders_{DateTime.Now:yyyy-MM-dd}"
            };

            if (dlg.ShowDialog() != true) return;

            BtnExport.IsEnabled = false;
            BtnExport.Content   = "Exporting…";

            string? err = App.DataStore.ExportBackup(dlg.FileName);

            BtnExport.IsEnabled = true;
            BtnExport.Content   = "Choose Location & Export…";

            if (err == null)
            {
                ShowBackupStatus(
                    $"✓  Backup saved to {System.IO.Path.GetFileName(dlg.FileName)}",
                    success: true);
            }
            else
            {
                ShowBackupStatus($"✗  Export failed: {err}", success: false);
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Open Desktop Folders Backup",
                Filter = "Desktop Folders Backup (*.dfbackup)|*.dfbackup|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            // Confirm before overwriting
            var result = System.Windows.MessageBox.Show(
                "This will replace all current folders, widget positions, and theme settings.\n\n" +
                "Are you sure you want to restore from this backup?",
                "Restore Backup",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            BtnImport.IsEnabled = false;
            BtnImport.Content   = "Restoring…";

            string? err = App.DataStore.ImportBackup(dlg.FileName);

            BtnImport.IsEnabled = true;
            BtnImport.Content   = "Choose Backup File & Restore…";

            if (err == null)
            {
                ShowBackupStatus(
                    "✓  Backup restored. Restarting widgets…",
                    success: true);

                // Refresh all widgets with the newly loaded data
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (Application.Current is App app)
                    {
                        app.RemoveAllWidgets();
                        app.ShowAllWidgets();
                    }
                    App.NotifyThemeChanged();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            else
            {
                ShowBackupStatus($"✗  Restore failed: {err}", success: false);
            }
        }

        private void ShowBackupStatus(string message, bool success)
        {
            BackupStatus.Text       = message;
            BackupStatus.Foreground = new SolidColorBrush(
                success ? Color.FromRgb(80, 200, 100) : Color.FromRgb(220, 80, 80));
            BackupStatus.Visibility = Visibility.Visible;
        }


    }
}
