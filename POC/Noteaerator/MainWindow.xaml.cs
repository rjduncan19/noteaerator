using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using Noteaerator.Core;

namespace Noteaerator;

/// <summary>
/// On-disk schema for projects.json. Old form was a plain string array of
/// folder paths; new form is an array of these objects so per-project
/// settings (currently just <see cref="GroupByPrefix"/>) can be persisted.
/// Loader accepts both shapes; saver always writes the new shape.
/// </summary>
internal sealed class ProjectConfig
{
    public string Path { get; set; } = "";
    public bool GroupByPrefix { get; set; } = true;
}

public partial class MainWindow : Window
{
    private const string ArchiveSubdir = "archive";

    private readonly List<ProjectTab> _projects = new();
    private readonly string _configPath;
    private System.Windows.Threading.DispatcherTimer? _searchDebounce;

    public MainWindow()
    {
        InitializeComponent();
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "noteaerator", "viewer");
        Directory.CreateDirectory(dir);
        _configPath = Path.Combine(dir, "projects.json");
        Loaded += (_, _) => LoadProjects();
        Closed += (_, _) => SaveProjects();
    }

    private void LoadProjects()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                // First run ever — drop a getting-started project so a brand-
                // new install isn't an empty window. Only triggers when the
                // config file is fully absent; if the user later removes the
                // last project we keep the empty list and don't re-seed.
                var seeded = FirstRunSeeder.TrySeed();
                if (seeded != null)
                {
                    AddProject(seeded, groupByPrefix: true);
                    SaveProjects();
                }
            }
            else
            {
                foreach (var cfg in ParseProjectConfigs(File.ReadAllText(_configPath)))
                {
                    if (Directory.Exists(cfg.Path))
                        AddProject(cfg.Path, cfg.GroupByPrefix);
                }
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to load projects: {ex.Message}", null);
        }

        if (ProjectsTabs.Items.Count > 0)
            ProjectsTabs.SelectedIndex = 0;
    }

    /// <summary>
    /// Accept both legacy (string[]) and new (object[]) shapes so existing
    /// projects.json files keep loading after the schema bump. JSON property
    /// lookups are case-insensitive so the parser works whether the writer
    /// used camelCase (manual) or PascalCase (default System.Text.Json).
    /// </summary>
    private static IEnumerable<ProjectConfig> ParseProjectConfigs(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) yield break;
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.String)
            {
                yield return new ProjectConfig { Path = el.GetString() ?? "", GroupByPrefix = true };
            }
            else if (el.ValueKind == JsonValueKind.Object)
            {
                string? path = null;
                bool? group = null;
                foreach (var prop in el.EnumerateObject())
                {
                    if (prop.NameEquals("path") &&
                        prop.Value.ValueKind == JsonValueKind.String)
                        path = prop.Value.GetString();
                    else if (prop.NameEquals("groupByPrefix"))
                        group = prop.Value.ValueKind != JsonValueKind.False;
                    else if (string.Equals(prop.Name, "Path",
                                StringComparison.OrdinalIgnoreCase) &&
                             prop.Value.ValueKind == JsonValueKind.String)
                        path = prop.Value.GetString();
                    else if (string.Equals(prop.Name, "GroupByPrefix",
                                StringComparison.OrdinalIgnoreCase))
                        group = prop.Value.ValueKind != JsonValueKind.False;
                }
                yield return new ProjectConfig
                {
                    Path = path ?? "",
                    GroupByPrefix = group ?? true
                };
            }
        }
    }

    private void SaveProjects()
    {
        try
        {
            var cfgs = _projects
                .Select(p => new ProjectConfig { Path = p.FolderPath, GroupByPrefix = p.GroupByPrefix })
                .ToList();
            File.WriteAllText(_configPath, JsonSerializer.Serialize(cfgs,
                new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
        }
        catch { /* best-effort */ }
    }

    private void OnAddProject(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Pick a project folder containing .md files" };
        if (dlg.ShowDialog() == true)
        {
            AddProject(dlg.FolderName, groupByPrefix: true);
            ProjectsTabs.SelectedIndex = ProjectsTabs.Items.Count - 1;
            SaveProjects();
        }
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        if (ProjectsTabs.SelectedItem is TabItem ti && ti.Tag is ProjectTab pt)
            pt.Refresh();
    }

    private void OnProjectChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != ProjectsTabs) return;
        if (ProjectsTabs.SelectedItem is TabItem ti && ti.Tag is ProjectTab pt)
            SetStatus(pt.FolderPath, pt.FolderPath);
    }

    private void RemoveProject(ProjectTab pt)
    {
        var ti = ProjectsTabs.Items.Cast<TabItem>().FirstOrDefault(t => ReferenceEquals(t.Tag, pt));
        if (ti == null) return;
        pt.Dispose();
        _projects.Remove(pt);
        ProjectsTabs.Items.Remove(ti);
        SaveProjects();
    }

    private void SetStatus(string text, string? tooltip)
    {
        StatusText.Text = text;
        StatusText.ToolTip = string.IsNullOrEmpty(tooltip) ? null : tooltip;
    }

    // ---------------- Search ----------------

    private ProjectTab? CurrentProject =>
        ProjectsTabs.SelectedItem is TabItem ti && ti.Tag is ProjectTab pt ? pt : null;

    private void OnSearchButtonClick(object sender, RoutedEventArgs e) => OpenSearch();
    private void OnCloseSearchClick(object sender, RoutedEventArgs e) => CloseSearch();

    private void OnCloseSearchExecuted(object sender, ExecutedRoutedEventArgs e) => CloseSearch();
    private void OnCloseSearchCanExecute(object sender, CanExecuteRoutedEventArgs e)
        => e.CanExecute = SearchPopup.IsOpen;

    private void OpenSearch()
    {
        if (CurrentProject == null)
        {
            SetStatus("Add a project folder first to enable search.", null);
            return;
        }
        // Anchor the popup to the right side of the project area.
        // PlacementTarget = ProjectsTabs; offset puts it 8px from top, 16px from right edge.
        PositionSearchPopup();
        SearchPopup.IsOpen = true;
        var hasFile = CurrentProject.CurrentFile != null;
        ScopeFile.IsEnabled = hasFile;
        if (!hasFile && ScopeFile.IsChecked == true)
        {
            ScopeProject.IsChecked = true;
        }
        SearchInput.Focus();
        SearchInput.SelectAll();
        UpdateSearchSummary();
    }

    private void PositionSearchPopup()
    {
        // Right-align: HorizontalOffset = (target width) - (panel width + right margin).
        // The panel itself has width 440 with a 16px right margin baked into the Border;
        // we offset by (target width - 440 - 16) and 8px from top.
        const double panelWidth = 440 + 16; // panel + margin
        const double topInset = 8;
        var targetWidth = ProjectsTabs.ActualWidth;
        SearchPopup.HorizontalOffset = Math.Max(0, targetWidth - panelWidth);
        SearchPopup.VerticalOffset = topInset;
    }

    private void CloseSearch()
    {
        SearchPopup.IsOpen = false;
    }

    private void OnSearchPopupClosed(object? sender, EventArgs e)
    {
        // No-op for now; could clear input if desired.
    }

    private void OnSearchInputKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape) { CloseSearch(); e.Handled = true; }
        else if (e.Key == System.Windows.Input.Key.Down) { SearchResults.Focus();
            if (SearchResults.Items.Count > 0) SearchResults.SelectedIndex = 0;
            e.Handled = true; }
        else if (e.Key == System.Windows.Input.Key.Enter) { ActivateSelectedResult(); e.Handled = true; }
    }

    private void OnSearchResultsKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) { ActivateSelectedResult(); e.Handled = true; }
        else if (e.Key == System.Windows.Input.Key.Escape) { CloseSearch(); e.Handled = true; }
    }

    private void OnSearchInputChanged(object sender, TextChangedEventArgs e)
    {
        // Debounce by 180ms so typing doesn't spawn a scan per keystroke.
        if (_searchDebounce == null)
        {
            _searchDebounce = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(180)
            };
            _searchDebounce.Tick += (_, _) =>
            {
                _searchDebounce!.Stop();
                _ = RunSearchAsync();
            };
        }
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private void OnScopeChanged(object sender, RoutedEventArgs e) => _ = RunSearchAsync();

    private async System.Threading.Tasks.Task RunSearchAsync()
    {
        SearchResults.ItemsSource = null;
        var query = (SearchInput.Text ?? "").Trim();
        if (query.Length == 0) { UpdateSearchSummary(); return; }
        var pt = CurrentProject;
        if (pt == null) return;

        var fileScope = ScopeFile.IsChecked == true && pt.CurrentFile != null;
        var rootForScan = fileScope ? null : pt.FolderPath;
        var singleFile = fileScope ? pt.CurrentFile : null;

        SearchSummary.Text = "searching...";
        var hits = await System.Threading.Tasks.Task.Run(() =>
            SearchEngine.Search(query, rootForScan, singleFile));

        SearchResults.ItemsSource = hits;
        var pluralH = hits.Count == 1 ? "" : "s";
        var scopeLbl = fileScope ? "in file" : "in project";
        SearchSummary.Text = $"{hits.Count} hit{pluralH} {scopeLbl}";
    }

    private void UpdateSearchSummary()
    {
        SearchSummary.Text = string.IsNullOrEmpty(SearchInput.Text)
            ? "type to search"
            : "";
    }

    private void OnSearchResultActivate(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => ActivateSelectedResult();

    private void ActivateSelectedResult()
    {
        if (SearchResults.SelectedItem is not SearchHit hit) return;
        var pt = CurrentProject;
        if (pt == null) return;
        pt.OpenFileForSearch(hit);
        // Keep search panel open so the user can pick the next result.
        SearchInput.Focus();
    }

    private void AddProject(string folderPath, bool groupByPrefix)
    {
        if (_projects.Any(p => string.Equals(p.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase)))
            return;

        var pt = new ProjectTab(folderPath, (text, tip) =>
            Dispatcher.Invoke(() => SetStatus(text, tip)))
        {
            GroupByPrefix = groupByPrefix
        };
        _projects.Add(pt);

        var headerText = new TextBlock { Text = Path.GetFileName(folderPath.TrimEnd('\\', '/')) };

        var tabItem = new TabItem
        {
            Header = headerText,
            ToolTip = folderPath,
            Content = pt.Root,
            Tag = pt
        };

        // Right-click on a project tab:
        //   * Group by prefix (checkable, default ON) — toggles per-project.
        //   * Remove project from list.
        var menu = new ContextMenu();

        var groupItem = new MenuItem
        {
            Header = "Group by prefix",
            IsCheckable = true,
            IsChecked = pt.GroupByPrefix,
            ToolTip = "Group files that share a leading dash-separated prefix (e.g. corp-orcl, corp-orcl-thomas)."
        };
        groupItem.Click += (_, _) =>
        {
            pt.GroupByPrefix = groupItem.IsChecked;
            SaveProjects();
        };
        menu.Items.Add(groupItem);

        menu.Items.Add(new Separator());

        var removeItem = new MenuItem { Header = "Remove project from list" };
        removeItem.Click += (_, _) =>
        {
            var ans = MessageBox.Show(
                $"Remove this project from the list?\n\n{folderPath}\n\nFiles on disk are not affected.",
                "Remove project", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ans == MessageBoxResult.Yes) RemoveProject(pt);
        };
        menu.Items.Add(removeItem);

        tabItem.ContextMenu = menu;

        ProjectsTabs.Items.Add(tabItem);
    }
}


