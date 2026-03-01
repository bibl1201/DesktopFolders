using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopFolders.Helpers;
using DesktopFolders.Models;
using Microsoft.Win32;

namespace DesktopFolders.Views
{
    public partial class FolderEditorWindow : Window
    {
        public AppFolder? ResultFolder { get; private set; }
        private AppFolder _working;

        private double _hue        = 220;
        private double _saturation = 0.70;
        private double _brightness = 1.0;

        private string _selectedColor = "#5B8CFF";
        private bool   _updatingColor = false;
        private bool   _wheelDragging = false;
        private bool   _brightDragging = false;

        private const int WheelSize = 180;

        private static readonly string[] PresetColors = new[]
        {
            "#5B8CFF","#4169E1","#C45BFF","#FF5B5B","#FF6B35",
            "#FFD45B","#5BFF8C","#4ECDC4","#FF5BBA","#FFFFFF",
            "#808080","#1E1E2E"
        };

        public FolderEditorWindow(AppFolder? existing)
        {
            InitializeComponent();

            if (existing != null)
            {
                _working = new AppFolder
                {
                    Id        = existing.Id,
                    Name      = existing.Name,
                    Color     = existing.Color,
                    PositionX = existing.PositionX,
                    PositionY = existing.PositionY,
                    Apps      = existing.Apps.Select(a => new AppEntry
                    {
                        Name           = a.Name,
                        ExecutablePath = a.ExecutablePath,
                        Arguments      = a.Arguments,
                        IconPath       = a.IconPath,
                        SubFolderId    = a.SubFolderId
                    }).ToList()
                };
            }
            else { _working = new AppFolder(); }

            _selectedColor = _working.Color;
            NameBox.Text   = _working.Name;

            Loaded += (s, e) =>
            {
                DrawWheel();
                BuildPresets();
                SetColorFromHex(_selectedColor);
                RefreshAppList();
            };
        }

        // ── Color wheel rendering ──────────────────────────────────────────

        private void DrawWheel()
        {
            int size   = WheelSize;
            double r   = size / 2.0;
            double cx  = r, cy = r;

            var bmp    = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
            var pixels = new byte[size * size * 4];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double dx   = x - cx;
                    double dy   = y - cy;
                    double dist = Math.Sqrt(dx * dx + dy * dy);

                    int idx = (y * size + x) * 4;
                    if (dist > r)
                    {
                        pixels[idx] = pixels[idx+1] = pixels[idx+2] = pixels[idx+3] = 0;
                    }
                    else
                    {
                        double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                        if (angle < 0) angle += 360;
                        var c = HsbToRgb(angle, dist / r, _brightness);
                        pixels[idx]     = c.B;
                        pixels[idx + 1] = c.G;
                        pixels[idx + 2] = c.R;
                        pixels[idx + 3] = 255;
                    }
                }
            }

