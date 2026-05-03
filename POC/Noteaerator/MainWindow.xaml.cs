using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;

namespace Noteaerator;

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
            if (File.Exists(_configPath))
            {
                var paths = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_configPath)) ?? new();
                foreach (var p in paths.Where(Directory.Exists))
                    AddProject(p);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to load projects: {ex.Message}", null);
        }

        if (ProjectsTabs.Items.Count > 0)
            ProjectsTabs.SelectedIndex = 0;
    }

    private void SaveProjects()
    {
        try
        {
            var paths = _projects.Select(p => p.FolderPath).ToList();
            File.WriteAllText(_configPath, JsonSerializer.Serialize(paths));
        }
        catch { /* best-effort */ }
    }

    private void OnAddProject(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Pick a project folder containing .md files" };
        if (dlg.ShowDialog() == true)
        {
            AddProject(dlg.FolderName);
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
    private void OnOpenSearchExecuted(object sender, ExecutedRoutedEventArgs e) => OpenSearch();
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

    private void AddProject(string folderPath)
    {
        if (_projects.Any(p => string.Equals(p.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase)))
            return;

        var pt = new ProjectTab(folderPath, (text, tip) =>
            Dispatcher.Invoke(() => SetStatus(text, tip)));
        _projects.Add(pt);

        var headerText = new TextBlock { Text = Path.GetFileName(folderPath.TrimEnd('\\', '/')) };

        var tabItem = new TabItem
        {
            Header = headerText,
            ToolTip = folderPath,
            Content = pt.Root,
            Tag = pt
        };

        // Right-click on a project tab → Remove project
        var menu = new ContextMenu();
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

// =====================================================================================
// Sidecar comment data model
// =====================================================================================

internal sealed class CommentAnchor
{
    [JsonPropertyName("headingSlug")] public string? HeadingSlug { get; set; }
    [JsonPropertyName("blockIndex")]  public int BlockIndex { get; set; }
    [JsonPropertyName("subPath")]     public string? SubPath { get; set; }
    [JsonPropertyName("textQuote")]   public string? TextQuote { get; set; }
}

internal sealed class CommentEntry
{
    [JsonPropertyName("id")]        public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [JsonPropertyName("createdAt")] public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
    [JsonPropertyName("anchor")]    public CommentAnchor Anchor { get; set; } = new();
    [JsonPropertyName("body")]      public string Body { get; set; } = "";
}

internal sealed class CommentFile
{
    [JsonPropertyName("_purpose")]
    public string Purpose { get; set; } =
        "Human comments on the sibling .md file, written by the Note Aerator viewer. " +
        "Agents are expected to read these, act on them, and DELETE this file when done. " +
        "Removing all entries also auto-deletes this file.";

    [JsonPropertyName("version")]  public int Version { get; set; } = 1;
    [JsonPropertyName("comments")] public List<CommentEntry> Comments { get; set; } = new();
}

internal static class CommentStore
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string SidecarPath(string mdPath)
    {
        var dir = Path.GetDirectoryName(mdPath) ?? "";
        var baseName = Path.GetFileNameWithoutExtension(mdPath);
        return Path.Combine(dir, baseName + "-comments.json");
    }

    public static CommentFile Load(string mdPath)
    {
        var path = SidecarPath(mdPath);
        if (!File.Exists(path)) return new CommentFile();
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize<CommentFile>(fs) ?? new CommentFile();
        }
        catch
        {
            return new CommentFile();
        }
    }

    public static void Save(string mdPath, CommentFile data)
    {
        var path = SidecarPath(mdPath);
        if (data.Comments.Count == 0)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
            return;
        }
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, Opts));
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else                   File.Move(tmp, path);
    }

    public static void AddComment(string mdPath, CommentEntry entry)
    {
        var data = Load(mdPath);
        data.Comments.Add(entry);
        Save(mdPath, data);
    }

    public static void DeleteComment(string mdPath, string id)
    {
        var data = Load(mdPath);
        data.Comments.RemoveAll(c => c.Id == id);
        Save(mdPath, data);
    }
}

