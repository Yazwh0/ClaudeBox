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
var restoreNewWindow = new Option<bool>("--new-window") { Description = "Open the tabs in a new window" };
var restoreDryRun = new Option<bool>("--dry-run") { Description = "Show what would be restored without opening anything" };
var restoreCommand = new Command("restore", "Reopen sessions that were running when Windows shut down")
{
    restoreAll, restoreNewWindow, restoreDryRun,
};
restoreCommand.SetAction(r => SessionCommands.Restore(r.GetValue(restoreAll), r.GetValue(restoreNewWindow), r.GetValue(restoreDryRun)));

// rename
var renameNewName = new Argument<string>("new-name");
var renameCommand = new Command("rename", "Rename a session") { sessionArgument, renameNewName };
renameCommand.SetAction(r => SessionCommands.Rename(r.GetValue(sessionArgument)!, r.GetValue(renameNewName)!));

// rm
var rmForget = new Option<bool>("--forget-transcript") { Description = "Also delete Claude's transcript of the conversation" };
var rmCommand = new Command("rm", "Remove a session from cb") { sessionArgument, rmForget };
rmCommand.SetAction(r => SessionCommands.Remove(r.GetValue(sessionArgument)!, r.GetValue(rmForget)));

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

var root = new RootCommand("claudebox: named, restorable Claude Code sessions")
{
    newCommand, resumeCommand, lsCommand, restoreCommand, renameCommand, rmCommand, runCommand, hookCommand,
};

try
{
    return root.Parse(args).Invoke(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
}
catch (Exception e) when (e is SessionNotFoundException or ConfigException or FileNotFoundException)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}