            bmp.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);
            WheelImage.Source = bmp;
        }

        private void DrawBrightnessBar()
        {
            var topColor = HsbToRgb(_hue, _saturation, 1.0);
            BrightnessBar.Fill = new LinearGradientBrush(
                Color.FromRgb(topColor.R, topColor.G, topColor.B),
                Colors.Black,
                new Point(0, 0), new Point(0, 1));
        }

        // ── Wheel mouse ────────────────────────────────────────────────────

        private void Wheel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _wheelDragging = true;
            WheelCanvas.CaptureMouse();
            PickFromWheel(e.GetPosition(WheelCanvas));
        }
        private void Wheel_MouseMove(object sender, MouseEventArgs e)
        {
            if (_wheelDragging) PickFromWheel(e.GetPosition(WheelCanvas));
        }
        private void Wheel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _wheelDragging = false;
            WheelCanvas.ReleaseMouseCapture();
        }

        private void PickFromWheel(Point p)
        {
            double cx = WheelSize / 2.0, cy = WheelSize / 2.0;
            double dx = p.X - cx, dy = p.Y - cy;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double r    = WheelSize / 2.0;
            if (dist > r) { dx = dx / dist * r; dy = dy / dist * r; dist = r; }

            _hue        = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            if (_hue < 0) _hue += 360;
            _saturation = dist / r;

            System.Windows.Controls.Canvas.SetLeft(WheelCursor, cx + dx - 7);
            System.Windows.Controls.Canvas.SetTop(WheelCursor,  cy + dy - 7);
            CommitHSB();
            DrawBrightnessBar();
        }

        // ── Brightness mouse ───────────────────────────────────────────────

        private void Brightness_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _brightDragging = true;
            BrightnessCanvas.CaptureMouse();
            PickBrightness(e.GetPosition(BrightnessCanvas).Y);
        }
        private void Brightness_MouseMove(object sender, MouseEventArgs e)
        {
            if (_brightDragging) PickBrightness(e.GetPosition(BrightnessCanvas).Y);
        }
        private void Brightness_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _brightDragging = false;
            BrightnessCanvas.ReleaseMouseCapture();
        }

        private void PickBrightness(double y)
        {
            y = Math.Max(0, Math.Min(WheelSize, y));
            _brightness = 1.0 - (y / WheelSize);
            System.Windows.Controls.Canvas.SetTop(BrightnessHandle, y - 2.5);
            CommitHSB();
            DrawWheel();
        }

        // ── Commit HSB ────────────────────────────────────────────────────

        private void CommitHSB()
        {
            var rgb = HsbToRgb(_hue, _saturation, _brightness);
            string hex = $"#{rgb.R:X2}{rgb.G:X2}{rgb.B:X2}";
            _selectedColor = hex;
            ApplyColorToUI(rgb, hex);
        }

        private void ApplyColorToUI(Color c, string hex)
        {
            _updatingColor = true;
            PreviewSwatch.Background = new SolidColorBrush(c);
            HexBox.Text  = hex.TrimStart('#').ToUpperInvariant();
            SliderR.Value = c.R; LabelR.Text = c.R.ToString();
            SliderG.Value = c.G; LabelG.Text = c.G.ToString();
            SliderB.Value = c.B; LabelB.Text = c.B.ToString();
            _updatingColor = false;
        }

        private void HexBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingColor) return;
            string input = HexBox.Text.Trim();
            if (input.Length == 6) try { SetColorFromHex("#" + input); } catch { }
        }

        private void RGB_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingColor) return;
            var c = Color.FromRgb((byte)SliderR.Value, (byte)SliderG.Value, (byte)SliderB.Value);
            RgbToHsb(c, out _hue, out _saturation, out _brightness);
            string hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            _selectedColor = hex;
            ApplyColorToUI(c, hex);
            UpdateWheelCursor(); DrawWheel(); DrawBrightnessBar(); UpdateBrightnessHandle();
        }

        private void SetColorFromHex(string hex)
        {
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(hex);
                RgbToHsb(c, out _hue, out _saturation, out _brightness);
                _selectedColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                ApplyColorToUI(c, _selectedColor);
                UpdateWheelCursor(); DrawWheel(); DrawBrightnessBar(); UpdateBrightnessHandle();
            }
            catch { }
        }

        private void UpdateWheelCursor()
        {
            double r   = WheelSize / 2.0;
            double rad = _hue * Math.PI / 180.0;
            double dx  = Math.Cos(rad) * _saturation * r;
            double dy  = Math.Sin(rad) * _saturation * r;
            System.Windows.Controls.Canvas.SetLeft(WheelCursor, r + dx - 7);
            System.Windows.Controls.Canvas.SetTop(WheelCursor,  r + dy - 7);
        }

        private void UpdateBrightnessHandle()
        {
            System.Windows.Controls.Canvas.SetTop(BrightnessHandle,
                (1.0 - _brightness) * WheelSize - 2.5);
        }

        private void BuildPresets()
        {
            PresetPanel.Children.Clear();
            foreach (var hex in PresetColors)
            {
                var capturedHex = hex;
                var swatch = new Border
                {
                    Width = 22, Height = 22, CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(0, 0, 5, 5), Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(
                        string.Equals(hex, _selectedColor, StringComparison.OrdinalIgnoreCase) ? 2 : 0),
                    BorderBrush = Brushes.White, ToolTip = hex
                };
                try { swatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
                catch { swatch.Background = Brushes.Gray; }
                swatch.MouseLeftButtonUp += (s, e) => { SetColorFromHex(capturedHex); BuildPresets(); };
                PresetPanel.Children.Add(swatch);
            }
        }

        // ── HSB ↔ RGB ──────────────────────────────────────────────────────

        private static Color HsbToRgb(double h, double s, double b)
        {
            h = h % 360;
            if (s == 0) { byte v = (byte)(b * 255); return Color.FromRgb(v, v, v); }
            double sector = h / 60.0; int i = (int)Math.Floor(sector); double f = sector - i;
            double p = b*(1-s), q = b*(1-s*f), t = b*(1-s*(1-f));
            double r, g, bl;
            switch (i)
            {
                case 0: r=b; g=t; bl=p; break; case 1: r=q; g=b; bl=p; break;
                case 2: r=p; g=b; bl=t; break; case 3: r=p; g=q; bl=b; break;
                case 4: r=t; g=p; bl=b; break; default: r=b; g=p; bl=q; break;
            }
            return Color.FromRgb((byte)(r*255),(byte)(g*255),(byte)(bl*255));
        }

        private static void RgbToHsb(Color c, out double h, out double s, out double b)
        {
            double r=c.R/255.0, g=c.G/255.0, bl=c.B/255.0;
            double max=Math.Max(r,Math.Max(g,bl)), min=Math.Min(r,Math.Min(g,bl));
            double delta=max-min;
            b=max; s=max==0?0:delta/max;
            if (delta==0){h=0;return;}
            if (max==r)      h=60*(((g-bl)/delta)%6);
            else if (max==g) h=60*(((bl-r)/delta)+2);
            else             h=60*(((r-g)/delta)+4);
            if (h<0) h+=360;
        }

        // ── App list ───────────────────────────────────────────────────────

        private void RefreshAppList()
        {
            AppListPanel.Children.Clear();
            for (int i = 0; i < _working.Apps.Count; i++)
            {
                var app = _working.Apps[i];
                var idx = i;

                var row = new Border
                {
                    Background   = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                    CornerRadius = new CornerRadius(8),
                    Padding      = new Thickness(10, 8, 10, 8),
                    Margin       = new Thickness(0, 0, 0, 6)
                };

                var outerPanel = new StackPanel();

                // ── Row 1: name + icon badge + remove button ───────────────
                var topRow = new DockPanel();

                var removeBtn = new Button
                {
                    Content = "✕", Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 80, 80)),
                    BorderThickness = new Thickness(0), FontSize = 14,
                    Cursor = Cursors.Hand, Padding = new Thickness(4, 0, 0, 0)
                };
                removeBtn.Click += (s, e) => { _working.Apps.RemoveAt(idx); RefreshAppList(); };
                DockPanel.SetDock(removeBtn, Dock.Right);
                topRow.Children.Add(removeBtn);

                // Custom icon indicator
                if (!string.IsNullOrEmpty(app.IconPath))
                {
                    var iconBadge = new Border
                    {
                        Width = 16, Height = 16, CornerRadius = new CornerRadius(3),
                        Background = new SolidColorBrush(Color.FromArgb(80, 91, 140, 255)),
                        Margin = new Thickness(0, 0, 6, 0),
                        ToolTip = app.IconPath,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var starText = new TextBlock
                    {
                        Text = "🖼", FontSize = 10,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment   = VerticalAlignment.Center
                    };
                    iconBadge.Child = starText;
                    DockPanel.SetDock(iconBadge, Dock.Right);
                    topRow.Children.Add(iconBadge);
                }

                var nameLabel = new TextBlock
                {
                    Text      = string.IsNullOrEmpty(app.SubFolderId)
                                ? app.Name
                                : $"📁 {app.Name}",
                    Foreground = Brushes.White, FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                topRow.Children.Add(nameLabel);
                outerPanel.Children.Add(topRow);

                // ── Row 2: path label ──────────────────────────────────────
                string pathHint = string.IsNullOrEmpty(app.SubFolderId)
                    ? Path.GetFileName(app.ExecutablePath)
                    : "Nested folder";

                outerPanel.Children.Add(new TextBlock
                {
                    Text      = pathHint,
                    Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                    FontSize  = 10,
                    Margin    = new Thickness(0, 2, 0, 0)
                });

                // ── Row 3: Arguments field (only for real apps, not sub-folders) ───
                if (string.IsNullOrEmpty(app.SubFolderId))
                {
                    var argsPanel = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
                    var argsLabel = new TextBlock
                    {
                        Text = "Args:", Foreground = new SolidColorBrush(Color.FromArgb(150,255,255,255)),
                        FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0,0,6,0)
                    };
                    DockPanel.SetDock(argsLabel, Dock.Left);

                    var argsBox = new TextBox
                    {
                        Text            = app.Arguments ?? "",
                        Background      = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                        Foreground      = Brushes.White,
                        BorderBrush     = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                        BorderThickness = new Thickness(1),
                        Padding         = new Thickness(6, 3, 6, 3),
                        FontSize        = 11,
                        CaretBrush      = Brushes.White,
                        FontFamily      = new FontFamily("Consolas"),
                        Tag             = app  // so we can update on change
                    };
                    argsBox.TextChanged += (s, e) =>
                    {
                        if (argsBox.Tag is AppEntry a) a.Arguments = argsBox.Text;
                    };

                    argsPanel.Children.Add(argsLabel);
                    argsPanel.Children.Add(argsBox);
                    outerPanel.Children.Add(argsPanel);

                    // ── Row 4: custom icon picker ──────────────────────────
                    var iconRow = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };

                    var iconPathLabel = new TextBlock
                    {
                        Text = string.IsNullOrEmpty(app.IconPath) ? "No custom icon" : Path.GetFileName(app.IconPath),
                        Foreground = new SolidColorBrush(Color.FromArgb(120,255,255,255)),
                        FontSize = 10, VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };

                    var pickIconBtn = new Button
                    {
                        Content = "🖼 Icon", FontSize = 10,
                        Background = new SolidColorBrush(Color.FromArgb(60, 91, 140, 255)),
                        Foreground = Brushes.White, BorderThickness = new Thickness(0),
                        Padding = new Thickness(8, 3, 8, 3), Cursor = Cursors.Hand,
                        Margin = new Thickness(0, 0, 6, 0)
                    };
                    var clearIconBtn = new Button
                    {
                        Content = "✕", FontSize = 10,
                        Background = new SolidColorBrush(Color.FromArgb(60, 200, 60, 60)),
                        Foreground = Brushes.White, BorderThickness = new Thickness(0),
                        Padding = new Thickness(6, 3, 6, 3), Cursor = Cursors.Hand,
                        Visibility = string.IsNullOrEmpty(app.IconPath)
                            ? Visibility.Collapsed : Visibility.Visible
                    };

                    pickIconBtn.Click += (s, e) =>
                    {
                        var dlg = new OpenFileDialog
                        {
                            Title  = "Select Icon",
                            Filter = "Icon files (*.ico;*.png;*.jpg;*.bmp)|*.ico;*.png;*.jpg;*.bmp"
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            app.IconPath       = dlg.FileName;
                            iconPathLabel.Text = Path.GetFileName(dlg.FileName);
                            clearIconBtn.Visibility = Visibility.Visible;
                        }
                    };

                    clearIconBtn.Click += (s, e) =>
                    {
                        app.IconPath            = null;
                        iconPathLabel.Text      = "No custom icon";
                        clearIconBtn.Visibility = Visibility.Collapsed;
                    };

                    DockPanel.SetDock(pickIconBtn,   Dock.Left);
                    DockPanel.SetDock(clearIconBtn,  Dock.Left);
                    iconRow.Children.Add(pickIconBtn);
                    iconRow.Children.Add(clearIconBtn);
                    iconRow.Children.Add(iconPathLabel);
                    outerPanel.Children.Add(iconRow);
                }

                row.Child = outerPanel;
                AppListPanel.Children.Add(row);
            }
        }

        private void AddApp_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title           = "Select Application or Shortcut",
                Filter          = "Launchable files (*.exe;*.lnk;*.bat;*.url)|*.exe;*.lnk;*.bat;*.url|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() != true) return;

            string path     = dialog.FileName;
            string? storable = ShortcutHelper.ResolveToStorable(path);
            if (storable == null) storable = path;

            string appName = Path.GetFileNameWithoutExtension(path);
            var nameDialog = new AppNameDialog(appName);
            if (nameDialog.ShowDialog() == true) appName = nameDialog.AppName;

            _working.Apps.Add(new AppEntry { Name = appName, ExecutablePath = storable });
            RefreshAppList();
        }

        /// <summary>Adds another existing folder as a nested entry.</summary>
        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var otherFolders = App.DataStore.Folders
                .Where(f => f.Id != _working.Id)
                .ToList();

            if (otherFolders.Count == 0)
            {
                MessageBox.Show("No other folders exist yet. Create another folder first.",
                    "No Folders", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var picker = new FolderPickerDialog(otherFolders, _working);
            if (picker.ShowDialog() != true || picker.SelectedFolder == null) return;

            var target = picker.SelectedFolder;

            // Prevent duplicate
            if (_working.Apps.Exists(a => a.SubFolderId == target.Id))
            {
                MessageBox.Show($"\"{target.Name}\" is already in this folder.",
                    "Duplicate", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _working.Apps.Add(new AppEntry
            {
                Name          = target.Name,
                ExecutablePath = "",         // unused for sub-folders
                SubFolderId   = target.Id
            });
            RefreshAppList();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("Please enter a folder name.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                NameBox.Focus();
                return;
            }

            _working.Name  = NameBox.Text.Trim();
            _working.Color = _selectedColor;

            var existing = App.DataStore.Folders.FirstOrDefault(f => f.Id == _working.Id);
            if (existing != null)
            {
                existing.Name  = _working.Name;
                existing.Color = _working.Color;
                existing.Apps  = _working.Apps;
            }

            ResultFolder = _working;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    // ── Folder picker dialog ───────────────────────────────────────────────

    public class FolderPickerDialog : Window
    {
        public AppFolder? SelectedFolder { get; private set; }
        private ListBox _list;

        public FolderPickerDialog(List<AppFolder> folders, AppFolder parentFolder)
        {
            Title  = "Add Nested Folder";
            Width  = 320; Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background   = new SolidColorBrush(Color.FromRgb(30, 30, 46));
            ResizeMode   = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(18) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.Children.Add(new TextBlock
            {
                Text = "Choose a folder to nest inside this one:",
                Foreground = Brushes.White, FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10)
            });

            _list = new ListBox
            {
                Background      = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                Foreground      = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                FontSize        = 13,
                Margin          = new Thickness(0, 0, 0, 12),
                ItemContainerStyle = MakeListItemStyle()
            };

            foreach (var f in folders)
            {
                var item = new StackPanel { Orientation = Orientation.Horizontal, Tag = f };

                // Color swatch
                Color swatchColor = Colors.CornflowerBlue;
                try { swatchColor = (Color)ColorConverter.ConvertFromString(f.Color); } catch { }
                item.Children.Add(new Border
                {
                    Width = 12, Height = 12, CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(swatchColor),
                    Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center
                });
                item.Children.Add(new TextBlock
                {
                    Text = $"{f.Name}  ({f.Apps.Count} apps)",
                    VerticalAlignment = VerticalAlignment.Center
                });
                _list.Items.Add(item);
            }

            Grid.SetRow(_list, 1);
            grid.Children.Add(_list);

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelBtn = MakeBtn("Cancel", "#44475A");
            var addBtn    = MakeBtn("Add", "#5B8CFF");
            cancelBtn.Margin = new Thickness(0, 0, 8, 0);

            cancelBtn.Click += (s, e) => { DialogResult = false; };
            addBtn.Click    += (s, e) =>
            {
                if (_list.SelectedItem is StackPanel sp && sp.Tag is AppFolder f)
                {
                    SelectedFolder = f;
                    DialogResult   = true;
                }
                else
                {
                    MessageBox.Show("Please select a folder.", "Select Folder",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };

            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(addBtn);
            Grid.SetRow(btnRow, 2);
            grid.Children.Add(btnRow);

            Content = grid;
        }

        private static Style MakeListItemStyle()
        {
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(PaddingProperty, new Thickness(8, 5, 8, 5)));
            return style;
        }

        private static Button MakeBtn(string text, string hex)
        {
            var btn = new Button
            {
                Content = text, Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 7, 14, 7), FontSize = 13,
                Cursor = Cursors.Hand
            };
            try { btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch { }
            return btn;
        }
    }

    // ── App name dialog (unchanged) ────────────────────────────────────────

    public class AppNameDialog : Window
    {
        public string AppName { get; private set; }
        private readonly TextBox _nameBox;

        public AppNameDialog(string defaultName)
        {
            AppName = defaultName;
            Title   = "App Display Name";
            Width   = 320; Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background  = new SolidColorBrush(Color.FromRgb(30, 30, 46));
            ResizeMode  = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _nameBox = new TextBox
            {
                Text = defaultName,
                Background      = new SolidColorBrush(Color.FromRgb(42, 42, 62)),
                Foreground      = Brushes.White,
                BorderBrush     = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(8, 6, 8, 6),
                FontSize        = 13,
                CaretBrush      = Brushes.White,
                Margin          = new Thickness(0, 0, 0, 14)
            };

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okBtn     = MakeBtn("OK",     "#5B8CFF");
            var cancelBtn = MakeBtn("Cancel", "#44475A");
            okBtn.Click     += (s, e) => { AppName = _nameBox.Text; DialogResult = true; };
            cancelBtn.Click += (s, e) => { DialogResult = false; };
            cancelBtn.Margin = new Thickness(0, 0, 8, 0);

            var label = new TextBlock
            {
                Text = "Display name for this app:",
                Foreground = Brushes.White, FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8)
            };

            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(okBtn);

            Grid.SetRow(label,    0);
            Grid.SetRow(_nameBox, 1);
            Grid.SetRow(btnPanel, 2);
            grid.Children.Add(label);
            grid.Children.Add(_nameBox);
            grid.Children.Add(btnPanel);
            Content = grid;

            _nameBox.SelectAll();
            _nameBox.Focus();
        }

        private static Button MakeBtn(string text, string hex)
        {
            var btn = new Button
            {
                Content = text, Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 7, 14, 7), FontSize = 13,
                Cursor = Cursors.Hand
            };
            try { btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch { }
            return btn;
        }
    }
}