// =====================================================================================
// Search engine — naive scan over .md files and their sidecar comments.
// =====================================================================================

internal sealed class SearchHit
{
    public string FilePath { get; init; } = "";
    public string FileName => System.IO.Path.GetFileName(FilePath);
    public bool IsComment { get; init; }
    public int LineNumber { get; init; }    // 1-based; 0 if not applicable
    public string? CommentId { get; init; } // for comment hits
    public string Snippet { get; init; } = "";
    public string Term { get; init; } = "";

    // Display strings used by the simple ListBox template (defined inline in code-behind).
    public string Display1 => IsComment
        ? $"💬  {FileName}  ·  comment"
        : $"{FileName}  ·  line {LineNumber}";
    public string Display2 => Snippet;
}

internal static class SearchEngine
{
    private const int MaxHits = 200;

    /// <summary>
    /// Either provide <paramref name="folderPath"/> for project-wide scan, or
    /// <paramref name="singleFile"/> to limit to one file. Exactly one should
    /// be non-null.
    /// </summary>
    public static List<SearchHit> Search(string query, string? folderPath, string? singleFile)
    {
        var results = new List<SearchHit>();
        if (string.IsNullOrWhiteSpace(query)) return results;
        var cmp = StringComparison.OrdinalIgnoreCase;

        IEnumerable<string> files;
        if (singleFile != null)
        {
            files = new[] { singleFile };
        }
        else if (folderPath != null)
        {
            files = EnumerateProjectMd(folderPath);
        }
        else
        {
            return results;
        }

        foreach (var file in files)
        {
            if (results.Count >= MaxHits) break;
            ScanFile(file, query, cmp, results);
            if (results.Count >= MaxHits) break;
            ScanSidecar(file, query, cmp, results);
        }
        return results;
    }

