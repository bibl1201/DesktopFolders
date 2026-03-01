using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DesktopFolders.Views
{
    /// <summary>
    /// A small themed dialog shown on first launch when no folders exist.
    /// Built entirely in code so it needs no XAML file.
    /// Returns ShowDialog() == true when the user wants to create a folder.
    /// </summary>
    public class FirstRunDialog : Window
    {
        public FirstRunDialog()
        {
            Title               = "Desktop Folders";
            Width               = 380;
            SizeToContent       = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode          = ResizeMode.NoResize;
            WindowStyle         = WindowStyle.None;
            AllowsTransparency  = true;
            Background          = Brushes.Transparent;
            ShowInTaskbar       = false;
            Topmost             = true;
            Icon                = LoadIcon();

            // ── Outer card ─────────────────────────────────────────────────
            var card = new Border
            {
                CornerRadius    = new CornerRadius(16),
                Background      = new SolidColorBrush(Color.FromRgb(28, 28, 42)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(32, 28, 32, 24),
                Effect          = new DropShadowEffect
                {
                    Color       = Colors.Black,
                    BlurRadius  = 32,
                    ShadowDepth = 8,
                    Opacity     = 0.55
                }
            };

            var stack = new StackPanel();

            // ── Folder emoji ───────────────────────────────────────────────
            stack.Children.Add(new TextBlock
            {
                Text                = "📁",
                FontSize            = 42,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 14)
            });

            // ── Heading ────────────────────────────────────────────────────
            stack.Children.Add(new TextBlock
            {
                Text                = "Welcome to Desktop Folders",
                FontSize            = 17,
                FontWeight          = FontWeights.SemiBold,
                Foreground          = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 8)
            });

            // ── Body ───────────────────────────────────────────────────────
            stack.Children.Add(new TextBlock
            {
                Text                = "You don't have any folders yet.\nWould you like to create one now?",
                FontSize            = 12,
                Foreground          = new SolidColorBrush(Color.FromArgb(180, 200, 200, 220)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                TextWrapping        = TextWrapping.Wrap,
                Margin              = new Thickness(0, 0, 0, 26)
            });

            // ── Buttons ────────────────────────────────────────────────────
            var btnRow = new Grid();
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var skipBtn = MakeButton("Not Now", false,
                Color.FromArgb(60, 255, 255, 255), Colors.White);
            Grid.SetColumn(skipBtn, 0);

            var createBtn = MakeButton("Create Folder", true,
                Color.FromRgb(91, 140, 255), Colors.White);
            Grid.SetColumn(createBtn, 2);

            btnRow.Children.Add(skipBtn);
            btnRow.Children.Add(createBtn);
            stack.Children.Add(btnRow);

            card.Child   = stack;
            Content      = card;

            // Close on click outside
            Deactivated += (_, _) => { DialogResult = false; };
        }

        private static Button MakeButton(string text, bool isDefault,
            Color bg, Color fg)
        {
            var btn = new Button
            {
                Content   = text,
                FontSize  = 12.5,
                Foreground = new SolidColorBrush(fg),
                Cursor    = System.Windows.Input.Cursors.Hand,
                IsDefault = isDefault,
                Padding   = new Thickness(0, 10, 0, 10),
                Template  = BuildTemplate(bg)
            };
            return btn;
        }

        private static System.Windows.Controls.ControlTemplate BuildTemplate(Color bg)
        {
            // Minimal rounded button template (no XAML triggers needed)
            var tpl = new System.Windows.Controls.ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.BackgroundProperty, new SolidColorBrush(bg));
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            factory.SetValue(Border.PaddingProperty, new Thickness(0, 10, 0, 10));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty,
                HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty,
                VerticalAlignment.Center);
            factory.AppendChild(content);
            tpl.VisualTree = factory;
            return tpl;
        }

        private static System.Windows.Media.ImageSource? LoadIcon()
        {
            try
            {
                var uri = new System.Uri("pack://application:,,,/Resources/folder.ico");
                var img = new System.Windows.Media.Imaging.BitmapImage(uri);
                return img;
            }
            catch { return null; }
        }

        protected override void OnContentRendered(System.EventArgs e)
        {
            base.OnContentRendered(e);

            // Wire button clicks here so we have access to DialogResult
            if (Content is Border card && card.Child is StackPanel sp
                && sp.Children[^1] is Grid btnRow)
            {
                if (btnRow.Children[0] is Button skip)
                    skip.Click += (_, _) => { DialogResult = false; };
                if (btnRow.Children[1] is Button create)
                    create.Click += (_, _) => { DialogResult = true; };
            }
        }
    }
}
