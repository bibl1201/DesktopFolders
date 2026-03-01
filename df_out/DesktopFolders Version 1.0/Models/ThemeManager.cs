using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DesktopFolders.Models;
using Microsoft.Win32;

namespace DesktopFolders
{
    public static class ThemeManager
    {
        // ── Windows Accent Color ───────────────────────────────────────────

        public static Color GetWindowsAccentColor()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\DWM", false);
                if (key?.GetValue("AccentColor") is int raw)
                {
                    // DWM stores it as AABBGGRR
                    byte a = (byte)((raw >> 24) & 0xFF);
                    byte b = (byte)((raw >> 16) & 0xFF);
                    byte g = (byte)((raw >> 8)  & 0xFF);
                    byte r = (byte)( raw         & 0xFF);
                    return Color.FromArgb(255, r, g, b);
                }
            }
            catch { }
            return Color.FromRgb(91, 140, 255); // fallback blue
        }

        // ── Theme colors ───────────────────────────────────────────────────

        public static ThemeColors GetColors()
        {
            var t = App.DataStore.Theme;

            if (t.Theme == AppTheme.Accent)
                return BuildAccentTheme(t);

            return t.Theme switch
            {
                AppTheme.Light    => BuildLight(t),
                AppTheme.Midnight => BuildMidnight(t),
                AppTheme.Sunset   => BuildSunset(t),
                _                 => BuildDark(t)
            };
        }

        private static ThemeColors BuildAccentTheme(ThemeSettings t)
        {
            var a = GetWindowsAccentColor();
            // Derive a dark background from the accent
            var bg = Color.FromRgb(
                (byte)Math.Max(a.R * 0.15, 0),
                (byte)Math.Max(a.G * 0.15, 0),
                (byte)Math.Max(a.B * 0.15, 0));
            var bgInput = Color.FromRgb(
                (byte)Math.Min(bg.R + 20, 255),
                (byte)Math.Min(bg.G + 20, 255),
                (byte)Math.Min(bg.B + 20, 255));
            var accentBright = Color.FromRgb(
                (byte)Math.Min(a.R + 60, 255),
                (byte)Math.Min(a.G + 60, 255),
                (byte)Math.Min(a.B + 60, 255));

            return new ThemeColors
            {
                PopupBackground       = Color.FromArgb((byte)(t.PopupOpacity * 230), bg.R, bg.G, bg.B),
                PopupBorder           = Color.FromArgb(80, a.R, a.G, a.B),
                AppIconBackground     = Color.FromArgb(50, a.R, a.G, a.B),
                LabelForeground       = Colors.White,
                TitleForeground       = accentBright,
                EmptyForeground       = Color.FromArgb(136, 255, 255, 255),
                EditorBackground      = bg,
                EditorForeground      = Colors.White,
                EditorInputBackground = bgInput,
                EditorBorder          = Color.FromArgb(80, a.R, a.G, a.B),
                ButtonBackground      = a,
                ButtonSecondary       = bgInput,
                AccentColor           = a,
            };
        }

        private static ThemeColors BuildDark(ThemeSettings t) => new()
        {
            PopupBackground       = Color.FromArgb((byte)(t.PopupOpacity * 230), 30, 30, 46),
            PopupBorder           = Color.FromArgb(40, 255, 255, 255),
            AppIconBackground     = Color.FromArgb(50, 255, 255, 255),
            LabelForeground       = Colors.White,
            TitleForeground       = Colors.White,
            EmptyForeground       = Color.FromArgb(136, 255, 255, 255),
            EditorBackground      = Color.FromRgb(30, 30, 46),
            EditorForeground      = Colors.White,
            EditorInputBackground = Color.FromRgb(42, 42, 62),
            EditorBorder          = Color.FromArgb(68, 71, 90, 255),
            ButtonBackground      = Color.FromRgb(91, 140, 255),
            ButtonSecondary       = Color.FromRgb(68, 71, 90),
            AccentColor           = Color.FromRgb(91, 140, 255),
        };

        private static ThemeColors BuildLight(ThemeSettings t) => new()
        {
            PopupBackground       = Color.FromArgb((byte)(t.PopupOpacity * 220), 240, 240, 248),
            PopupBorder           = Color.FromArgb(60, 0, 0, 0),
            AppIconBackground     = Color.FromArgb(40, 0, 0, 0),
            LabelForeground       = Colors.Black,
            TitleForeground       = Colors.Black,
            EmptyForeground       = Color.FromArgb(150, 0, 0, 0),
            EditorBackground      = Color.FromRgb(235, 235, 245),
            EditorForeground      = Colors.Black,
            EditorInputBackground = Color.FromRgb(255, 255, 255),
            EditorBorder          = Color.FromArgb(80, 0, 0, 0),
            ButtonBackground      = Color.FromRgb(91, 140, 255),
            ButtonSecondary       = Color.FromRgb(200, 200, 210),
            AccentColor           = Color.FromRgb(91, 140, 255),
        };

        private static ThemeColors BuildMidnight(ThemeSettings t) => new()
        {
            PopupBackground       = Color.FromArgb((byte)(t.PopupOpacity * 230), 5, 5, 20),
            PopupBorder           = Color.FromArgb(80, 100, 100, 255),
            AppIconBackground     = Color.FromArgb(60, 255, 255, 255),
            LabelForeground       = Color.FromRgb(200, 200, 255),
            TitleForeground       = Color.FromRgb(150, 150, 255),
            EmptyForeground       = Color.FromArgb(120, 200, 200, 255),
            EditorBackground      = Color.FromRgb(5, 5, 20),
            EditorForeground      = Color.FromRgb(220, 220, 255),
            EditorInputBackground = Color.FromRgb(10, 10, 35),
            EditorBorder          = Color.FromArgb(100, 100, 100, 255),
            ButtonBackground      = Color.FromRgb(80, 80, 200),
            ButtonSecondary       = Color.FromRgb(30, 30, 60),
            AccentColor           = Color.FromRgb(100, 100, 255),
        };

        private static ThemeColors BuildSunset(ThemeSettings t) => new()
        {
            PopupBackground       = Color.FromArgb((byte)(t.PopupOpacity * 220), 40, 15, 30),
            PopupBorder           = Color.FromArgb(80, 255, 120, 60),
            AppIconBackground     = Color.FromArgb(50, 255, 255, 255),
            LabelForeground       = Color.FromRgb(255, 220, 190),
            TitleForeground       = Color.FromRgb(255, 160, 80),
            EmptyForeground       = Color.FromArgb(120, 255, 200, 150),
            EditorBackground      = Color.FromRgb(40, 15, 30),
            EditorForeground      = Color.FromRgb(255, 220, 190),
            EditorInputBackground = Color.FromRgb(55, 25, 40),
            EditorBorder          = Color.FromArgb(100, 255, 120, 60),
            ButtonBackground      = Color.FromRgb(220, 100, 50),
            ButtonSecondary       = Color.FromRgb(80, 30, 50),
            AccentColor           = Color.FromRgb(255, 120, 60),
        };

        // ── Widget helpers ─────────────────────────────────────────────────

        public static double GetIconSize() => App.DataStore.Theme.IconSize switch
        {
            IconSize.Small => 40, IconSize.Large => 68, _ => 52
        };

        public static double GetWidgetIconSize() => App.DataStore.Theme.IconSize switch
        {
            IconSize.Small => 60, IconSize.Large => 84, _ => 72
        };

        public static double GetCornerRadius() => App.DataStore.Theme.RoundedIcons ? 13 : 4;

        public static CornerRadius GetWidgetCornerRadius(double size) =>
            App.DataStore.Theme.WidgetShape switch
            {
                WidgetShape.Circle => new CornerRadius(size / 2),
                WidgetShape.Sharp  => new CornerRadius(4),
                _                  => new CornerRadius(size * 0.21)
            };

        public static Brush GetWidgetBackground(string folderColor)
        {
            var t = App.DataStore.Theme;
            Color baseColor = Colors.CornflowerBlue;
            try { baseColor = (Color)ColorConverter.ConvertFromString(folderColor); } catch { }

            switch (t.WidgetStyle)
            {
                case WidgetStyle.Solid:
                    return new SolidColorBrush(Color.FromArgb(
                        (byte)(t.WidgetOpacity * 255), baseColor.R, baseColor.G, baseColor.B));

                case WidgetStyle.Glass:
                    return new SolidColorBrush(Color.FromArgb(
                        (byte)(t.WidgetOpacity * 160), baseColor.R, baseColor.G, baseColor.B));

                case WidgetStyle.Outline:
                    return new SolidColorBrush(Color.FromArgb(
                        (byte)(t.WidgetOpacity * 30), baseColor.R, baseColor.G, baseColor.B));

                default: // Gradient
                    var dark = Color.FromArgb(
                        (byte)(t.WidgetOpacity * 255),
                        (byte)Math.Max(baseColor.R - 45, 0),
                        (byte)Math.Max(baseColor.G - 45, 0),
                        (byte)Math.Max(baseColor.B - 45, 0));
                    var light = Color.FromArgb(
                        (byte)(t.WidgetOpacity * 255), baseColor.R, baseColor.G, baseColor.B);
                    var brush = new LinearGradientBrush { StartPoint = new Point(0,0), EndPoint = new Point(0,1) };
                    brush.GradientStops.Add(new GradientStop(light, 0));
                    brush.GradientStops.Add(new GradientStop(dark,  1));
                    return brush;
            }
        }

        public static Brush GetWidgetBorderBrush(string folderColor)
        {
            Color baseColor = Colors.CornflowerBlue;
            try { baseColor = (Color)ColorConverter.ConvertFromString(folderColor); } catch { }
            return App.DataStore.Theme.WidgetStyle == WidgetStyle.Outline
                ? new SolidColorBrush(baseColor)
                : new SolidColorBrush(Colors.Transparent);
        }

        public static double GetWidgetBorderThickness() =>
            App.DataStore.Theme.WidgetStyle == WidgetStyle.Outline ? 2.5 : 0;

        public static Effect? GetWidgetShadow()
        {
            if (!App.DataStore.Theme.WidgetShadow) return null;
            return new DropShadowEffect
            {
                Color       = Colors.Black,
                BlurRadius  = App.DataStore.Theme.WidgetStyle == WidgetStyle.Glass ? 16 : 10,
                ShadowDepth = 2,
                Opacity     = App.DataStore.Theme.WidgetStyle == WidgetStyle.Glass ? 0.35 : 0.5
            };
        }
    }

    public class ThemeColors
    {
        public Color PopupBackground      { get; set; }
        public Color PopupBorder          { get; set; }
        public Color AppIconBackground    { get; set; }
        public Color LabelForeground      { get; set; }
        public Color TitleForeground      { get; set; }
        public Color EmptyForeground      { get; set; }
        public Color EditorBackground     { get; set; }
        public Color EditorForeground     { get; set; }
        public Color EditorInputBackground{ get; set; }
        public Color EditorBorder         { get; set; }
        public Color ButtonBackground     { get; set; }
        public Color ButtonSecondary      { get; set; }
        public Color AccentColor          { get; set; }
    }
}