internal sealed class ProjectTab : IDisposable
{
    private const string ArchiveSubdir = "archive";

    public string FolderPath { get; }
    public Grid Root { get; }
    public string? CurrentFile => _currentFile;

    private bool _groupByPrefix = true;
    public bool GroupByPrefix
    {
        get => _groupByPrefix;
        set
        {
            if (_groupByPrefix == value) return;
            _groupByPrefix = value;
            // Drop the trees so the next populate starts from a clean slate
            // when switching modes (no half-applied expand state).
            _activeTree = null;
            _archivedTree = null;
            PopulateFiles();
        }
    }

    private readonly ListBox _activeList;
    private readonly ListBox _archivedList;
    private readonly Expander _archiveExpander;
    private readonly TextBlock _archiveHeaderText;
    private readonly ObservableCollection<FileListRow> _activeRows = new();
    private readonly ObservableCollection<FileListRow> _archivedRows = new();

    private PrefixNode? _activeTree;
    private PrefixNode? _archivedTree;

    private readonly WebView2 _webView;
    private readonly FileSystemWatcher _mdWatcher;
    private readonly FileSystemWatcher _commentsWatcher;
    private readonly Action<string, string?> _setStatus;
    private readonly SynchronizationContext _ui;
    private bool _webViewReady;
    private (string md, string commentsJson)? _pending;
    private string? _currentFile;
    private bool _suppressSelChange;

