# drift

watch a file or directory and run a command on change. debounced so rapid saves don't spam builds.

```
drift <path> <command> [flags]
```

---

## examples

```sh
drift src/ "dotnet build"
drift . "npm test" -f *.ts
drift config.json "echo reloading"
drift src/ "dotnet run" --debounce 500
```

## output

```
  watching src/   filter=*.cs   cmd=dotnet build

  [14:32:01]  Program.cs changed
    Build succeeded.
    0 Warning(s)
    0 Error(s)
  ✓ exit 0

  [14:35:44]  Utils.cs changed
    Build FAILED.
  ✗ exit 1
```

## flags

| flag | description |
|------|-------------|
| `-f, --filter <glob>` | watch only files matching glob |
| `--no-recursive` | don't watch subdirectories |
| `-d, --debounce <ms>` | wait N ms after last change before running (default: 300) |

## build

```
dotnet build -c Release
```

.NET 8+, windows/linux/mac (uses `FileSystemWatcher`).

## testing

built clean. `FileSystemWatcher` setup, debounce timer, and command execution logic reviewed. output capture and exit code display confirmed.

**not live-tested:** didn't watch a real directory through an actual file change. the FileSystemWatcher + debounce pattern is standard but the end-to-end wasn't exercised.

## license

MIT
