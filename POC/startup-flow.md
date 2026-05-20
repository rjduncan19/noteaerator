# App startup flow

How Note Aerator gets from a double-click on the Start menu tile to a
rendered Markdown document on screen.

```mermaid
flowchart TD
    Launch([User launches Note Aerator]):::user --> App[App.xaml.cs OnStartup]
    App --> WV2{WebView2<br/>runtime<br/>installed?}
    WV2 -- no --> Prompt[Show one-click<br/>download prompt]:::warn
    Prompt --> Exit1([Exit]):::user
    WV2 -- yes --> Main[MainWindow ctor]

    Main --> LoadState[Read<br/>%APPDATA%\noteaerator\<br/>viewer\projects.json]
    LoadState --> Tabs[Build project tab strip]
    Tabs --> Active{Any saved<br/>active tab?}
    Active -- no --> Empty[Show 'Add project folder...' hint]:::neutral
    Active -- yes --> Open[Open active project]

    Open --> Enum[Enumerate *.md<br/>at project root<br/>+ archive/ subdir]
    Enum --> Watch[Start two<br/>FileSystemWatchers<br/>64 KB buffer, non-recursive]:::infra
    Watch --> List[Populate vertical file list]
    List --> Pick[Select last-viewed file<br/>or first file]
    Pick --> Render

    subgraph Render[Render pipeline]
        direction TB
        Read[Read .md with<br/>FileMode.Open + FileAccess.Read]:::safe
        Sidecar[Load sibling<br/>basename-comments.json<br/>if present]
        Push[PushAsync to WebView2<br/>with file:// baseUri]
        JS[viewer.html:<br/>markdown-it + highlight.js<br/>+ mermaid + KaTeX]
        Anchor[Restore scroll position<br/>+ inject @ai callouts<br/>+ inject sidecar comments]
        Read --> Sidecar --> Push --> JS --> Anchor
    end

    Anchor --> Ready([Viewer ready]):::user

    classDef user fill:#1e293b,stroke:#1e293b,color:#fff
    classDef warn fill:#fde68a,stroke:#b45309,color:#7c2d12
    classDef neutral fill:#f1f5f9,stroke:#94a3b8,color:#1e293b
    classDef infra fill:#dbeafe,stroke:#1d4ed8,color:#1e3a8a
    classDef safe fill:#dcfce7,stroke:#15803d,color:#14532d
```

## What each step is doing

1. **WebView2 check.** Note Aerator is a thin WPF shell around a
   WebView2 control. If the runtime is missing (rare on Windows 11,
   occasional on older Windows 10 installs), the app surfaces a single
   "Install WebView2" button instead of crashing.

2. **Project state.** The list of folders you've added — and which one
   was active last — lives in `%APPDATA%\noteaerator\viewer\projects.json`.
   That file is the only thing the app writes outside the folders you
   point it at. Your `.md` files are never modified.

3. **Two watchers, not one.** Each project gets two non-recursive
   `FileSystemWatcher`s (one on the root, one on the optional
   `archive/` subdir) with a 64 KB internal buffer. The deliberate
   non-recursion keeps the watcher reliable on top of OneDrive,
   Dropbox, and Google Drive folders, where deep sync churn can
   otherwise overflow the buffer and silently drop events for
   top-level changes.

4. **Read-only Markdown access.** Files are opened with
   `FileMode.Open` + `FileAccess.Read` — the viewer cannot accidentally
   write to a Markdown file even if you tried to make it.

5. **Render pipeline.** The Markdown text plus the base URI of the
   source file is pushed into `viewer.html`. The renderer there is a
   standard `markdown-it` + `highlight.js` + `mermaid` + `KaTeX`
   stack, with two small additions: a CSS callout for
   `<!-- @ai: ... -->` and `<!-- @ai-done: ... -->` markers, and an
   overlay for any human comments in the sidecar JSON.

6. **State restoration.** Scroll position is preserved across reloads
   so editing a long note in your favorite editor doesn't bounce you
   back to the top every time you save.

```mermaid
sequenceDiagram
    autonumber
    actor U as User
    participant App as App.xaml.cs
    participant Main as MainWindow
    participant FS as FileSystemWatcher
    participant WV as WebView2
    participant JS as viewer.html

    U->>App: Launch
    App->>App: Probe WebView2 runtime
    App->>Main: Show window
    Main->>Main: Load projects.json
    Main->>FS: Watch root + archive/<br/>(non-recursive, 64 KB buffer)
    Main->>WV: NavigateToString(viewer.html)
    Main->>JS: PushAsync(markdown, baseUri)
    JS->>JS: Render MD + sidecar comments<br/>+ @ai callouts
    JS-->>U: Document ready
    Note over FS,JS: ...later, file changes on disk...
    FS->>Main: Created / Changed
    Main->>Main: Debounce 180 ms
    Main->>JS: PushAsync(new markdown)
    JS-->>U: Reload, preserve scroll
```

That's it — there are no background services, no telemetry round-trips,
and no network calls beyond the CDN-hosted rendering libraries pulled
into the WebView2 sandbox at first paint.