    private static IEnumerable<string> EnumerateProjectMd(string folderPath)
    {
        IEnumerable<string> SafeEnum(string dir)
        {
            try   { return Directory.EnumerateFiles(dir, "*.md", SearchOption.TopDirectoryOnly); }
            catch { return Array.Empty<string>(); }
        }
        foreach (var f in SafeEnum(folderPath).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            yield return f;
        var arch = Path.Combine(folderPath, "archive");
        if (Directory.Exists(arch))
            foreach (var f in SafeEnum(arch).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                yield return f;
    }

    private static void ScanFile(string path, string query, StringComparison cmp, List<SearchHit> results)
    {
        string[] lines;
        try { lines = File.ReadAllLines(path); }
        catch { return; }
        for (int i = 0; i < lines.Length; i++)
        {
            if (results.Count >= MaxHits) return;
            if (lines[i].IndexOf(query, cmp) >= 0)
            {
                results.Add(new SearchHit
                {
                    FilePath = path,
                    LineNumber = i + 1,
                    Snippet = MakeSnippet(lines[i], query, cmp),
                    Term = query
                });
            }
        }
    }

    private static void ScanSidecar(string mdPath, string query, StringComparison cmp, List<SearchHit> results)
    {
        var data = CommentStore.Load(mdPath);
        foreach (var c in data.Comments)
        {
            if (results.Count >= MaxHits) return;
            if (!string.IsNullOrEmpty(c.Body) && c.Body.IndexOf(query, cmp) >= 0)
            {
                results.Add(new SearchHit
                {
                    FilePath = mdPath,
                    IsComment = true,
                    CommentId = c.Id,
                    Snippet = MakeSnippet(c.Body, query, cmp),
                    Term = query
                });
            }
        }
    }

    private static string MakeSnippet(string line, string query, StringComparison cmp)
    {
        const int radius = 60;
        var idx = line.IndexOf(query, cmp);
        if (idx < 0) return line.Length > 120 ? line.Substring(0, 120) + "…" : line;
        var start = Math.Max(0, idx - radius);
        var end = Math.Min(line.Length, idx + query.Length + radius);
        var prefix = start > 0 ? "…" : "";
        var suffix = end < line.Length ? "…" : "";
        return prefix + line.Substring(start, end - start).Replace("\t", "  ") + suffix;
    }
}

internal sealed class ProjectTab : IDisposable
{
    private const string ArchiveSubdir = "archive";

    public string FolderPath { get; }
    public Grid Root { get; }
    public string? CurrentFile => _currentFile;

    private readonly ListBox _activeList;
    private readonly ListBox _archivedList;
    private readonly Expander _archiveExpander;
    private readonly TextBlock _archiveHeaderText;
    private readonly ObservableCollection<FileEntry> _activeFiles = new();
    private readonly ObservableCollection<FileEntry> _archivedFiles = new();

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

        _activeList = new ListBox { ItemsSource = _activeFiles };
        if (Application.Current?.TryFindResource("FileListBoxStyle") is Style listStyle)
            _activeList.Style = listStyle;
        _activeList.ItemTemplate = BuildFileItemTemplate();
        _activeList.SelectionChanged += OnActiveSelected;
        _activeList.PreviewMouseRightButtonDown += (s, e) => OnFileRightClick(s, e, isArchived: false);

        _archivedList = new ListBox { ItemsSource = _archivedFiles };
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

        // Watchers — recursive so we catch the archive subdir too.
        _mdWatcher = new FileSystemWatcher(folderPath, "*.md")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        _mdWatcher.Changed += OnMdChanged;
        _mdWatcher.Created += OnMdChanged;
        _mdWatcher.Deleted += OnMdChanged;
        _mdWatcher.Renamed += OnMdRenamed;

        _commentsWatcher = new FileSystemWatcher(folderPath, "*-comments.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        _commentsWatcher.Changed += OnSidecarChanged;
        _commentsWatcher.Created += OnSidecarChanged;
        _commentsWatcher.Deleted += OnSidecarChanged;
        _commentsWatcher.Renamed += (_, e) => OnSidecarChanged(this, e);

        PopulateFiles();
    }

    private DataTemplate BuildFileItemTemplate()
    {
        // Simple: a TextBlock with the file's display name, ellipsis-trimmed,
        // tooltip = full path. Right-click context menu is wired on the
        // ListBox via PreviewMouseRightButtonDown so the menu can be built
        // freshly per-click without DataTemplate sharing pitfalls.
        var template = new DataTemplate(typeof(FileEntry));
        var tb = new FrameworkElementFactory(typeof(TextBlock));
        tb.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(FileEntry.Display)));
        tb.SetBinding(FrameworkElement.ToolTipProperty, new System.Windows.Data.Binding(nameof(FileEntry.Path)));
        tb.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        template.VisualTree = tb;
        template.Seal();
        return template;
    }

    private void OnFileRightClick(object sender, MouseButtonEventArgs e, bool isArchived)
    {
        if (sender is not ListBox lb) return;
        // Find ListBoxItem under the cursor.
        var dep = e.OriginalSource as DependencyObject;
        while (dep != null && dep is not ListBoxItem) dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
        if (dep is not ListBoxItem lbi || lbi.DataContext is not FileEntry fe) return;

        // Select it so it's clear what the menu acts on.
        lbi.IsSelected = true;

        var menu = new ContextMenu();
        var item = new MenuItem
        {
            Header = isArchived ? "Restore from Archive" : "Move to Archive…"
        };
        var path = fe.Path;
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

        var active = SafeEnum(FolderPath).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        var archived = Directory.Exists(ArchiveDir)
            ? SafeEnum(ArchiveDir).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();

        _suppressSelChange = true;
        try
        {
            _activeFiles.Clear();
            foreach (var f in active) _activeFiles.Add(new FileEntry(f));
            _archivedFiles.Clear();
            foreach (var f in archived) _archivedFiles.Add(new FileEntry(f));
        }
        finally { _suppressSelChange = false; }

        _archiveHeaderText.Text = archived.Count > 0 ? $"Archive  ({archived.Count})" : "Archive";

        if (_activeFiles.Count == 0 && _archivedFiles.Count == 0)
        {
            _setStatus($"No .md files in {FolderPath}", FolderPath);
            return;
        }

        // Restore previous selection if possible.
        FileEntry? toSelect = null;
        bool inArchive = false;
        if (prevFile != null)
        {
            toSelect = _activeFiles.FirstOrDefault(fe => Eq(fe.Path, prevFile));
            if (toSelect == null)
            {
                toSelect = _archivedFiles.FirstOrDefault(fe => Eq(fe.Path, prevFile));
                if (toSelect != null) inArchive = true;
            }
        }
        toSelect ??= _activeFiles.FirstOrDefault();

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
            _currentFile = toSelect.Path;
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
        if (_activeList.SelectedItem is FileEntry fe)
        {
            _suppressSelChange = true;
            try { _archivedList.SelectedItem = null; }
            finally { _suppressSelChange = false; }
            _currentFile = fe.Path;
            _ = LoadCurrentAsync();
        }
    }

