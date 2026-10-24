# ClaudeBox

`cb` runs Claude Code sessions in named, colour-coded Windows Terminal tabs, keeps track of them, and brings back the ones that were running after a reboot.

## Install

```
dotnet tool install -g BitMagic.ClaudeBox.Cli
```

Needs the .NET 10 runtime, [Claude Code](https://code.claude.com) and Windows Terminal.

## Use

```
cb                                   start a session here, called "Claude" (then "Claude 2", ...)
cb new "API refactor"                start a named session in a new tab
cb new "Login bug" --work hotfix     colour the tab by work type
cb ls                                list sessions and their status
cb resume "API refactor"             reopen a session in a new tab
cb restore                           reopen everything that was running before a reboot
cb set <session> --name <name> --work <type> --colour <colour> --scheme <scheme>
cb rename <session> <new name>
cb rm <session>                      forget a session (its transcript is kept)
cb rm --exited                       forget every session that was exited or closed
cb restore --all                     reopen every session that isn't running
cb restore --work hotfix             ... just the ones of one work type
cb rm --work hotfix                  forget every hotfix session that isn't running
```

`rm --exited --work hotfix` narrows to exited hotfix sessions. Bulk `rm` lists what it will remove and asks first (`--yes` skips that). `restore --dry-run` shows what would open.

Changes made with `cb set` to an open session's tab show the next time it's opened. `--colour none` and `--scheme none` clear them.

Sessions can be given by name, name prefix or their number in `cb ls`. Add `--here` to run in the current tab instead of a new one. Anything after `--` is passed to `claude`.

## Config

`%APPDATA%\claudebox\config.json` is created on first run:

```json
{
  "claudePath": null,
  "defaultWork": "general",
  "defaultName": "Claude",
  "nameClash": { "action": "ask", "nonInteractive": "fail", "renamePattern": "{name} {n}" },
  "work": {
    "general":  {},
    "hotfix":   { "colour": "#C62828" },
    "feature":  { "colour": "#2E7D32" },
    "research": { "colour": "#1565C0" }
  }
}
```

- `defaultName`: name for `cb` on its own; `{folder}` is replaced with the current folder's name.
- `defaultWork`: work type when `--work` isn't given.
- `work`: tab colour, and optionally a Windows Terminal colour `scheme`, per type of work.
- `nameClash.action`: what `cb new` does when the name is taken: `ask`, `resume`, `replace`, `rename` or `fail`. Override with `--on-clash`.

## How it works

`cb` starts `claude` with a session ID it chooses and passes some hooks with `--settings` (your own Claude settings aren't touched). The hooks keep the session ID up to date after `/clear` and track whether Claude is busy or waiting. Closing a tab or exiting Claude marks the session closed; a shutdown or crash leaves it open, so `cb restore` knows what to bring back.

## Updating

Tabs run a private copy of `cb` (in `%APPDATA%\claudebox\bin`), so you can update or reinstall while sessions are open; each copy is removed once nothing is using it. A session started with `--here` runs from the installed tool itself, so close those before updating.

From source, `install-local-build.bat` installs a build numbered `<next version>-local.<timestamp>`, and `install-from-nuget.bat` switches back to the latest release. Both uninstall first and install an exact version from a single source, so it doesn't matter which one is numbered higher.

## Licence

GPL-3.0. See [LICENSE](LICENSE).
