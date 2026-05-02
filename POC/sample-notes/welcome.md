# Sample project — viewer smoke test

This file exists so you can point the viewer at the `POC/sample-notes/`
folder and confirm everything renders. Edit the file, save it, and the
viewer should auto-refresh thanks to `FileSystemWatcher`.

## Headings, lists, and code

- bullet one
- bullet two with **bold** and _italic_ and `inline code`
- [ ] a task
- [x] a completed task

```python
def hello(name: str) -> str:
    return f"hello, {name}"
```

## A mermaid diagram

```mermaid
flowchart LR
  CLI[Copilot CLI] --> MD[Markdown files]
  MD --> App[noteaerator]
  App -->|file change| FSW[FileSystemWatcher]
  FSW --> App
```

## Human-to-AI comments

The line below is an `@ai:` comment. The viewer renders it as a yellow
sticky note so it's obviously a human request to the AI:

<!-- @ai: please add a section here describing the file watcher debouncing strategy -->

And a resolved one:

<!-- @ai-done: standardized the project list storage path under %APPDATA%\noteaerator\viewer -->

## A table

| Component | Owner |
|---|---|
| Shell | C# / WPF |
| Renderer | markdown-it + mermaid |
| File IO | C# `FileSystemWatcher` |