    private void OnArchivedSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelChange) return;
        if (_archivedList.SelectedItem is FileEntry fe)
        {
            _suppressSelChange = true;
            try { _activeList.SelectedItem = null; }
            finally { _suppressSelChange = false; }
            _currentFile = fe.Path;
            _ = LoadCurrentAsync();
        }
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
        var fe = _activeFiles.FirstOrDefault(f => Eq(f.Path, hit.FilePath))
              ?? _archivedFiles.FirstOrDefault(f => Eq(f.Path, hit.FilePath));
        if (fe == null) return;

        var inArchive = _archivedFiles.Contains(fe);
        if (inArchive) _archiveExpander.IsExpanded = true;

        _suppressSelChange = true;
        try
        {
            if (inArchive) { _archivedList.SelectedItem = fe; _activeList.SelectedItem = null; }
            else           { _activeList.SelectedItem   = fe; _archivedList.SelectedItem = null; }
        }
        finally { _suppressSelChange = false; }

        _currentFile = fe.Path;
        _pendingScroll = hit;
        _ = LoadCurrentAsync();
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
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true
            });
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
        var rel = FormatRelative(lastWrite);
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
        var diff = DateTime.Now - when;
        if (diff.TotalSeconds < 60)  return "just now";
        if (diff.TotalMinutes < 60)  return $"{(int)diff.TotalMinutes} minute{((int)diff.TotalMinutes == 1 ? "" : "s")} ago";
        if (diff.TotalHours   < 24)  return $"{(int)diff.TotalHours} hour{((int)diff.TotalHours == 1 ? "" : "s")} ago";
        if (diff.TotalDays    < 2)   return "yesterday";
        if (diff.TotalDays    < 30)  return $"{(int)diff.TotalDays} days ago";
        if (diff.TotalDays    < 365) return $"{(int)(diff.TotalDays / 30)} months ago";
        return $"{(int)(diff.TotalDays / 365)} years ago";
    }

    private async System.Threading.Tasks.Task PushAsync(string md, string commentsJson)
    {
        var mdLit = JsonSerializer.Serialize(md);
        var cmtLit = JsonSerializer.Serialize(commentsJson);
        var script =
            $"window.renderMarkdown && window.renderMarkdown({mdLit}, JSON.parse({cmtLit}));";
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
            PopulateFiles();
            if (_currentFile != null && Eq(_currentFile, changedPath))
                _ = LoadCurrentAsync();
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
        try { _webView.Dispose(); } catch { }
    }
}

// =====================================================================================
// Tiny DTO bound to ListBox items.
// =====================================================================================
internal sealed class FileEntry
{
    public string Path { get; }
    public string Display { get; }

    public FileEntry(string path)
    {
        Path = path;
        Display = System.IO.Path.GetFileName(path);
    }
    public override string ToString() => Display;
}
