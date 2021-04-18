using Mt.Command;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Mt
{

    public class Cli
    {
        // TODO: 
        // named args SAMPLES, EVENTS, CONDITION for commands SAMPLE and CURRENT - easier than using xpath
        // named args TYPE, SUBTYPE, ID for commands SAMPLE and CURRENT - common filters
        // add support for formats: JSON, CSV with header

        private readonly CliBuilder _proc;
        private Agent _agent;
        private Options _options;


        private void ProcessDoc(XDocument doc)
        {
            // Verify that the document does not contain MTConnect errors
            var errors = doc.Descendants().Where(e => e.Name.LocalName == "Errors");
            if (errors.Any())
            {
                Console.Error.WriteLine($"Error: MTConnect agent returned errors:");
                foreach (var child in errors)
                {
                    Console.Error.WriteLine($"- {child.Value}");
                }
                return;
            }

            // Check output format
            if (Format == Format.Xml)
            {
                if (HeaderOnly)
                {
                    var header = doc.Descendants().Where(e => e.Name.LocalName == "Header").FirstOrDefault();
                    Console.WriteLine(header);
                }
                else
                    Console.WriteLine(doc);
            }
            else if (Format == Format.Json)
            {
                Console.WriteLine("JSON not supported");
            }
            else
            {
                Console.WriteLine($"Format {Format} not supported");
            }
        }


        public Cli(CliBuilder proc)
        {
            _proc = proc;
        }


        public void Setup()
        {
            _proc.ProcessObjectForCommands(this);
            _proc.AddVersionCommand();
            _proc.AddExitCommand();
            _proc.AddHelpCommand();

            _options = new Options
            {
                Format = Format.Xml,
                HeaderOnly = false,
                Verbose = false
            };

            _proc.ProcessObjectForOptions(_options);
        }

        public async Task ProcessToEnd(string[] args)
        {
            while (true)
            {
                try
                {
                    Context context;
                    int tokensRead;

                    (context, tokensRead) = _proc.ParseFirstCommand(args, _options);
                    args = args.Skip(tokensRead).ToArray();

                    if (context == null)
                    {
                        if (args.Any())
                            Console.WriteLine("Parser Error!");
                        break;
                    }

                    await _proc.RunCommand(context);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        public bool Verbose => _proc.GetOption<bool>(nameof(Verbose));

        public bool HeaderOnly => _proc.GetOption<bool>(nameof(HeaderOnly));

        public Format Format => _proc.GetOption<Format>(nameof(Format));

        [Command]
        [Description("Specifies the URI of the MTConnect agent.")]
        public void Connect([Positional(0)] string agentUri)
        {
            if (Verbose) Console.WriteLine($"CONNECT {agentUri}");
            _agent = new Agent(agentUri);
        }

        [Command]
        [Description("Tests whether the URI is a valid MTConnect agent.")]
        public async Task Test([Positional(0)] string agentUri)
        {
            if (Verbose) Console.WriteLine($"TEST {agentUri}");

            var agent = new Agent(agentUri);
            var doc = await agent.ProbeAsync();

            Console.WriteLine("OK");
        }

        [Command]
        [Description("Enters interactive mode.")]
        public async Task Interactive()
        {
            if (Verbose) Console.WriteLine("INTERACTIVE");
            while (true)
            {
                try
                {

                    Console.Write("> ");
                    var commands = Console.ReadLine();

                    // TODO: better argument processing. This won't act the same way as the args parser on the command line.
                    var tokens = commands.Split(' ');

                    Context context;
                    int tokensRead;

                    (context, tokensRead) = _proc.ParseFirstCommand(tokens, _options);

                    if (context == null)
                    {
                        Console.Error.WriteLine("Unrecognized command.");
                        continue;
                    }

                    await _proc.RunCommand(context);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                }

            }
        }

        [Command]
        [Description("Sets an option specified by a key to a value.")]
        public void Option([Positional(0)] string key, [Positional(1)] string value)
        {
            if (Verbose) Console.WriteLine($"OPTION {key}={value}");
            _proc.SetOption(key, value);

        }

        [Command]
        [Description("Clears the screen.")]
        public void Clear()
        {
            if (Verbose) Console.WriteLine($"CLEAR");
            Console.Clear();
        }

        [Command]
        [Description("Sends a probe request to the MTConnect agent.")]
        public async Task Probe([Named("deviceName", isOptional: true)] string deviceName = null)
        {
            if (Verbose) Console.WriteLine($"PROBE {deviceName}");
            if (_agent == null)
                throw new Exception("No connection to an MTConnect Agent has been configured.");

            var doc = await _agent.ProbeAsync(deviceName);
            ProcessDoc(doc);
        }

        [Command]
        [Description("Sends a current request to the MTConnect agent.")]
        public async Task Current(
            [Named("deviceName", isOptional: true)] string deviceName = null,
            [Named("at", isOptional: true)] ulong? at = null,
            [Named("path", isOptional: true)] string path = null,
            [Named("interval", isOptional: true)] ulong? interval = null
            )
        {
            if (Verbose) Console.WriteLine($"CURRENT {deviceName} {at} {path} {interval}");
            if (_agent == null)
                throw new Exception("No connection to an MTConnect Agent has been configured.");

            var doc = await _agent.CurrentAsync(deviceName, at, path, interval);
            ProcessDoc(doc);
        }

        [Command]
        [Description("Sends a sample request to the MTConnect agent.")]
        public async Task Sample(
            [Named("deviceName", isOptional: true)] string deviceName = null,
            [Named("from", isOptional: true)] ulong? from = null,
            [Named("path", isOptional: true)] string path = null,
            [Named("interval", isOptional: true)] ulong? interval = null,
            [Named("count", isOptional: true)] ulong? count = null
            )
        {
            if (Verbose) Console.WriteLine($"SAMPLE {deviceName} {from} {path} {interval} {count}");
            if (_agent == null)
                throw new Exception("No connection to an MTConnect Agent has been configured.");

            var doc = await _agent.SampleAsync(deviceName, from, path, interval, count);
            ProcessDoc(doc);
        }

        [Command]
        [Description("Sends an asset request to the MTConnect agent.")]
        public async Task Asset(
            [Named("assetId", isOptional: true)] string assetId = null,
            [Named("type", isOptional: true)] string type = null,
            [Named("removed", isOptional: true)] string removed = null,
            [Named("count", isOptional: true)] ulong? count = null
            )
        {
            if (Verbose) Console.WriteLine($"Asset {assetId} {type} {removed} {count}");
            if (_agent == null)
                throw new Exception("No connection to an MTConnect Agent has been configured.");

            var doc = await _agent.AssetAsync(assetId, type, removed, count);
            ProcessDoc(doc);
        }

    }
}
