using System.CommandLine;
using Claudebox.Commands;
using Claudebox.Registry;

var hereOption = new Option<bool>("--here") { Description = "Run in this tab instead of opening a new one" };

// new
var newName = new Argument<string>("name") { Description = "Name for the session (shown as the tab title)" };
var newDir = new Option<DirectoryInfo?>("--dir", "-d") { Description = "Folder to run Claude in (default: current folder)" };
var newColour = new Option<string?>("--colour", "--color") { Description = "Tab colour, e.g. #C62828" };
var newClaudeArgs = new Argument<string[]>("claude-args")
{
    Description = "Extra arguments for claude, after --",
    Arity = ArgumentArity.ZeroOrMore,
};
var newCommand = new Command("new", "Start a new named Claude session") { newName, newDir, newColour, hereOption, newClaudeArgs };
newCommand.SetAction(r => SessionCommands.New(
    r.GetValue(newName)!, r.GetValue(newDir), r.GetValue(newColour), r.GetValue(hereOption), r.GetValue(newClaudeArgs) ?? []));

// resume
var resumeQuery = new Argument<string>("session") { Description = "Session name, number from 'cb ls', or id prefix" };
var resumeCommand = new Command("resume", "Resume a saved session") { resumeQuery, hereOption };
resumeCommand.SetAction(r => SessionCommands.Resume(r.GetValue(resumeQuery)!, r.GetValue(hereOption)));

// ls
var lsAll = new Option<bool>("--all", "-a") { Description = "Include sessions closed more than two weeks ago" };
var lsCommand = new Command("ls", "List sessions") { lsAll };
lsCommand.SetAction(r => SessionCommands.List(r.GetValue(lsAll)));

// run (internal: what a new tab executes)
var runId = new Argument<string>("id");
var runCommand = new Command("run", "Internal: run a session in this console") { runId };
runCommand.Hidden = true;
runCommand.SetAction(r => SessionCommands.Run(r.GetValue(runId)!));

var root = new RootCommand("claudebox: named, restorable Claude Code sessions")
{
    newCommand,
    resumeCommand,
    lsCommand,
    runCommand,
};

try
{
    return root.Parse(args).Invoke(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
}
catch (SessionNotFoundException e)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}
