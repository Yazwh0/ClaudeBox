# claudebox (`cb`): plan

A C# command-line wrapper that launches Claude Code sessions, remembers each one's name, folder and session ID, and restores them as Windows Terminal tabs after a reboot. Windows Terminal does all rendering; `cb` never touches screen output.

## Environment

- .NET 10 SDK (10.0.401)
- Claude Code native install: `C:\Users\benju\.local\bin\claude.exe`
- Windows Terminal: `wt.exe` on PATH

## Commands

```
cb new <name> [--dir <path>] [--work <type>] [--colour <c>] [--here] [-- <claude args>]
                                                   start a new named session in a new tab
                                                   of this window (--here: run in this tab)
cb resume <name|#> [--here]                        resume a saved session in a new tab
cb set <name|#> [--work <type>] [--colour <c>]     change a session's work type or colour
cb work ls|add|rm <type> [--colour <c>] [--scheme <name>]
                                                   manage work types (see config.json)
cb ls [--all]                                      list sessions: name, folder, status, last active
cb restore [--all] [--window new|current]         reopen sessions as Windows Terminal tabs
cb rename <name|#> <new name>
cb rm <name|#> [--forget-transcript]              remove from registry (transcript kept by default)
cb adopt <session-id> [--name <name>]             register a session started outside cb
cb startup enable|disable                          run `cb restore` at login
cb hook <event>                                    internal: called by Claude Code hooks
```

Sessions can be addressed by exact name, unique prefix or list index.

### Name clashes on `cb new`

If a session with that name already exists, `cb new` asks what to do:

```
A session called 'foo' already exists (exited, last active 2026-10-24 13:10, C:\src\app).
  [R] Resume it
  [D] Delete it and start a new one
  [N] Start a new one with a different name
  [C] Cancel
```

- Resume behaves exactly like `cb resume foo` (refuses if it is already running; offers to switch to `[N]`).
- Delete removes the registry entry only (the Claude transcript is kept, as with `cb rm`).
- Different name prompts for a name, suggesting `foo 2`, and re-checks for clashes.
- Non-interactive (input redirected): fail with the current message so scripts don't hang.
  Flags `--resume-existing` / `--replace` allow choosing up front.
- cmd passes single quotes literally (`cb new 'foo'` gives the name `'foo'`), so strip one
  pair of surrounding single quotes from names.

## How it works

### Launching

- `cb new`: generate a GUID, then run `claude --session-id <guid> --settings <hooks.json> [args]` in the target folder.
- `cb resume`: run `claude --resume <sessionId> --settings <hooks.json> [saved args]` in the saved folder.
- New-tab mode (the default for every command that launches Claude; `--here` opts out): `cb` saves the record first, then runs
  `wt -w 0 new-tab --title "<name>" --suppressApplicationTitle --tabColor <hex> [--colorScheme <scheme>] -d <cwd> cb run <id>` and exits.
  `-w 0` targets the current Windows Terminal window. The new tab runs `cb run <id>`
  (an internal command that does the real launch), so the tab is tracked like any other.
  If `WT_SESSION` isn't set (not inside Windows Terminal), fall back to running in place.
- Start `claude.exe` with `UseShellExecute = false` and no redirection, so it inherits the console directly (pure passthrough).
- `Console.CancelKeyPress` sets `e.Cancel = true` in `cb`, so Ctrl+C goes to Claude only.
- `cb` waits for exit and returns Claude's exit code.
- Environment passed to Claude:
  - `CLAUDEBOX_ID=<record id>` so hooks know which record to update.
  - `CLAUDE_CODE_DISABLE_TERMINAL_TITLE=1` so Claude doesn't overwrite the tab title (verify this variable during the spike).
- Set the tab title to the session name with an OSC escape (`ESC ] 0 ; name BEL`) before launching.
- Locate `claude` via PATH, with a configurable override in `config.json`.

### Registry

`%APPDATA%\claudebox\sessions.json`:

```json
{
  "version": 1,
  "sessions": [
    {
      "id": "b3f1c2...",
      "name": "API refactor",
      "cwd": "C:\\Documents\\Source\\api",
      "sessionId": "8c2e...",
      "transcriptPath": "C:\\Users\\benju\\.claude\\projects\\...\\8c2e....jsonl",
      "claudeArgs": ["--model", "opus"],
      "work": "hotfix",
      "colour": "#C62828",
      "scheme": "Campbell",
      "status": "waiting",
      "pid": 12345,
      "open": true,
      "createdAt": "2026-10-24T12:00:00Z",
      "lastActiveAt": "2026-10-24T13:10:00Z"
    }
  ]
}
```

- Several `cb` processes and hooks write at the same time, so every read-modify-write takes a lock (`sessions.lock` opened with `FileShare.None`, retry with backoff) and writes via temp file plus `File.Replace` to stay atomic.
- `open` is set on launch and cleared on clean exit. After a reboot, sessions still marked `open` with a dead `pid` are exactly the ones that were running, so they're what `cb restore` brings back by default.
- `cwd` is required, not just the GUID: Claude stores transcripts per project folder
  (`~/.claude/projects/<encoded cwd>/<id>.jsonl`) and `--resume` looks there.

### Work types and colours

`%APPDATA%\claudebox\config.json`:

```json
{
  "claudePath": null,
  "defaultWork": "feature",
  "work": {
    "hotfix":   { "colour": "#C62828", "scheme": "Campbell" },
    "feature":  { "colour": "#2E7D32" },
    "research": { "colour": "#1565C0" },
    "personal": { "colour": "#6A1B9A" }
  }
}
```

