using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;
using System.Runtime.CompilerServices;

namespace UrlSupervisor
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<Monitor> Monitors { get; } = new();
        private readonly DispatcherTimer _uiTimer;
        private readonly string _xmlPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "urls.config.xml");

        public ListCollectionView GroupedView { get; private set; }

        private string _summaryText = "";
        public string SummaryText { get => _summaryText; set { _summaryText = value; OnPropertyChanged(nameof(SummaryText)); } }

        private bool _filterErrorsOnly = false;
        private bool _filterRunningOnly = false;
        private string _searchQuery = "";
        private string? _filterGroup;
        private string? _filterTag;
        private bool _compactMode = false;
        private bool _isLightTheme = false;

        private bool _isEditPanelOpen;
        public bool IsEditPanelOpen { get => _isEditPanelOpen; set { _isEditPanelOpen = value; OnPropertyChanged(nameof(IsEditPanelOpen)); } }
        public EditableMonitor Editing { get; set; } = new EditableMonitor();
        private bool _isEditingExisting = false;
        private string _originalKey = "";
        private string _editPanelTitle = "Ajouter un site";
        public string EditPanelTitle { get => _editPanelTitle; set { _editPanelTitle = value; OnPropertyChanged(nameof(EditPanelTitle)); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? prop = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            GroupedView = new ListCollectionView(Monitors);
            GroupedView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Monitor.Group)));
            GroupedView.SortDescriptions.Add(new SortDescription(nameof(Monitor.HasError), ListSortDirection.Descending));
            GroupedView.SortDescriptions.Add(new SortDescription(nameof(Monitor.Order), ListSortDirection.Ascending));
            GroupedView.Filter = FilterMonitors;

            LoadFromXml();
            RebuildFiltersChoices();

            foreach (var m in Monitors)
            {
                m.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(Monitor.HasError) || e.PropertyName == nameof(Monitor.Order))
                        GroupedView.Refresh();
                };
            }

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _uiTimer.Tick += (_, __) =>
            {
                foreach (var m in Monitors) m.TickUptime();
                SummaryText = $"{Monitors.Count} URL(s) • {Monitors.Count(x => x.IsRunning)} en cours • erreurs: {Monitors.Count(x => x.HasError)}";
            };
            _uiTimer.Start();
        }

        private bool FilterMonitors(object obj)
        {
            if (obj is not Monitor m) return false;
            if (_filterErrorsOnly && !m.HasError) return false;
            if (_filterRunningOnly && !m.IsRunning) return false;
            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                var q = _searchQuery.Trim().ToLowerInvariant();
                if (!(m.Name.ToLowerInvariant().Contains(q) || m.Url.ToLowerInvariant().Contains(q))) return false;
            }
            if (!string.IsNullOrWhiteSpace(_filterGroup) && m.Group != _filterGroup) return false;
            if (!string.IsNullOrWhiteSpace(_filterTag) && !m.Tags.Any(t => string.Equals(t, _filterTag, StringComparison.InvariantCultureIgnoreCase))) return false;
            return true;
        }

        private void RefreshFilter() => GroupedView.Refresh();

        private void LoadFromXml()
        {
            Monitors.Clear();
            if (!File.Exists(_xmlPath))
            {
                System.Windows.MessageBox.Show($"Fichier XML introuvable: {_xmlPath}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            XDocument doc;
            try { doc = XDocument.Load(_xmlPath); }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"XML invalide: {ex.Message}", "Erreur XML", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var entries = doc.Root?.Elements("Monitor") ?? Enumerable.Empty<XElement>();
            foreach (var e in entries)
            {
                var name = (string?)e.Attribute("name") ?? "SansNom";
                var url = (string?)e.Attribute("url") ?? "";
                var order = (int?)e.Attribute("order") ?? 0;
                var interval = (int?)e.Attribute("intervalSeconds") ?? 15;
                var enabled = (bool?)e.Attribute("enabled") ?? true;
                var timeout = (int?)e.Attribute("timeoutSeconds") ?? 5;
                var group = (string?)e.Attribute("group") ?? "";
                var tagsRaw = (string?)e.Attribute("tags") ?? "";
                var tags = tagsRaw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());

                var m = new Monitor(name, url, order, interval, timeout, group, tags);
                Monitors.Add(m);
                if (enabled) _ = m.StartAsync();
                m.PropertyChanged += (_, __) => RefreshFilter();
            }
        }

        private void RebuildFiltersChoices()
        {
            var previousGroup = _filterGroup;
            var previousTag = _filterTag;

            var totalMonitors = Monitors.Count;

            var groupOptions = new List<FilterOption>
            {
                new("Tous les groupes", null, totalMonitors)
            };

            groupOptions.AddRange(
                Monitors
                    .Where(m => !string.IsNullOrWhiteSpace(m.Group))
                    .GroupBy(m => m.Group)
                    .OrderBy(g => g.Key)
                    .Select(g => new FilterOption(g.Key, g.Key, g.Count())));

            GroupFilter.ItemsSource = groupOptions;
            GroupFilter.SelectedItem = groupOptions.First();

            if (!string.IsNullOrWhiteSpace(previousGroup))
            {
                var match = groupOptions.FirstOrDefault(o => string.Equals(o.Value, previousGroup, StringComparison.InvariantCulture));
                if (match != null) GroupFilter.SelectedItem = match;
            }

            var tagOptions = new List<FilterOption>
            {
                new("Tous les tags", null, totalMonitors)
            };

            tagOptions.AddRange(
                Monitors
                    .SelectMany(m => m.Tags.Select(t => new { Tag = t, Monitor = m }))
                    .Where(x => !string.IsNullOrWhiteSpace(x.Tag))
                    .GroupBy(x => x.Tag, StringComparer.InvariantCultureIgnoreCase)
                    .OrderBy(g => g.Key, StringComparer.InvariantCultureIgnoreCase)
                    .Select(g => new FilterOption(g.Key, g.First().Tag, g.Select(x => x.Monitor).Distinct().Count())));

            TagFilter.ItemsSource = tagOptions;
            TagFilter.SelectedItem = tagOptions.First();

            if (!string.IsNullOrWhiteSpace(previousTag))
            {
                var matchTag = tagOptions.FirstOrDefault(o => string.Equals(o.Value, previousTag, StringComparison.InvariantCultureIgnoreCase));
                if (matchTag != null) TagFilter.SelectedItem = matchTag;
            }
        }

        // Header interactions
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { _searchQuery = (sender as System.Windows.Controls.TextBox)?.Text ?? ""; RefreshFilter(); }
        private void GroupFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _filterGroup = (GroupFilter.SelectedItem as FilterOption)?.Value;
            RefreshFilter();
        }
        private void TagFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _filterTag = (TagFilter.SelectedItem as FilterOption)?.Value;
            RefreshFilter();
        }

        private sealed record FilterOption(string Label, string? Value, int Count)
        {
            public string Display => Count >= 0 ? $"{Label} ({Count})" : Label;
        }
        private void ToggleErrors_Checked(object sender, RoutedEventArgs e) { _filterErrorsOnly = (sender as ToggleButton)?.IsChecked == true; RefreshFilter(); }
        private void ToggleRunning_Checked(object sender, RoutedEventArgs e) { _filterRunningOnly = (sender as ToggleButton)?.IsChecked == true; RefreshFilter(); }
        private void ToggleCompact_Checked(object sender, RoutedEventArgs e)
        {
            _compactMode = (sender as ToggleButton)?.IsChecked == true;
            var res = System.Windows.Application.Current.Resources;
            res["TileWidth"] = _compactMode ? 320.0 : 380.0;
            res["HistoryBarHeight"] = _compactMode ? 34.0 : 44.0;
            res["HistoryBarWidth"] = _compactMode ? 6.0 : 8.0;
            res["TitleFontSize"] = _compactMode ? 14.0 : 16.0;
            res["BodyFontSize"] = _compactMode ? 11.0 : 12.0;
        }
        private void ToggleTheme_Checked(object sender, RoutedEventArgs e)
        {
            _isLightTheme = (sender as ToggleButton)?.IsChecked == true;
            if (_isLightTheme) ThemeManager.UseLight(); else ThemeManager.UseDark();
        }

        private void ReloadXml_Click(object sender, RoutedEventArgs e) { LoadFromXml(); RebuildFiltersChoices(); }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog()
                {
                    Title = "Exporter les pannes en CSV",
                    Filter = "CSV (*.csv)|*.csv",
                    FileName = $"downtimes_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                };
                if (sfd.ShowDialog() != true) return;

                var sb = new StringBuilder();
                sb.AppendLine("Name,Url,StartUtc,EndUtc,DurationSeconds,Open");
                foreach (var m in Monitors)
                {
                    foreach (var d in m.Downtimes)
                    {
                        bool open = d.EndUtc == null;
                        var endStr = d.EndUtc?.ToString("o") ?? "";
                        sb.AppendLine($"{Escape(m.Name)},{Escape(m.Url)},{d.StartUtc:o},{endStr},{(int)d.Duration.TotalSeconds},{open}");
                    }
                }
                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                System.Windows.MessageBox.Show("Export terminé.", "CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erreur export: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static string Escape(string s) => s.Contains(',') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

        // Tile interactions
        private void Tile_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Monitor m)
            {
                if (m.IsRunning) m.Stop();
                else _ = m.StartAsync();
            }
        }
        private async void PingNow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Monitor m) await m.PingOnceAsync();
        }
        private async void StartStop_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Monitor m)
            {
                if (m.IsRunning) m.Stop();
                else await m.StartAsync();
            }
        }
        private void EditTile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Monitor m)
            {
                Editing = new EditableMonitor
                {
                    Name = m.Name,
                    Url = m.Url,
                    IntervalSeconds = m.IntervalSeconds,
                    TimeoutSeconds = m.TimeoutSeconds,
                    Order = m.Order,
                    Group = m.Group,
                    Tags = string.Join(", ", m.Tags),
                    Enabled = m.IsRunning
                };
                OnPropertyChanged(nameof(Editing));
                _isEditingExisting = true;
                _originalKey = m.Url;
                EditPanelTitle = "Modifier le site";
                IsEditPanelOpen = true;
            }
        }
        private void DeleteTile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Monitor m)
            {
                var confirm = System.Windows.MessageBox.Show($"Supprimer '{m.Name}' ?", "Confirmer", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;
                try
                {
                    var doc = XDocument.Load(_xmlPath);
                    var root = doc.Root!;
                    var el = root.Elements("Monitor").FirstOrDefault(x => ((string?)x.Attribute("url") ?? "") == m.Url);
                    el?.Remove();
                    doc.Save(_xmlPath);
                    LoadFromXml();
                    RebuildFiltersChoices();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Erreur lors de la suppression: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Editor
        private void AddNew_Click(object sender, RoutedEventArgs e)
        {
            Editing = new EditableMonitor
            {
                Name = "Nouveau site",
                Url = "https://",
                IntervalSeconds = 10,
                TimeoutSeconds = 5,
                Order = (Monitors.Count > 0 ? Monitors.Max(x => x.Order) + 1 : 1),
                Group = "",
                Tags = "",
                Enabled = true
            };
            OnPropertyChanged(nameof(Editing));
            _isEditingExisting = false;
            EditPanelTitle = "Ajouter un site";
            IsEditPanelOpen = true;
        }
        private void CancelEdit_Click(object sender, RoutedEventArgs e) => IsEditPanelOpen = false;

        private void SaveEdit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Editing.Name) || string.IsNullOrWhiteSpace(Editing.Url))
                {
                    System.Windows.MessageBox.Show("Nom et URL sont requis.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                XDocument doc;
                try { doc = XDocument.Load(_xmlPath); }
                catch { doc = new XDocument(new XElement("Monitors")); }
                var root = doc.Root!;

                var existingByUrl = root.Elements("Monitor").FirstOrDefault(x => ((string?)x.Attribute("url") ?? "") == Editing.Url);
                if (!_isEditingExisting && existingByUrl != null)
                {
                    System.Windows.MessageBox.Show("Cette URL existe déjà.", "Doublon", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (_isEditingExisting)
                {
                    var el = root.Elements("Monitor").FirstOrDefault(x => ((string?)x.Attribute("url") ?? "") == _originalKey);
                    if (el != null)
                    {
                        el.SetAttributeValue("name", Editing.Name);
                        el.SetAttributeValue("url", Editing.Url);
                        el.SetAttributeValue("intervalSeconds", Editing.IntervalSeconds);
                        el.SetAttributeValue("order", Editing.Order);
                        el.SetAttributeValue("enabled", Editing.Enabled);
                        el.SetAttributeValue("timeoutSeconds", Editing.TimeoutSeconds);
                        el.SetAttributeValue("group", Editing.Group ?? "");
                        el.SetAttributeValue("tags", Editing.Tags ?? "");
                    }
                }
                else
                {
                    root.Add(new XElement("Monitor",
                        new XAttribute("name", Editing.Name),
                        new XAttribute("url", Editing.Url),
                        new XAttribute("intervalSeconds", Editing.IntervalSeconds),
                        new XAttribute("order", Editing.Order),
                        new XAttribute("enabled", Editing.Enabled),
                        new XAttribute("timeoutSeconds", Editing.TimeoutSeconds),
                        new XAttribute("group", Editing.Group ?? ""),
                        new XAttribute("tags", Editing.Tags ?? "")));
                }

                doc.Save(_xmlPath);
                LoadFromXml();
                RebuildFiltersChoices();
                IsEditPanelOpen = false;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erreur lors de l'enregistrement: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
}
}
