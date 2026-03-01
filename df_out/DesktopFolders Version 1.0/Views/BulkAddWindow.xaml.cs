using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopFolders.Helpers;
using DesktopFolders.Models;

namespace DesktopFolders.Views
{
    public partial class BulkAddWindow : Window
    {
        private readonly AppFolder _folder;

        // The last result-set from Everything; re-rendered when the filter toggle changes
        private EverythingSearch.SearchResult? _lastResult;
        private string _lastSearchTerm = "";

        private readonly HashSet<string> _selected =
            new(StringComparer.OrdinalIgnoreCase);

        public BulkAddWindow(AppFolder folder)
        {
            _folder = folder;
            InitializeComponent();

            // Keep placeholder hint visible only when search box is empty
            SearchBox.TextChanged += (_, _) =>
                SearchHint.Visibility =
                    string.IsNullOrEmpty(SearchBox.Text)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
        }

        // ── Search button / Enter key ──────────────────────────────────────

        private void SearchBtn_Click(object sender, RoutedEventArgs e) => RunSearch();

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) RunSearch();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // If results are already loaded, re-filter in-memory on every keystroke
            // (instant; no new IPC call).  A new IPC search only runs on Search click.
            if (_lastResult != null && _lastResult.IsAvailable)
                RenderResults();
        }

        // ── User-installed toggle ──────────────────────────────────────────

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            // Re-render from the cached result — no new IPC call needed
            if (_lastResult != null && _lastResult.IsAvailable)
                RenderResults();
        }

        // ── Core search ───────────────────────────────────────────────────

        private async void RunSearch()
        {
            string term = SearchBox.Text.Trim();

            // Require at least one character — empty queries scan the entire
            // filesystem index and are slow. User must type something first.
            if (string.IsNullOrEmpty(term))
            {
                StatusText.Text       = "Please enter a search term first.";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 180, 60));
                return;
            }

            SetBusy(true);
            StatusText.Text       = $"Searching for \"{term}\"…";
            StatusText.Foreground = new SolidColorBrush(Color.FromArgb(112, 176, 176, 192));

            _lastSearchTerm = term;

            // Run Everything IPC off the UI thread
            var result = await Task.Run(() => EverythingSearch.Query(term));

            SetBusy(false);
            _lastResult = result;

            if (!result.IsAvailable)
            {
                StatusText.Text       = result.ErrorMessage;
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
                EmptyState.Visibility   = Visibility.Collapsed;
                ResultsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            StatusText.Foreground = new SolidColorBrush(Color.FromArgb(112, 176, 176, 192));
            RenderResults();
        }

        private void SetBusy(bool busy)
        {
            LoadingBar.Visibility = busy ? Visibility.Visible   : Visibility.Collapsed;
            SearchBtn.IsEnabled   = !busy;
        }

        // ── Render cached results with current filter state ────────────────

        private void RenderResults()
        {
            if (_lastResult == null || !_lastResult.IsAvailable) return;

            bool userOnly  = UserInstalledToggle.IsChecked == true;
            string filter  = SearchBox.Text.Trim();
            bool hasFilter = !string.IsNullOrWhiteSpace(filter);

            GroupsPanel.Children.Clear();
            int shown = 0;

            foreach (var group in _lastResult.Groups)
            {
                IEnumerable<EverythingSearch.ExeItem> items = group.Items;

                // Apply user-installed filter
                if (userOnly)
                    items = items.Where(i => i.IsUserInstalled);

                // Apply text filter
                if (hasFilter)
                    items = items.Where(i =>
                        i.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        i.Path.Contains(filter, StringComparison.OrdinalIgnoreCase));

                var list = items.ToList();
                if (list.Count == 0) continue;
                shown += list.Count;

                // Group header
                GroupsPanel.Children.Add(new TextBlock
                {
                    Text       = $"  {group.Label.ToUpperInvariant()}  ({list.Count:N0})",
                    FontSize   = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(128, 160, 160, 190)),
                    Margin     = new Thickness(0, 12, 0, 4)
                });

                foreach (var item in list)
                    GroupsPanel.Children.Add(BuildRow(item));
            }

            if (shown == 0)
                GroupsPanel.Children.Add(new TextBlock
                {
                    Text       = userOnly && !hasFilter
                        ? "No user-installed apps found. Try disabling the filter."
                        : "No results match your search.",
                    Foreground = new SolidColorBrush(Color.FromArgb(100, 200, 200, 200)),
                    Margin     = new Thickness(8, 20, 0, 0),
                    FontSize   = 12
                });

            int total = _lastResult.Groups.Sum(g => g.Items.Count);
            int visible = shown;
            string suffix = userOnly ? " · user-installed only" : "";
            suffix += hasFilter ? $" · filtered by \"{filter}\"" : "";
            StatusText.Text = $"{total:N0} found  ·  {visible:N0} shown{suffix}";

            EmptyState.Visibility   = Visibility.Collapsed;
            ResultsPanel.Visibility = Visibility.Visible;
            RefreshAddButton();
        }

        // ── Row builder ────────────────────────────────────────────────────

        private Border BuildRow(EverythingSearch.ExeItem item)
        {
            bool alreadyAdded = _folder.Apps.Any(a =>
                string.Equals(a.ExecutablePath, item.Path,
                    StringComparison.OrdinalIgnoreCase));
            bool isSelected = _selected.Contains(item.Path);

            var bgColor = isSelected
                ? Color.FromArgb(55, 91, 140, 255)
                : alreadyAdded
                    ? Color.FromArgb(14, 140, 140, 140)
                    : Colors.Transparent;

            var row = new Border
            {
                Background   = new SolidColorBrush(bgColor),
                CornerRadius = new CornerRadius(6),
                Padding      = new Thickness(8, 6, 8, 6),
                Margin       = new Thickness(0, 1, 0, 1),
                IsEnabled    = !alreadyAdded,
                Cursor       = alreadyAdded ? Cursors.Arrow : Cursors.Hand
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = GridLength.Auto });

            var nameBlock = new TextBlock
            {
                Text              = item.DisplayName,
                FontSize          = 12,
                Foreground        = alreadyAdded
                    ? new SolidColorBrush(Color.FromArgb(70, 200, 200, 200))
                    : new SolidColorBrush(Color.FromRgb(218, 218, 230)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming      = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameBlock, 0);
            grid.Children.Add(nameBlock);

            if (alreadyAdded)
            {
                var badge = new TextBlock
                {
                    Text              = "✓ Added",
                    FontSize          = 10,
                    Foreground        = new SolidColorBrush(Color.FromArgb(100, 100, 200, 100)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(8, 0, 0, 0)
                };
                Grid.SetColumn(badge, 1);
                grid.Children.Add(badge);
            }
            else
            {
                var pathBlock = new TextBlock
                {
                    Text              = TrimPath(item.Path),
                    FontSize          = 10,
                    Foreground        = new SolidColorBrush(Color.FromArgb(60, 180, 180, 190)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(8, 0, 0, 0),
                    TextTrimming      = TextTrimming.CharacterEllipsis,
                    MaxWidth          = 230
                };
                Grid.SetColumn(pathBlock, 1);
                grid.Children.Add(pathBlock);
                row.MouseLeftButtonUp += (_, _) => ToggleItem(item.Path);
            }

            row.Child = grid;
            return row;
        }

        private void ToggleItem(string path)
        {
            if (_selected.Contains(path)) _selected.Remove(path);
            else                          _selected.Add(path);
            RenderResults();
        }

        private void RefreshAddButton()
        {
            int n = _selected.Count;
            AddBtn.IsEnabled = n > 0;
            AddBtn.Content   = n == 0
                ? "Add Selected"
                : $"Add {n} Item{(n == 1 ? "" : "s")}";
        }

        // ── Footer actions ─────────────────────────────────────────────────

        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            foreach (string path in _selected)
            {
                if (_folder.Apps.Any(a =>
                        string.Equals(a.ExecutablePath, path,
                            StringComparison.OrdinalIgnoreCase)))
                    continue;
                _folder.Apps.Add(new AppEntry
                {
                    Name           = Path.GetFileNameWithoutExtension(path),
                    ExecutablePath = path
                });
            }
            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static string TrimPath(string path)
        {
            var parts = path.Split(
                Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length <= 2
                ? path
                : "…\\" + string.Join("\\", parts[^2..]);
        }
    }
}