- `--work hotfix` looks up the type; `--colour` overrides it for that one session. Colours
  accept hex (`#C62828`) or a small set of names (`red`, `green`, `blue`, `amber`, `purple`, ...).
- `colour` sets the tab colour (`--tabColor`); the optional `scheme` names a Windows Terminal
  colour scheme for the whole pane (`--colorScheme`), e.g. a red-tinted scheme for hotfixes.
- The session record stores `work` and the resolved `colour`/`scheme`, so restored tabs keep
  their colours even if the config changes later (`cb set` updates them).
- `cb ls` shows each session's work type in its colour.
- `--here` can't change the current tab's colour (Windows Terminal has no escape sequence for
  tab colour); it only sets the title. Verify whether a background tint via OSC 11 is worth adding.
- Unknown work type: error listing the configured types (`cb work add` to create one).

### Lifecycle (tracking when Claude closes)

The principle: **a session stays `open` unless `cb` positively sees a deliberate close.**
Reboots, crashes and power cuts run no code at all, so they naturally leave sessions `open`.

| Event | How `cb` sees it | Result |
| --- | --- | --- |
| User exits Claude (`/exit`, Ctrl+D) | `WaitForExit` returns; `SessionEnd` hook gives `reason` | `open = false` |
| Tab or window closed | `SetConsoleCtrlHandler` gets `CTRL_CLOSE_EVENT` (~5s to act) | `open = false`, `closedAt` set |
| Shutdown / logoff | `CTRL_SHUTDOWN_EVENT` / `CTRL_LOGOFF_EVENT` if delivered | stays `open` |
| Crash, power loss, `cb` killed | nothing runs | stays `open` |

- Store Claude's `pid` **and process start time**; liveness = process with that pid exists and
  started at that time (guards against PID reuse after reboot).
- `cb ls` reconciles: `open` + dead process shows as `interrupted` (restorable).
- Risk: during shutdown Windows Terminal may close its tabs first, so `cb` sees
  `CTRL_CLOSE_EVENT` rather than a shutdown event. Mitigation: on restore, treat sessions whose
  `closedAt` is within ~60s of the last shutdown time (registry
  `HKLM\SYSTEM\CurrentControlSet\Control\Windows\ShutdownTime`, or event log ID 1074) as
  interrupted, not closed. Verify in the spike.

### Hooks

`hooks.json` (generated in `%APPDATA%\claudebox`, passed with `--settings` so the user's own settings stay untouched):

| Hook | Action |
| --- | --- |
| `SessionStart` | Update `sessionId` and `transcriptPath` (handles `/clear`, `/resume`, compaction) |
| `UserPromptSubmit` | `status = busy` |
| `Stop` | `status = waiting` |
| `Notification` | `status = needs-attention` |
| `SessionEnd` | `status = exited` |

Each hook runs `cb hook <event>`. It reads the hook JSON from stdin, finds the record by `CLAUDEBOX_ID`, updates it and exits quickly (it must never block Claude; failures are logged to `cb.log` and swallowed).

### Restore

`cb restore` builds one `wt` call with chained tabs:

```
wt -w 0 new-tab --title "API refactor" --suppressApplicationTitle -d "C:\..." cb resume b3f1 ; new-tab ...
```

- Use the `ArgumentList` API to avoid quoting bugs.
- Skip sessions whose `pid` is still alive (already open).
- Warn about sessions whose transcript is missing, and offer to drop them or start them fresh.
- `cb startup enable` puts a shortcut to `cb restore --window new` in the user's Startup folder.

## Project layout

```
claudebox.sln
src/Claudebox/
  Program.cs              command wiring (System.CommandLine)
  Commands/               New, Resume, List, Restore, Rename, Remove, Adopt, Startup, Hook
  Registry/               SessionRecord, SessionStore (locking, atomic writes)
  Launch/                 ClaudeLocator, ClaudeLauncher, HooksFile, TerminalTitle
  Terminal/               WindowsTerminal (builds wt arguments)
  Transcripts.cs          locate and validate ~/.claude/projects transcripts
tests/Claudebox.Tests/    xUnit: store concurrency, name resolution, wt argument building, hook parsing
```

Dependencies: `System.CommandLine`, `Spectre.Console` (tables and prompts for `ls` and ambiguous names). Ship it as a .NET global tool (`dotnet tool install -g`) or a single-file publish onto PATH.

## Milestones

1. **Spike**: done. `new`, `resume`, `ls`, new-tab default, tab colour, lifecycle tracking; verified in Windows Terminal.
2. **Registry**: store with locking, `ls`, `rename`, `rm`, name resolution, name-clash prompt on `cb new`.
3. **Hooks**: generated `hooks.json`, `cb hook` handler, session ID tracking across `/clear`, live status in `ls`.
4. **Restore**: `wt` tab building, `open`/`pid` logic, missing-transcript handling.
5. **Extras**: `adopt` (including a picker over existing transcripts), `startup enable`, packaging.
6. **Later, optional**: `cb pick` single-tab switcher (Option 2 from the earlier discussion).

## Things to verify early

- `--settings` hooks merge with the user's own hooks rather than replacing them.
- How `CLAUDE_CODE_DISABLE_TERMINAL_TITLE` behaves, alongside `--suppressApplicationTitle`.
- `--resume <id>` must run from the original folder; confirm this, since the transcript lives under the folder-encoded project path.
- Behaviour when the same session is resumed twice at once (guard with the `pid` check).
