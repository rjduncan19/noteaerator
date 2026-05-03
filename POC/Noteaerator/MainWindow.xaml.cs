using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;

namespace Noteaerator;

public partial class MainWindow : Window
{
    private readonly List<ProjectTab> _projects = new();
    private readonly string _configPath;

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
            StatusText.Text = $"Failed to load projects: {ex.Message}";
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

    private void OnRemoveProject(object sender, RoutedEventArgs e)
    {
        if (ProjectsTabs.SelectedItem is TabItem ti && ti.Tag is ProjectTab pt)
        {
            pt.Dispose();
            _projects.Remove(pt);
            ProjectsTabs.Items.Remove(ti);
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
            StatusText.Text = pt.FolderPath;
    }

    private void AddProject(string folderPath)
    {
        if (_projects.Any(p => string.Equals(p.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase)))
            return;

        var pt = new ProjectTab(folderPath, msg => Dispatcher.Invoke(() => StatusText.Text = msg));
        _projects.Add(pt);

        var tabItem = new TabItem
        {
            Header = new TextBlock { Text = Path.GetFileName(folderPath.TrimEnd('\\', '/')) },
            ToolTip = folderPath,
            Content = pt.Root,
            Tag = pt
        };
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
        "Human comments on the sibling .md file, written by the noteaerator viewer. " +
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
            // Empty → delete the sidecar so the project tree stays clean and the
            // "comments processed" lifecycle is symmetric.
            try { if (File.Exists(path)) File.Delete(path); } catch { }
            return;
        }

        // Atomic write: temp + replace, so a crash mid-write doesn't corrupt the file.
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, Opts));
        if (File.Exists(path))
            File.Replace(tmp, path, null);
        else
            File.Move(tmp, path);
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
// One project = one folder. Vertical TabControl of .md files + WebView2 renderer.
// =====================================================================================

internal sealed class ProjectTab : IDisposable
{
    public string FolderPath { get; }
    public Grid Root { get; }

    private readonly TabControl _filesTabs;
    private readonly WebView2 _webView;
    private readonly FileSystemWatcher _mdWatcher;
    private readonly FileSystemWatcher _commentsWatcher;
    private readonly Action<string> _setStatus;
    private readonly SynchronizationContext _ui;
    private bool _webViewReady;
    private (string md, string commentsJson)? _pending;
    private string? _currentFile;

    // We just wrote a sidecar file ourselves; suppress the next watcher event to
    // avoid an immediate redundant re-render flicker.
    private DateTime _suppressSidecarUntil = DateTime.MinValue;

    public ProjectTab(string folderPath, Action<string> setStatus)
    {
        FolderPath = folderPath;
        _setStatus = setStatus;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();

        Root = new Grid();
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _filesTabs = new TabControl { TabStripPlacement = Dock.Left };
        if (System.Windows.Application.Current?.TryFindResource("FileTabControlStyle") is Style fileStyle)
            _filesTabs.Style = fileStyle;
        _filesTabs.SelectionChanged += OnFileSelected;
        Grid.SetColumn(_filesTabs, 0);
        Root.Children.Add(_filesTabs);

        var splitter = new GridSplitter
        {
            Width = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = (System.Windows.Media.Brush?)
                System.Windows.Application.Current?.TryFindResource("BorderBrush")
                ?? System.Windows.Media.Brushes.LightGray
        };
        Grid.SetColumn(splitter, 1);
        Root.Children.Add(splitter);

        _webView = new WebView2();
        Grid.SetColumn(_webView, 2);
        Root.Children.Add(_webView);

        _ = InitWebViewAsync();

        _mdWatcher = new FileSystemWatcher(folderPath, "*.md")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };
        _mdWatcher.Changed += OnMdChanged;
        _mdWatcher.Created += OnMdChanged;
        _mdWatcher.Deleted += OnMdChanged;
        _mdWatcher.Renamed += OnMdRenamed;

        _commentsWatcher = new FileSystemWatcher(folderPath, "*-comments.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };
        _commentsWatcher.Changed += OnSidecarChanged;
        _commentsWatcher.Created += OnSidecarChanged;
        _commentsWatcher.Deleted += OnSidecarChanged;
        _commentsWatcher.Renamed += (_, e) => OnSidecarChanged(this, e);

        PopulateFiles();
    }