    private DateTime _suppressSidecarUntil = DateTime.MinValue;

    // FileSystemWatcher fires multiple events per single save (especially when
    // OneDrive or another editor rewrites the file). Coalesce them so we only
    // re-render once per quiescent period — multiple back-to-back renders can
    // race scroll preservation and visually jump (issue #1).
    private System.Windows.Threading.DispatcherTimer? _mdEventDebounce;
    private readonly HashSet<string> _pendingMdChanges =
        new(StringComparer.OrdinalIgnoreCase);

    public ProjectTab(string folderPath, Action<string, string?> setStatus)
    {
        FolderPath = folderPath;
        _setStatus = setStatus;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();

        Root = new Grid();
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // ---- Left pane: active list (fills) + archive expander (docked bottom) ----
        var leftPanel = new DockPanel { LastChildFill = true };
        leftPanel.SetResourceReference(Control.BackgroundProperty, "SurfaceBrush");

        _activeList = new ListBox { ItemsSource = _activeRows };
        if (Application.Current?.TryFindResource("FileListBoxStyle") is Style listStyle)
            _activeList.Style = listStyle;
        _activeList.ItemTemplate = BuildFileItemTemplate();
        _activeList.SelectionChanged += OnActiveSelected;
        _activeList.PreviewMouseRightButtonDown += (s, e) => OnFileRightClick(s, e, isArchived: false);

        _archivedList = new ListBox { ItemsSource = _archivedRows };
        if (Application.Current?.TryFindResource("FileListBoxStyle") is Style listStyle2)
            _archivedList.Style = listStyle2;
        _archivedList.ItemTemplate = BuildFileItemTemplate();
        _archivedList.SelectionChanged += OnArchivedSelected;
        _archivedList.PreviewMouseRightButtonDown += (s, e) => OnFileRightClick(s, e, isArchived: true);

        // Expander header is custom so we can show a count badge.
        _archiveHeaderText = new TextBlock
        {
            Text = "Archive",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold
        };
        _archiveHeaderText.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");

        _archiveExpander = new Expander
        {
            IsExpanded = false,
            Header = _archiveHeaderText
        };
        if (Application.Current?.TryFindResource("ArchiveExpanderStyle") is Style expStyle)
            _archiveExpander.Style = expStyle;

        var archScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 220,
            Content = _archivedList
        };
        _archiveExpander.Content = archScroll;

        DockPanel.SetDock(_archiveExpander, Dock.Bottom);
        leftPanel.Children.Add(_archiveExpander);
        leftPanel.Children.Add(_activeList);

        Grid.SetColumn(leftPanel, 0);
        Root.Children.Add(leftPanel);

