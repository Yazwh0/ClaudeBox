using System.CommandLine;
using System.CommandLine.Invocation;

namespace Claudebox.Commands;

/// <summary>
/// The standard help only lists commands at the top level, so `cb -?` adds examples showing the
/// common options. Each command's own help (`cb rm -?`) is unchanged.
/// </summary>
internal sealed class ExamplesHelpAction(SynchronousCommandLineAction standardHelp) : SynchronousCommandLineAction
{
    private const string Examples = """
        Examples:
          cb                                  start a session here, in a new tab
          cb new "API refactor"               start a named session
          cb new "Login bug" --work hotfix    colour the tab by work type (from config.json)
          cb new "Spike" --dir C:\src --here  choose the folder; run in this tab
          cb new "API refactor" --replace     if the name is taken, replace it without asking
          cb ls                               list sessions and their status
          cb resume "API refactor"            reopen a session (name, number from ls, or prefix)
          cb restore                          reopen sessions interrupted by a reboot or crash
          cb restore --all                    reopen every session that isn't running
          cb restore --work hotfix            ... only one work type (--dry-run to preview)
          cb set 2 --work hotfix              change a session's name, work type, colour or scheme
          cb rm "API refactor"                forget a session (its transcript is kept)
          cb rm --exited                      forget every exited or closed session
          cb rm --work hotfix                 forget every hotfix session that isn't running
          cb rm --exited -y                   ... without asking first
          cb config                           show your settings
          cb config set defaultName Claude    change a setting (cb config unset to go back)
          cb work add bug --colour orange     add a work type, or change one's colour
          cb alias add c "rm --exited -y"     make cb c a shortcut for that command

        Run cb <command> -? for all of a command's options.
        Settings: %APPDATA%\claudebox\config.json (cb config edit opens it)

        """;

    public override int Invoke(ParseResult parseResult)
    {
        var result = standardHelp.Invoke(parseResult);
        if (parseResult.CommandResult.Command is RootCommand)
            Console.WriteLine(Examples);
        return result;
    }
}
