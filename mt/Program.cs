using Mt.Command;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mt
{
    class Program
    {
        static async Task Main(string[] args)
        {

            // Note: for double-quotes in powershell
            //  ./mt connect http://agent.mtconnect.org option verbose true current -path //DataItem[@type='\"AVAILABILITY\"']
            // See https://stackoverflow.com/questions/6714165/powershell-stripping-double-quotes-from-command-line-arguments
            // tldr; backslash is not powershell-related, so the command processor used to process argv doesn't remove them from args.
            var proc = new Command.CliBuilder();
            var commands = new Cli(proc);
            commands.Setup();

            var commandArgs = args;
            if (commandArgs.Length == 0)
                commandArgs = new string[] { "INTERACTIVE" };

            await commands.ProcessToEnd(commandArgs);
        }
    }
}