    private async System.Threading.Tasks.Task InitWebViewAsync()
    {
        try
        {
            // Put WebView2's user-data folder under %LOCALAPPDATA% rather than next
            // to the exe — required when the app is installed to Program Files
            // (which is read-only for non-admin) and just generally cleaner.
            var userDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "noteaerator", "WebView2");
            Directory.CreateDirectory(userDataDir);
            var env = await CoreWebView2Environment.CreateAsync(null, userDataDir, null);
            await _webView.EnsureCoreWebView2Async(env);
            _webView.CoreWebView2.WebMessageReceived += OnWebMessage;
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
                _setStatus($"viewer.html not found at {htmlPath}");
            }
        }
        catch (Exception ex)
        {
            _setStatus($"WebView2 init failed: {ex.Message}");
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
            _setStatus($"comment action failed: {ex.Message}");
        }
    }

    private static string? TryGetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private void PopulateFiles()
    {
        var prevFile = _currentFile;
        _filesTabs.Items.Clear();
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(FolderPath, "*.md", SearchOption.TopDirectoryOnly)
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _setStatus($"Cannot read {FolderPath}: {ex.Message}");
            return;
        }

        foreach (var f in files)
        {
            var item = new TabItem
            {
                Header = new TextBlock
                {
                    Text = Path.GetFileName(f),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 196
                },
                ToolTip = f,
                Tag = f
            };
            _filesTabs.Items.Add(item);
        }

        if (_filesTabs.Items.Count == 0)
        {
            _setStatus($"No .md files in {FolderPath}");
            return;
        }

        var toSelect = _filesTabs.Items.Cast<TabItem>()
            .FirstOrDefault(ti => string.Equals((string?)ti.Tag, prevFile, StringComparison.OrdinalIgnoreCase))
            ?? (TabItem)_filesTabs.Items[0]!;
        _filesTabs.SelectedItem = toSelect;
    }

    private void OnFileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != _filesTabs) return;
        if (_filesTabs.SelectedItem is TabItem ti && ti.Tag is string path)
        {
            _currentFile = path;
            _ = LoadCurrentAsync();
        }
    }

    public void Refresh()
    {
        PopulateFiles();
        _ = LoadCurrentAsync();
    }

    private async System.Threading.Tasks.Task LoadCurrentAsync()
    {
        if (_currentFile == null) return;
        string md;
        try
        {
            using var fs = new FileStream(_currentFile, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs);
            md = await sr.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _setStatus($"Read failed: {ex.Message}");
            return;
        }

        var commentFile = CommentStore.Load(_currentFile);
        var commentsJson = JsonSerializer.Serialize(commentFile);

        var commentCount = commentFile.Comments.Count;
        _setStatus(commentCount > 0
            ? $"{_currentFile}    ·    {commentCount} comment{(commentCount == 1 ? "" : "s")}"
            : $"{_currentFile}");

        if (!_webViewReady)
        {
            _pending = (md, commentsJson);
            return;
        }
        await PushAsync(md, commentsJson);
    }

    private async System.Threading.Tasks.Task PushAsync(string md, string commentsJson)
    {
        var mdLit = JsonSerializer.Serialize(md);
        // commentsJson is itself JSON; serialize it as a *string* so JS receives a string and parses it.
        var cmtLit = JsonSerializer.Serialize(commentsJson);
        var script =
            $"window.renderMarkdown && window.renderMarkdown({mdLit}, JSON.parse({cmtLit}));";
        try { await _webView.CoreWebView2.ExecuteScriptAsync(script); }
        catch (Exception ex) { _setStatus($"Render failed: {ex.Message}"); }
    }

    private void OnMdChanged(object sender, FileSystemEventArgs e) => HandleMdEvent(e.FullPath);
    private void OnMdRenamed(object sender, RenamedEventArgs e)    => HandleMdEvent(e.FullPath);

    private void HandleMdEvent(string changedPath)
    {
        _ui.Post(_ =>
        {
            PopulateFiles();
            if (_currentFile != null &&
                string.Equals(_currentFile, changedPath, StringComparison.OrdinalIgnoreCase))
            {
                _ = LoadCurrentAsync();
            }
        }, null);
    }

    private void OnSidecarChanged(object sender, FileSystemEventArgs e)
    {
        if (DateTime.UtcNow < _suppressSidecarUntil) return;
        _ui.Post(_ =>
        {
            // If the sidecar that changed corresponds to the currently loaded file, re-render.
            if (_currentFile != null)
            {
                var expected = CommentStore.SidecarPath(_currentFile);
                if (string.Equals(expected, e.FullPath, StringComparison.OrdinalIgnoreCase))
                    _ = LoadCurrentAsync();
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