        var splitter = new GridSplitter
        {
            Width = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = (System.Windows.Media.Brush?)
                Application.Current?.TryFindResource("BorderBrush")
                ?? System.Windows.Media.Brushes.LightGray
        };
        Grid.SetColumn(splitter, 1);
        Root.Children.Add(splitter);

        _webView = new WebView2();
        Grid.SetColumn(_webView, 2);
        Root.Children.Add(_webView);

        _ = InitWebViewAsync();

        // Watchers — non-recursive on the project root (only top-level *.md
        // matters) and on the archive subdir if it exists. Recursive watching
        // of a OneDrive folder fires events for every descendant change and
        // routinely overflows the watcher's 8 KB default buffer, after which
        // events for legitimately-relevant files (a new top-level .md) are
        // silently dropped. Buffer is also raised to 64 KB and we handle the
        // Error event by re-creating the watcher and re-enumerating.
        _mdWatcher = CreateMdWatcher();
        _commentsWatcher = CreateCommentsWatcher();
        if (Directory.Exists(ArchiveDir))
        {
            _archiveMdWatcher = CreateMdWatcherFor(ArchiveDir);
            _archiveCommentsWatcher = CreateCommentsWatcherFor(ArchiveDir);
        }

        PopulateFiles();
    }

    private FileSystemWatcher? _archiveMdWatcher;
    private FileSystemWatcher? _archiveCommentsWatcher;

    private FileSystemWatcher CreateMdWatcher() => CreateMdWatcherFor(FolderPath);
    private FileSystemWatcher CreateCommentsWatcher() => CreateCommentsWatcherFor(FolderPath);

    private FileSystemWatcher CreateMdWatcherFor(string dir)
    {
        var w = new FileSystemWatcher(dir, "*.md")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
            InternalBufferSize = 64 * 1024,
            EnableRaisingEvents = true
        };
        w.Changed += OnMdChanged;
        w.Created += OnMdChanged;
        w.Deleted += OnMdChanged;
        w.Renamed += OnMdRenamed;
        w.Error += OnWatcherError;
        return w;
    }

    private FileSystemWatcher CreateCommentsWatcherFor(string dir)
    {
        var w = new FileSystemWatcher(dir, "*-comments.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
            InternalBufferSize = 64 * 1024,
            EnableRaisingEvents = true
        };
        w.Changed += OnSidecarChanged;
        w.Created += OnSidecarChanged;
        w.Deleted += OnSidecarChanged;
        w.Renamed += (_, e) => OnSidecarChanged(this, e);
        w.Error += OnWatcherError;
        return w;
    }

    // If the watcher buffer overflows (common on OneDrive folders with bursty
    // sync activity), Windows drops events. Re-enumerate so we don't miss a
    // new top-level .md, and ensure the watcher is still raising events.
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _ui.Post(_ =>
        {
            try
            {
                if (sender is FileSystemWatcher w && !w.EnableRaisingEvents)
                    w.EnableRaisingEvents = true;
            }
            catch { /* best-effort */ }
            PopulateFiles();
            _ = LoadCurrentAsync();
        }, null);
    }

    private DataTemplate BuildFileItemTemplate()
    {
        // Each row: [chevron-or-spacer 16px] [indented label with ellipsis].
        // Indent is applied to the outer DockPanel via a depth -> Thickness
        // converter so child rows visually shift right. Right-click is wired
        // at the ListBox level (PreviewMouseRightButtonDown) so the menu is
        // built freshly per click.
        var template = new DataTemplate(typeof(FileListRow));

        var dock = new FrameworkElementFactory(typeof(DockPanel));
        dock.SetValue(DockPanel.LastChildFillProperty, true);
        dock.SetBinding(FrameworkElement.MarginProperty,
            new System.Windows.Data.Binding(nameof(FileListRow.Depth))
            {
                Converter = new DepthToIndentConverter()
            });

        // Chevron (dedicated hit-target so a click on it does not also
        // open the file behind a file-folder row).
        var chevron = new FrameworkElementFactory(typeof(TextBlock));
        chevron.SetValue(DockPanel.DockProperty, Dock.Left);
        chevron.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(FileListRow.ChevronGlyph)));
        chevron.SetValue(FrameworkElement.WidthProperty, 14.0);
        chevron.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
        chevron.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        chevron.SetValue(TextBlock.FontSizeProperty, 10.0);
        chevron.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);
        chevron.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnChevronClick));

        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(FileListRow.Display)));
        label.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        label.SetBinding(FrameworkElement.ToolTipProperty,
            new System.Windows.Data.Binding(nameof(FileListRow.FilePath)));

        dock.AppendChild(chevron);
        dock.AppendChild(label);

        template.VisualTree = dock;
        template.Seal();
        return template;
    }

    private void OnChevronClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe &&
            fe.DataContext is FileListRow row &&
            row.HasChildren && row.Node != null)
        {
            row.Node.IsExpanded = !row.Node.IsExpanded;
            // We are grouping ON if we got a chevron click at all.
            PopulateFiles();
            e.Handled = true;
        }
    }

    private void OnFileRightClick(object sender, MouseButtonEventArgs e, bool isArchived)
    {
        if (sender is not ListBox lb) return;
        var dep = e.OriginalSource as DependencyObject;
        while (dep != null && dep is not ListBoxItem) dep = VisualTreeHelper.GetParent(dep);
        if (dep is not ListBoxItem lbi || lbi.DataContext is not FileListRow row) return;

        // Folder-only rows (synthetic groups) have no file to archive — bail.
        if (!row.IsFile) { e.Handled = true; return; }

        lbi.IsSelected = true;

        var menu = new ContextMenu();
        var item = new MenuItem
        {
            Header = isArchived ? "Restore from Archive" : "Move to Archive…"
        };
        var path = row.FilePath!;
        item.Click += (_, _) =>
        {
            if (isArchived) RestoreFile(path);
            else            ArchiveFile(path);
        };
        menu.Items.Add(item);
        menu.PlacementTarget = lbi;
        menu.IsOpen = true;
        e.Handled = true;
    }

    // ---------------- Archive operations ----------------

    private string ArchiveDir => Path.Combine(FolderPath, ArchiveSubdir);

    private void ArchiveFile(string mdPath)
    {
        try
        {
            Directory.CreateDirectory(ArchiveDir);
            var name = Path.GetFileName(mdPath);
            var dest = Path.Combine(ArchiveDir, name);
            if (File.Exists(dest))
            {
                _setStatus($"Archive already contains a file named {name}", null);
                return;
            }
            File.Move(mdPath, dest);

            // Move sidecar if present
            var srcSidecar = CommentStore.SidecarPath(mdPath);
            if (File.Exists(srcSidecar))
            {
                var dstSidecar = CommentStore.SidecarPath(dest);
                if (!File.Exists(dstSidecar)) File.Move(srcSidecar, dstSidecar);
            }

            // If we just archived the open file, close it.
            if (string.Equals(_currentFile, mdPath, StringComparison.OrdinalIgnoreCase))
                _currentFile = null;

            PopulateFiles();
        }
        catch (Exception ex)
        {
            _setStatus($"Archive failed: {ex.Message}", null);
        }
    }

    private void RestoreFile(string archivedPath)
    {
        try
        {
            var name = Path.GetFileName(archivedPath);
            var dest = Path.Combine(FolderPath, name);
            if (File.Exists(dest))
            {
                _setStatus($"Project already contains a file named {name}", null);
                return;
            }
            File.Move(archivedPath, dest);

            var srcSidecar = CommentStore.SidecarPath(archivedPath);
            if (File.Exists(srcSidecar))
            {
                var dstSidecar = CommentStore.SidecarPath(dest);
                if (!File.Exists(dstSidecar)) File.Move(srcSidecar, dstSidecar);
            }

            PopulateFiles();
        }
        catch (Exception ex)
        {
            _setStatus($"Restore failed: {ex.Message}", null);
        }
    }

    // ---------------- File listing + selection ----------------

    private void PopulateFiles()
    {
        var prevFile = _currentFile;

        var active = SafeEnum(FolderPath).ToList();
        var archived = Directory.Exists(ArchiveDir)
            ? SafeEnum(ArchiveDir).ToList()
            : new List<string>();

        // Spin up the archive watchers lazily once the archive dir exists.
        if (Directory.Exists(ArchiveDir) && _archiveMdWatcher == null)
        {
            try
            {
                _archiveMdWatcher = CreateMdWatcherFor(ArchiveDir);
                _archiveCommentsWatcher = CreateCommentsWatcherFor(ArchiveDir);
            }
            catch { /* best-effort */ }
        }

        List<FileListRow> activeRows;
        List<FileListRow> archivedRows;
        if (_groupByPrefix)
        {
            _activeTree = PrefixGrouping.BuildTree(active, previous: _activeTree);
            activeRows = PrefixGrouping.Flatten(_activeTree).ToList();
            _archivedTree = PrefixGrouping.BuildTree(archived, previous: _archivedTree);
            archivedRows = PrefixGrouping.Flatten(_archivedTree).ToList();
        }
        else
        {
            _activeTree = null;
            _archivedTree = null;
            activeRows = PrefixGrouping.Flat(active).ToList();
            archivedRows = PrefixGrouping.Flat(archived).ToList();
        }

        _suppressSelChange = true;
        try
        {
            _activeRows.Clear();
            foreach (var r in activeRows) _activeRows.Add(r);
            _archivedRows.Clear();
            foreach (var r in archivedRows) _archivedRows.Add(r);
        }
        finally { _suppressSelChange = false; }

        // Show count of *files* (not rows) in the archive header.
        var archivedFileCount = archived.Count;
        _archiveHeaderText.Text = archivedFileCount > 0
            ? $"Archive  ({archivedFileCount})"
            : "Archive";

        if (active.Count == 0 && archived.Count == 0)
        {
            _setStatus($"No .md files in {FolderPath}", FolderPath);
            return;
        }

        // Restore previous selection.
        FileListRow? toSelect = null;
        bool inArchive = false;
        if (prevFile != null)
        {
            toSelect = _activeRows.FirstOrDefault(r => r.IsFile && Eq(r.FilePath, prevFile));
            if (toSelect == null)
            {
                toSelect = _archivedRows.FirstOrDefault(r => r.IsFile && Eq(r.FilePath, prevFile));
                if (toSelect != null) inArchive = true;
            }
        }
        toSelect ??= _activeRows.FirstOrDefault(r => r.IsFile);

        if (toSelect != null)
        {
            _suppressSelChange = true;
            try
            {
                if (inArchive)
                {
                    _archivedList.SelectedItem = toSelect;
                    _activeList.SelectedItem = null;
                }
                else
                {
                    _activeList.SelectedItem = toSelect;
                    _archivedList.SelectedItem = null;
                }
            }
            finally { _suppressSelChange = false; }
            _currentFile = toSelect.FilePath;
            _ = LoadCurrentAsync();
        }
    }

    private static bool Eq(string? a, string? b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SafeEnum(string dir)
    {
        try   { return Directory.EnumerateFiles(dir, "*.md", SearchOption.TopDirectoryOnly); }
        catch { return Array.Empty<string>(); }
    }

    private void OnActiveSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelChange) return;
        if (_activeList.SelectedItem is not FileListRow row) return;

        if (!row.IsFile)
        {
            // Clicking a synthetic folder row toggles expansion and
            // immediately clears the selection so it never looks "active".
            if (row.HasChildren && row.Node != null)
            {
                row.Node.IsExpanded = !row.Node.IsExpanded;
                _suppressSelChange = true;
                try { _activeList.SelectedItem = null; }
                finally { _suppressSelChange = false; }
                PopulateFiles();
            }
            return;
        }

        _suppressSelChange = true;
        try { _archivedList.SelectedItem = null; }
        finally { _suppressSelChange = false; }
        _currentFile = row.FilePath;
        _ = LoadCurrentAsync();
    }

    private void OnArchivedSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelChange) return;
        if (_archivedList.SelectedItem is not FileListRow row) return;

        if (!row.IsFile)
        {
            if (row.HasChildren && row.Node != null)
            {
                row.Node.IsExpanded = !row.Node.IsExpanded;
                _suppressSelChange = true;
                try { _archivedList.SelectedItem = null; }
                finally { _suppressSelChange = false; }
                PopulateFiles();
            }
            return;
        }

        _suppressSelChange = true;
        try { _activeList.SelectedItem = null; }
        finally { _suppressSelChange = false; }
        _currentFile = row.FilePath;
        _ = LoadCurrentAsync();
    }

    public void Refresh()
    {
        PopulateFiles();
        _ = LoadCurrentAsync();
    }

    private SearchHit? _pendingScroll;

    /// <summary>
    /// Open the file referenced by a search hit and ask the renderer to scroll
    /// to either the matching line or the matching sidecar comment.
    /// </summary>
    public void OpenFileForSearch(SearchHit hit)
    {
        var row = _activeRows.FirstOrDefault(r => r.IsFile && Eq(r.FilePath, hit.FilePath))
               ?? _archivedRows.FirstOrDefault(r => r.IsFile && Eq(r.FilePath, hit.FilePath));
        if (row == null)
        {
            // The file may exist on disk but be hidden inside a collapsed
            // group — expand its ancestors and try again.
            EnsureVisible(hit.FilePath);
            row = _activeRows.FirstOrDefault(r => r.IsFile && Eq(r.FilePath, hit.FilePath))
               ?? _archivedRows.FirstOrDefault(r => r.IsFile && Eq(r.FilePath, hit.FilePath));
            if (row == null) return;
        }

        var inArchive = _archivedRows.Contains(row);
        if (inArchive) _archiveExpander.IsExpanded = true;

        _suppressSelChange = true;
        try
        {
            if (inArchive) { _archivedList.SelectedItem = row; _activeList.SelectedItem = null; }
            else           { _activeList.SelectedItem   = row; _archivedList.SelectedItem = null; }
        }
        finally { _suppressSelChange = false; }

        _currentFile = row.FilePath;
        _pendingScroll = hit;
        _ = LoadCurrentAsync();
    }

    /// <summary>
    /// Expand every ancestor of the row holding <paramref name="filePath"/>
    /// (in either tree) so search hits aren't lost inside a collapsed group.
    /// </summary>
    private void EnsureVisible(string filePath)
    {
        bool expanded = ExpandAncestorsOf(_activeTree, filePath)
                     || ExpandAncestorsOf(_archivedTree, filePath);
        if (expanded) PopulateFiles();
    }

    private static bool ExpandAncestorsOf(PrefixNode? root, string filePath)
    {
        if (root == null) return false;
        return Walk(root, new List<PrefixNode>());

        bool Walk(PrefixNode node, List<PrefixNode> ancestors)
        {
            if (node.FilePath != null &&
                string.Equals(node.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var a in ancestors) a.IsExpanded = true;
                return true;
            }
            ancestors.Add(node);
            try
            {
                foreach (var child in node.Children.Values)
                    if (Walk(child, ancestors)) return true;
                return false;
            }
            finally { ancestors.RemoveAt(ancestors.Count - 1); }
        }
    }

    // ---------------- WebView2 + render plumbing ----------------

    private bool _initialNavSeen;
    private string? _initialNavUri;

    private async System.Threading.Tasks.Task InitWebViewAsync()
    {
        try
        {
            var userDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "noteaerator", "WebView2");
            Directory.CreateDirectory(userDataDir);
            var env = await CoreWebView2Environment.CreateAsync(null, userDataDir, null);
            await _webView.EnsureCoreWebView2Async(env);
            _webView.CoreWebView2.WebMessageReceived += OnWebMessage;

            // Intercept any navigation away from our viewer page (links, redirects,
            // form posts, anything) and shell-launch it externally so links open
            // in the user's default browser instead of replacing the rendered MD.
            _webView.CoreWebView2.NavigationStarting += (_, e) =>
            {
                var uri = e.Uri ?? "";
                if (!_initialNavSeen)
                {
                    _initialNavSeen = true;
                    _initialNavUri = uri;
                    return;
                }
                // Allow same-document hash / fragment navigation.
                var hash = uri.IndexOf('#');
                var withoutHash = hash >= 0 ? uri.Substring(0, hash) : uri;
                var baseInitial = (_initialNavUri ?? "");
                var iHash = baseInitial.IndexOf('#');
                if (iHash >= 0) baseInitial = baseInitial.Substring(0, iHash);
                if (string.Equals(withoutHash, baseInitial, StringComparison.OrdinalIgnoreCase))
                    return;
                e.Cancel = true;
                LaunchExternal(uri);
            };

            // window.open / target="_blank" / Ctrl+click → also shell-launch.
            _webView.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                LaunchExternal(e.Uri);
            };

            var htmlPath = Path.Combine(AppContext.BaseDirectory, "Assets", "viewer.html");
            if (File.Exists(htmlPath))
            {
                _webView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
                _webView.CoreWebView2.NavigationCompleted += (_, e) =>
                {
                    if (e.IsSuccess)
                    {
                        _webViewReady = true;
                        if (_pending is { } p)
                        {
                            _pending = null;
                            _ = PushAsync(p.md, p.commentsJson);
                        }
                    }
                };
            }
            else
            {
                _setStatus($"viewer.html not found at {htmlPath}", null);
            }
        }
        catch (Exception ex)
        {
            _setStatus($"WebView2 init failed: {ex.Message}", null);
        }
    }

    private void LaunchExternal(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return;

        // file:// URLs go through Explorer (Reveal-in-Folder), never through
        // ShellExecute on the file itself. See ExternalLink.cs + Option A in
        // POC/file-link-rendering-options.md.
        var plan = ExternalLink.Classify(uri);
        try
        {
            switch (plan.Kind)
            {
                case ExternalLinkKind.FileFolder:
                case ExternalLinkKind.FileItem:
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = plan.Target,         // explorer.exe
                        Arguments = plan.Arguments!,    // "<path>" or "/select,<path>"
                        UseShellExecute = false
                    });
                    return;

                case ExternalLinkKind.FileRejected:
                    _setStatus($"Refused to open {uri} (unsupported file URL)", uri);
                    return;

                case ExternalLinkKind.Default:
                default:
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = plan.Target,
                        UseShellExecute = true
                    });
                    return;
            }
        }
        catch (Exception ex)
        {
            _setStatus($"Failed to open {uri}: {ex.Message}", uri);
        }
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var action = root.GetProperty("action").GetString();
            if (_currentFile == null) return;

            switch (action)
            {
                case "addComment":
                {
                    var anchor = root.GetProperty("anchor");
                    var entry = new CommentEntry
                    {
                        Body = root.GetProperty("body").GetString() ?? "",
                        Anchor = new CommentAnchor
                        {
                            HeadingSlug = TryGetString(anchor, "headingSlug"),
                            BlockIndex = anchor.TryGetProperty("blockIndex", out var bi)
                                         && bi.ValueKind == JsonValueKind.Number ? bi.GetInt32() : 0,
                            SubPath = TryGetString(anchor, "subPath"),
                            TextQuote = TryGetString(anchor, "textQuote")
                        }
                    };
                    _suppressSidecarUntil = DateTime.UtcNow.AddMilliseconds(500);
                    CommentStore.AddComment(_currentFile, entry);
                    _ = LoadCurrentAsync();
                    break;
                }
                case "deleteComment":
                {
                    var id = root.GetProperty("id").GetString();
                    if (id != null)
                    {
                        _suppressSidecarUntil = DateTime.UtcNow.AddMilliseconds(500);
                        CommentStore.DeleteComment(_currentFile, id);
                        _ = LoadCurrentAsync();
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _setStatus($"comment action failed: {ex.Message}", null);
        }
    }

    private static string? TryGetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private async System.Threading.Tasks.Task LoadCurrentAsync()
    {
        if (_currentFile == null) return;
        string md;
        DateTime lastWrite;
        try
        {
            using var fs = new FileStream(_currentFile, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs);
            md = await sr.ReadToEndAsync();
            lastWrite = File.GetLastWriteTime(_currentFile);
        }
        catch (Exception ex)
        {
            _setStatus($"Read failed: {ex.Message}", null);
            return;
        }

        var commentFile = CommentStore.Load(_currentFile);
        var commentsJson = JsonSerializer.Serialize(commentFile);

        var commentCount = commentFile.Comments.Count;
        var fileName = Path.GetFileName(_currentFile);
        var rel = TimeFormat.Relative(lastWrite);
        var abs = lastWrite.ToString("yyyy-MM-dd HH:mm:ss");
        var commentsBlurb = commentCount > 0
            ? $"  ·  {commentCount} comment{(commentCount == 1 ? "" : "s")}"
            : "";
        var statusText = $"{fileName}  ·  modified {rel} ({abs}){commentsBlurb}";
        _setStatus(statusText, _currentFile);

        if (!_webViewReady)
        {
            _pending = (md, commentsJson);
            return;
        }
        await PushAsync(md, commentsJson);
    }

    private static string FormatRelative(DateTime when)
    {
        // Moved to Noteaerator.Core.TimeFormat.Relative; kept here as a redirect.
        return TimeFormat.Relative(when);
    }

    private async System.Threading.Tasks.Task PushAsync(string md, string commentsJson)
    {
        var mdLit = JsonSerializer.Serialize(md);
        var cmtLit = JsonSerializer.Serialize(commentsJson);
        // Pass the current file path so the renderer can preserve scroll for
        // same-file re-renders and reset to top when the user switches files.
        var pathLit = JsonSerializer.Serialize(_currentFile ?? "");
        // Pass the directory of the current file as a file:// base URI so the
        // renderer can resolve relative image references (e.g. ![](foo.svg)).
        var baseUri = "";
        if (!string.IsNullOrEmpty(_currentFile))
        {
            var dir = Path.GetDirectoryName(_currentFile);
            if (!string.IsNullOrEmpty(dir))
            {
                var withSep = dir.EndsWith(Path.DirectorySeparatorChar)
                    ? dir : dir + Path.DirectorySeparatorChar;
                try { baseUri = new Uri(withSep).AbsoluteUri; } catch { }
            }
        }
        var baseLit = JsonSerializer.Serialize(baseUri);
        var script =
            $"window.renderMarkdown && window.renderMarkdown({mdLit}, JSON.parse({cmtLit}), {pathLit}, {baseLit});";
        try { await _webView.CoreWebView2.ExecuteScriptAsync(script); }
        catch (Exception ex) { _setStatus($"Render failed: {ex.Message}", null); return; }

        // If a search-result jump is pending, ask the renderer to scroll to it.
        if (_pendingScroll is { } hit)
        {
            _pendingScroll = null;
            try
            {
                if (hit.IsComment && !string.IsNullOrEmpty(hit.CommentId))
                {
                    var idLit = JsonSerializer.Serialize(hit.CommentId);
                    await _webView.CoreWebView2.ExecuteScriptAsync(
                        $"window.scrollToCommentId && window.scrollToCommentId({idLit});");
                }
                else if (!string.IsNullOrEmpty(hit.Term))
                {
                    var termLit = JsonSerializer.Serialize(hit.Term);
                    await _webView.CoreWebView2.ExecuteScriptAsync(
                        $"window.scrollToText && window.scrollToText({termLit});");
                }
            }
            catch { /* best-effort */ }
        }
    }

    // ---------------- Watcher event handlers ----------------

    private void OnMdChanged(object sender, FileSystemEventArgs e) => HandleMdEvent(e.FullPath);
    private void OnMdRenamed(object sender, RenamedEventArgs e)    => HandleMdEvent(e.FullPath);

    private bool IsRelevantPath(string fullPath)
    {
        // Top-level *.md or archive/*.md only — ignore anything deeper.
        var dir = Path.GetDirectoryName(fullPath);
        if (dir == null) return false;
        if (Eq(dir, FolderPath)) return true;
        if (Eq(dir, ArchiveDir)) return true;
        return false;
    }

    private void HandleMdEvent(string changedPath)
    {
        if (!IsRelevantPath(changedPath)) return;
        _ui.Post(_ =>
        {
            _pendingMdChanges.Add(changedPath);
            if (_mdEventDebounce == null)
            {
                _mdEventDebounce = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(180)
                };
                _mdEventDebounce.Tick += (_, _) =>
                {
                    _mdEventDebounce!.Stop();
                    var batch = _pendingMdChanges.ToArray();
                    _pendingMdChanges.Clear();
                    PopulateFiles();
                    if (_currentFile != null &&
                        batch.Any(p => Eq(p, _currentFile)))
                    {
                        _ = LoadCurrentAsync();
                    }
                };
            }
            _mdEventDebounce.Stop();
            _mdEventDebounce.Start();
        }, null);
    }

    private void OnSidecarChanged(object sender, FileSystemEventArgs e)
    {
        if (DateTime.UtcNow < _suppressSidecarUntil) return;
        if (!IsRelevantPath(e.FullPath)) return;
        _ui.Post(_ =>
        {
            if (_currentFile != null)
            {
                var expected = CommentStore.SidecarPath(_currentFile);
                if (Eq(expected, e.FullPath)) _ = LoadCurrentAsync();
            }
        }, null);
    }

    public void Dispose()
    {
        try { _mdWatcher.EnableRaisingEvents = false; _mdWatcher.Dispose(); } catch { }
        try { _commentsWatcher.EnableRaisingEvents = false; _commentsWatcher.Dispose(); } catch { }
        try { if (_archiveMdWatcher != null) { _archiveMdWatcher.EnableRaisingEvents = false; _archiveMdWatcher.Dispose(); } } catch { }
        try { if (_archiveCommentsWatcher != null) { _archiveCommentsWatcher.EnableRaisingEvents = false; _archiveCommentsWatcher.Dispose(); } } catch { }
        try { _webView.Dispose(); } catch { }
    }
}

// =====================================================================================
// (FileEntry removed — the file list is now driven by FileListRow from
// Noteaerator.Core which carries both file rows and synthetic-folder rows.)
// =====================================================================================
