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
            var proc = new Command.CommandProcessor();
            var commands = new Cli(proc);
            proc.ProcessObject(commands);
            proc.AddVersionCommand();
            proc.AddExitCommand();
            proc.AddHelpCommand();

            var commandArgs = args;
            if (commandArgs.Length == 0)
                commandArgs = new string[] { "INTERACTIVE" };

            while (true)
            {
                Context context;
                int tokensRead;

                (context, tokensRead) = proc.ParseFirstCommand(commandArgs, commands.Options);
                commandArgs = commandArgs.Skip(tokensRead).ToArray();

                if (context == null)
                {
                    if (commandArgs.Any())
                        Console.WriteLine("Parser Error!");
                    break;
                }

                var cmd = proc.GetCommand(context.Name);
                await cmd(context);
            }
        }
    }
}
