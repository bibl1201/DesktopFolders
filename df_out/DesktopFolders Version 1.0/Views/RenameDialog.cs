using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DesktopFolders;

namespace DesktopFolders.Views
{
    /// <summary>
    /// Compact inline rename dialog themed to match the popup window.
    /// Built entirely in code — no XAML file required.
    /// </summary>
    public class RenameDialog : Window
    {
        public string NewName { get; private set; } = "";

        private readonly TextBox _box;

        public RenameDialog(string currentName, ThemeColors colors)
        {
            Title                 = "Rename";
            Width                 = 320;
            SizeToContent         = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode            = ResizeMode.NoResize;
            WindowStyle           = WindowStyle.None;
            AllowsTransparency    = true;
            Background            = Brushes.Transparent;
            ShowInTaskbar         = false;

            // Derive card colours from the popup's theme colours
            var bg = colors.PopupBackground;
            var cardBg = Color.FromArgb(
                255,
                (byte)System.Math.Min(bg.R + 12, 255),
                (byte)System.Math.Min(bg.G + 12, 255),
                (byte)System.Math.Min(bg.B + 12, 255));

            var card = new Border
            {
                CornerRadius    = new CornerRadius(12),
                Background      = new SolidColorBrush(cardBg),
                BorderBrush     = new SolidColorBrush(colors.PopupBorder),
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(20, 18, 20, 18),
                Effect          = new DropShadowEffect
                {
                    Color       = Colors.Black,
                    BlurRadius  = 24,
                    ShadowDepth = 6,
                    Opacity     = 0.5
                }
            };

            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text       = "Rename shortcut",
                FontSize   = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                Margin     = new Thickness(0, 0, 0, 10)
            });

            // Text box pre-filled with the current name, all text selected
            _box = new TextBox
            {
                Text            = currentName,
                FontSize        = 13,
                Padding         = new Thickness(8, 7, 8, 7),
                Background      = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                Foreground      = new SolidColorBrush(Colors.White),
                CaretBrush      = new SolidColorBrush(Colors.White),
                BorderBrush     = new SolidColorBrush(colors.PopupBorder),
                BorderThickness = new Thickness(1),
                SelectionBrush  = new SolidColorBrush(Color.FromArgb(80, 91, 140, 255)),
                Margin          = new Thickness(0, 0, 0, 12)
            };
            // Select all text on focus so the user can type immediately
            _box.Loaded += (_, _) =>
            {
                _box.Focus();
                _box.SelectAll();
            };
            // Enter confirms, Escape cancels
            _box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)  Confirm();
                if (e.Key == Key.Escape) { DialogResult = false; }
            };
            stack.Children.Add(_box);

            // Button row
            var btnRow = new Grid();
            btnRow.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            btnRow.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var cancelBtn = MakeBtn("Cancel",
                Color.FromArgb(50, 255, 255, 255), false);
            cancelBtn.Click += (_, _) => { DialogResult = false; };
            Grid.SetColumn(cancelBtn, 0);

            var okBtn = MakeBtn("Rename",
                Color.FromRgb(91, 140, 255), true);
            okBtn.Click += (_, _) => Confirm();
            Grid.SetColumn(okBtn, 2);

            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(okBtn);
            stack.Children.Add(btnRow);

            card.Child = stack;
            Content    = card;
        }

        private void Confirm()
        {
            string name = _box.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;
            NewName      = name;
            DialogResult = true;
        }

        private static Button MakeBtn(string text, Color bg, bool isDefault)
        {
            var tpl     = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.BackgroundProperty, new SolidColorBrush(bg));
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            factory.SetValue(Border.PaddingProperty, new Thickness(0, 9, 0, 9));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty,
                HorizontalAlignment.Center);
            factory.AppendChild(cp);
            tpl.VisualTree = factory;

            return new Button
            {
                Content   = text,
                FontSize  = 12,
                Foreground = new SolidColorBrush(Colors.White),
                IsDefault = isDefault,
                Cursor    = Cursors.Hand,
                Template  = tpl
            };
        }
    }
}
