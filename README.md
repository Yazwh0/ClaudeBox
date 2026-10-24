# ClaudeBox

`cb` runs Claude Code sessions in named, colour-coded Windows Terminal tabs, keeps track of them, and brings back the ones that were running after a reboot.

## Install

```
dotnet tool install -g BitMagic.ClaudeBox.Cli
```

Needs the .NET 10 runtime, [Claude Code](https://code.claude.com) and Windows Terminal.

## Use

```
cb new "API refactor"                start a session in a new tab
cb new "Login bug" --work hotfix     colour the tab by work type
cb ls                                list sessions and their status
cb resume "API refactor"             reopen a session in a new tab
cb restore                           reopen everything that was running before a reboot
cb rename <session> <new name>
cb rm <session>
```

Sessions can be given by name, name prefix or their number in `cb ls`. Add `--here` to run in the current tab instead of a new one. Anything after `--` is passed to `claude`.

## Config

`%APPDATA%\claudebox\config.json` is created on first run:

```json
{
  "claudePath": null,
  "defaultWork": "feature",
  "nameClash": { "action": "ask", "nonInteractive": "fail", "renamePattern": "{name} {n}" },
  "work": {
    "hotfix":   { "colour": "#C62828" },
    "feature":  { "colour": "#2E7D32" },
    "research": { "colour": "#1565C0" }
  }
}
```

- `work`: tab colour, and optionally a Windows Terminal colour `scheme`, per type of work.
- `nameClash.action`: what `cb new` does when the name is taken: `ask`, `resume`, `replace`, `rename` or `fail`. Override with `--on-clash`.

## How it works

`cb` starts `claude` with a session ID it chooses and passes some hooks with `--settings` (your own Claude settings aren't touched). The hooks keep the session ID up to date after `/clear` and track whether Claude is busy or waiting. Closing a tab or exiting Claude marks the session closed; a shutdown or crash leaves it open, so `cb restore` knows what to bring back.

## Licence

GPL-3.0. See [LICENSE](LICENSE).
