using System.CommandLine;
using Claudebox;
using Claudebox.Commands;
using Claudebox.Registry;

var hereOption = new Option<bool>("--here") { Description = "Run in this tab instead of opening a new one" };
var sessionArgument = new Argument<string>("session") { Description = "Session name, number from 'cb ls', or id prefix" };

// new
var newName = new Argument<string>("name") { Description = "Name for the session (shown as the tab title)" };
var newDir = new Option<DirectoryInfo?>("--dir", "-d") { Description = "Folder to run Claude in (default: current folder)" };
var newWork = new Option<string?>("--work", "-w") { Description = "Work type from config.json, e.g. hotfix" };
var newColour = new Option<string?>("--colour", "--color") { Description = "Tab colour (#RRGGBB or a name); overrides the work type's" };
var newOnClash = new Option<string?>("--on-clash") { Description = "If the name exists: ask, resume, replace, rename or fail" };
var newResumeExisting = new Option<bool>("--resume-existing") { Description = "Same as --on-clash resume" };
var newReplace = new Option<bool>("--replace") { Description = "Same as --on-clash replace" };
var newRenamePattern = new Option<string?>("--rename-pattern") { Description = "Pattern for a free name, e.g. \"{name} {n}\"" };
var newClaudeArgs = new Argument<string[]>("claude-args")
{
    Description = "Extra arguments for claude, after --",
    Arity = ArgumentArity.ZeroOrMore,
};
var newCommand = new Command("new", "Start a new named Claude session")
{
    newName, newDir, newWork, newColour, hereOption, newOnClash, newResumeExisting, newReplace, newRenamePattern, newClaudeArgs,
};
newCommand.SetAction(r =>
{
    ClashAction? onClash = r.GetValue(newOnClash) is { } value ? NameClash.Parse(value)
        : r.GetValue(newResumeExisting) ? ClashAction.Resume
        : r.GetValue(newReplace) ? ClashAction.Replace
        : null;

    return SessionCommands.New(new NewOptions(
        r.GetValue(newName)!, r.GetValue(newDir), r.GetValue(newWork), r.GetValue(newColour), r.GetValue(hereOption),
        r.GetValue(newClaudeArgs) ?? [], onClash, r.GetValue(newRenamePattern)));
});

// resume
var resumeCommand = new Command("resume", "Resume a saved session") { sessionArgument, hereOption };
resumeCommand.SetAction(r => SessionCommands.Resume(r.GetValue(sessionArgument)!, r.GetValue(hereOption)));

// ls
var lsAll = new Option<bool>("--all", "-a") { Description = "Include sessions closed more than two weeks ago" };
var lsCommand = new Command("ls", "List sessions") { lsAll };
lsCommand.SetAction(r => SessionCommands.List(r.GetValue(lsAll)));

// restore
var restoreAll = new Option<bool>("--all", "-a") { Description = "Reopen every session that isn't running, not just interrupted ones" };
var restoreWork = new Option<string?>("--work", "-w") { Description = "Reopen every session of this work type that isn't running" };
var restoreNewWindow = new Option<bool>("--new-window") { Description = "Open the tabs in a new window" };
var restoreDryRun = new Option<bool>("--dry-run") { Description = "Show what would be restored without opening anything" };
var restoreCommand = new Command("restore", "Reopen sessions that were running when Windows shut down")
{
    restoreAll, restoreWork, restoreNewWindow, restoreDryRun,
};
restoreCommand.SetAction(r => SessionCommands.Restore(
    r.GetValue(restoreAll), r.GetValue(restoreWork), r.GetValue(restoreNewWindow), r.GetValue(restoreDryRun)));

// set
var setName = new Option<string?>("--name", "-n") { Description = "New name" };
var setWork = new Option<string?>("--work", "-w") { Description = "Work type from config.json (brings its colour and scheme)" };
var setColour = new Option<string?>("--colour", "--color") { Description = "Tab colour (#RRGGBB or a name), or none" };
var setScheme = new Option<string?>("--scheme") { Description = "Windows Terminal colour scheme, or none" };
var setCommand = new Command("set", "Change a session's name, work type, colour or scheme") { sessionArgument, setName, setWork, setColour, setScheme };
setCommand.SetAction(r => SessionCommands.Set(r.GetValue(sessionArgument)!,
    new SessionChanges(r.GetValue(setName), r.GetValue(setWork), r.GetValue(setColour), r.GetValue(setScheme))));

// rename
var renameNewName = new Argument<string>("new-name");
var renameCommand = new Command("rename", "Rename a session (same as cb set --name)") { sessionArgument, renameNewName };
renameCommand.SetAction(r => SessionCommands.Set(r.GetValue(sessionArgument)!, new SessionChanges(Name: r.GetValue(renameNewName))));

// rm
var rmSession = new Argument<string?>("session")
{
    Description = "Session name, number from 'cb ls', or id prefix",
    Arity = ArgumentArity.ZeroOrOne,
};
var rmExited = new Option<bool>("--exited") { Description = "Remove every session that was exited or closed (not interrupted ones)" };
var rmWork = new Option<string?>("--work", "-w") { Description = "Remove every session of this work type that isn't running" };
var rmYes = new Option<bool>("--yes", "-y") { Description = "Don't ask before removing several sessions" };
var rmForget = new Option<bool>("--forget-transcript") { Description = "Also delete Claude's transcript of the conversation" };
var rmCommand = new Command("rm", "Remove a session from cb") { rmSession, rmExited, rmWork, rmYes, rmForget };
rmCommand.SetAction(r => SessionCommands.Remove(
    r.GetValue(rmSession), r.GetValue(rmExited), r.GetValue(rmWork), r.GetValue(rmYes), r.GetValue(rmForget)));

// run (internal: what a new tab executes)
var runId = new Argument<string>("id");
var runCommand = new Command("run", "Internal: run a session in this console") { runId };
runCommand.Hidden = true;
runCommand.SetAction(r => SessionCommands.Run(r.GetValue(runId)!));

// hook (internal: called by Claude Code)
var hookEvent = new Argument<string>("event");
var hookCommand = new Command("hook", "Internal: handle a Claude Code hook") { hookEvent };
hookCommand.Hidden = true;
hookCommand.SetAction(r => HookCommand.Handle(r.GetValue(hookEvent)!));

var root = new RootCommand("claudebox: named, restorable Claude Code sessions. On its own, cb starts a session in this folder.")
{
    hereOption,
    newCommand, resumeCommand, lsCommand, restoreCommand, setCommand, renameCommand, rmCommand, runCommand, hookCommand,
};
root.SetAction(r => SessionCommands.Quick(r.GetValue(hereOption)));

try
{
    return root.Parse(args).Invoke(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
}
catch (Exception e) when (e is SessionNotFoundException or ConfigException or FileNotFoundException)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}
