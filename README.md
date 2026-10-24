# ClaudeBox

`cb` runs Claude Code sessions in named, colour-coded Windows Terminal tabs, keeps track of them, and brings back the ones that were running after a reboot.

## Install

```
dotnet tool install -g BitMagic.ClaudeBox.Cli
```

Needs the .NET 10 runtime, [Claude Code](https://code.claude.com) and Windows Terminal.

## Quick start

```
cb                                  start a session here, in a new tab, called "Claude"
cb new "Login bug" --work hotfix    start a named session with a red tab
cb ls                               see what you've got
cb restore                          after a reboot, reopen what was running
```

`cb -?` shows every command with examples; `cb <command> -?` shows all of a command's options.

Sessions can be given by name, the start of a name, their number in `cb ls`, or the start of their id. Commands that launch Claude open a new tab in the current Windows Terminal window; add `--here` to use the current tab instead.

## Commands

### `cb`

Starts a session in the current folder, named from `defaultName` in the config (`Claude`, then `Claude 2`, ...).

| Option | |
| --- | --- |
| `--here` | Run in this tab |

### `cb new <name> [-- <claude args>]`

Starts a new named session. Anything after `--` is passed to `claude`, e.g. `cb new "Spike" -- --model opus`.

| Option | |
| --- | --- |
| `-d`, `--dir <path>` | Folder to run Claude in (default: the current folder) |
| `-w`, `--work <type>` | Work type from the config, which sets the tab colour |
| `--colour <colour>` | Tab colour, overriding the work type's (`--color` works too) |
| `--here` | Run in this tab |
| `--on-clash <action>` | If the name is taken: `ask`, `resume`, `replace`, `rename` or `fail` |
| `--resume-existing` | Same as `--on-clash resume` |
| `--replace` | Same as `--on-clash replace` |
| `--rename-pattern <p>` | Pattern for a free name, e.g. `"{name} {n}"` |

If the name is taken, `cb` asks whether to resume it, delete it and start again, or use a different name (see `nameClash` in the config).

### `cb resume <session>`

Reopens a saved session with its conversation, in its own folder. Refuses if it's already running.

| Option | |
| --- | --- |
| `--here` | Run in this tab |

### `cb ls`

Lists sessions with their work type (in its colour), status, when they were last active and their folder.

| Option | |
| --- | --- |
| `-a`, `--all` | Include sessions closed more than two weeks ago |

### `cb restore`

Reopens sessions in tabs, all in one go. On its own it reopens only sessions that were interrupted (by a reboot, crash or shutdown); ones you closed yourself stay closed.

| Option | |
| --- | --- |
| `-a`, `--all` | Reopen every session that isn't running |
| `-w`, `--work <type>` | Reopen every session of this work type that isn't running |
| `--new-window` | Open the tabs in a new window |
| `--dry-run` | Show what would be reopened without opening anything |

### `cb set <session>`

Changes a session's details. Changes to an open session's tab show the next time it's opened.

| Option | |
| --- | --- |
| `-n`, `--name <name>` | New name |
| `-w`, `--work <type>` | Work type; brings that type's colour and scheme |
| `--colour <colour>` | Tab colour, or `none` |
| `--scheme <scheme>` | Windows Terminal colour scheme, or `none` |

### `cb rename <session> <new name>`

Same as `cb set <session> --name <new name>`.

### `cb rm [<session>]`

Forgets sessions. Claude's transcripts are kept unless you ask otherwise, and running sessions are never removed. Removing several lists them and asks first.

| Option | |
| --- | --- |
| `--exited` | Every session that was exited or closed (interrupted ones are kept so they can be restored) |
| `-w`, `--work <type>` | Every session of this work type that isn't running; with `--exited`, only the exited ones |
| `-y`, `--yes` | Don't ask before removing several |
| `--forget-transcript` | Also delete Claude's transcript of the conversation |

### `cb config`

Shows your settings and where the file is. Values are checked before they're saved, so a typo can't leave a config that won't load.

| Command | Description |
| --- | --- |
| `cb config set <setting> <value>` | Change a setting, e.g. `cb config set nameClash.action rename` |
| `cb config unset <setting>` | Put a setting back to its default |
| `cb config edit` | Open `config.json` in `$VISUAL`/`$EDITOR`, or whatever opens `.json` files |

### `cb work`

Lists the work types, their colours, and which is the default.

| Command | Description |
| --- | --- |
| `cb work add <type> [--colour <c>] [--scheme <s>]` | Add a work type, or change one; `none` clears a colour or scheme |
| `cb work rm <type>` | Remove a work type (not the default one) |

Sessions keep the colour they were given, so changing a work type doesn't recolour existing sessions; use `cb set <session> --work <type>` for that.

## Statuses

| Status | |
| --- | --- |
| `running` | Claude has started |
| `waiting` | Waiting for you to type |
| `busy` | Working on a prompt |
| `attention` | Claude needs you, e.g. to approve a tool |
| `exited` | You exited Claude |
| `closed` | You closed the tab |
| `interrupted` | Was running when Windows shut down or something crashed; `cb restore` brings these back |

## Config

`%APPDATA%\claudebox\config.json` is created on first run. Change it with `cb config` and `cb work`, or edit it by hand (`cb config` rewrites the file, so comments you add are lost). Command-line options override it for a single command.

```json
{
  "claudePath": null,
  "defaultWork": "general",
  "defaultName": "Claude",
  "nameClash": { "action": "ask", "nonInteractive": "fail", "renamePattern": "{name} {n}" },
  "work": {
    "general":  {},
    "hotfix":   { "colour": "red" },
    "feature":  { "colour": "green" },
    "research": { "colour": "blue" }
  }
}
```

| Setting | |
| --- | --- |
| `claudePath` | Full path to `claude`, if it isn't on your PATH |
| `defaultName` | Name for `cb` on its own; `{folder}` is replaced with the current folder's name |
| `defaultWork` | Work type when `--work` isn't given |
| `nameClash.action` | What `cb new` does when the name is taken: `ask`, `resume`, `replace`, `rename` or `fail` |
| `nameClash.nonInteractive` | The same, when there's no one to ask (e.g. in a script) |
| `nameClash.renamePattern` | How `rename` makes a free name; needs `{n}` |
| `work` | Work types: a tab `colour` and optionally a Windows Terminal colour `scheme` for the whole pane |

Colours can be a name or hex (`#RRGGBB` or `#RGB`), and names are saved as names rather than converted to hex. `red`, `orange`, `amber`, `yellow`, `green`, `teal`, `blue`, `purple`, `pink` and `grey` are picked to look good as tab colours; any other standard web colour name works too (`crimson`, `navy`, `darkorange`, ...). Spaces and case don't matter, so `Dark Orange` is `darkorange`.

## How it works

`cb` starts `claude` with a session ID it chooses and passes some hooks with `--settings`, so your own Claude settings aren't touched. The hooks keep the session ID up to date after `/clear` and track whether Claude is busy or waiting. Exiting Claude or closing its tab marks the session as ended; a shutdown or crash leaves it open, which is how `cb restore` knows what to bring back.

Sessions are kept in `%APPDATA%\claudebox\sessions.json`.

## Updating

```
dotnet tool update -g BitMagic.ClaudeBox.Cli
```

Tabs run a private copy of `cb` (in `%APPDATA%\claudebox\bin`), so you can update while sessions are open; old copies are removed once nothing is using them. A session started with `--here` runs from the installed tool itself, so close those before updating.

## Building from source

```
dotnet build
dotnet test
install-local-build.bat
```

`install-local-build.bat` installs your build as the global tool, numbered `<next version>-local.<timestamp>`, and `install-from-nuget.bat` switches back to the latest release. Both uninstall first and install an exact version from a single source, so it doesn't matter which one is numbered higher.

Releases are published to NuGet by pushing a `v*` tag (e.g. `v0.2.0`), or by running the Release workflow on GitHub.

## Licence

GPL-3.0. See [LICENSE](LICENSE).
