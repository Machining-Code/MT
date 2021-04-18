﻿using Mt.Command;
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
        // TODO: some helper methods
        // TEST     check whether an agent is at the given URL
        //          TEST with no parameters - test the current URL
        //          TEST with one positional parameter - test the passed URL without changing the current URL
        // subcommands SAMPLES, EVENTS, CONDITION for commands SAMPLE and CURRENT - easier than using xpath
        // subcommands TYPE, SUBTYPE, ID for commands SAMPLE and CURRENT - common filters
        // (instead of subcommands, make these named parameters)
        // add support for formats: JSON, CSV with header
        // better way of setting options - make an options object. we can reflect over it to know what's supported



        private readonly IDictionary<string, object> _options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly CommandProcessor _proc;
        private Agent _agent;


        private string BuildUri(string baseUrl, params string[] others)
        {
            if (others.Length == 0)
                return baseUrl;

            if (!baseUrl.EndsWith('/'))
                baseUrl += '/';

            return baseUrl + string.Join('/', others.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray());
        }

        private string BuildUriQuery(string baseUrl, params (string Key, object Value)[] values)
        {
            if (values.Length == 0)
                return baseUrl;

            var queryString = string.Join('&', values.Where(t => t.Item2 != null).Select(t => $"{t.Key}={t.Value}").ToArray());
            return baseUrl + "?" + queryString;
        }

        public Cli(CommandProcessor proc)
        {
            _proc = proc;
        }

        public bool Verbose
        {
            get
            {
                object verboseValue;
                if (!_options.TryGetValue("verbose", out verboseValue)) return false;

                bool verboseBool;
                if (!bool.TryParse(verboseValue?.ToString(), out verboseBool)) return false;

                return verboseBool;
            }
        }

        public bool HeaderOnly
        {
            get
            {
                // TODO: need better way of setting options
                object headerOnlyValue;
                if (!_options.TryGetValue("headeronly", out headerOnlyValue)) return false;

                bool headerOnlyBool;
                if (!bool.TryParse(headerOnlyValue?.ToString(), out headerOnlyBool)) return false;

                return headerOnlyBool;

            }
        }

        public string Format
        {
            get
            {
                object formatObj;
                if (!_options.TryGetValue("format", out formatObj)) return "XML";
                return formatObj?.ToString().ToUpperInvariant() ?? "XML";
            }
        }

        public IReadOnlyDictionary<string, object> Options => (IReadOnlyDictionary<string, object>)_options;

        [Command]
        [Description("Specifies the URI of the MTConnect agent.")]
        public void Connect([Positional(0)] string agentUri)
        {
            if (Verbose) Console.WriteLine($"CONNECT {agentUri}");
            _agent = new Agent(agentUri);
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

                    (context, tokensRead) = _proc.ParseFirstCommand(tokens, Options);

                    if (context == null)
                    {
                        Console.Error.WriteLine("Unrecognized command.");
                        continue;
                    }

                    var cmd = _proc.GetCommand(context.Name);
                    await cmd(context);

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
            _options[key] = value;
        }

        [Command]
        [Description("Sends a probe request to the MTConnect agent.")]
        public async Task Probe([Named("deviceName", isOptional: true)] string deviceName = null)
        {
            if (Verbose) Console.WriteLine($"PROBE {deviceName}");

            try
            {
                var doc = await _agent.ProbeAsync(deviceName);
                ProcessDoc(doc);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
        }

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
            if (Format == "XML")
            {
                if (HeaderOnly)
                {
                    var header = doc.Descendants().Where(e => e.Name.LocalName == "Header").FirstOrDefault();
                    Console.WriteLine(header);
                }
                else
                    Console.WriteLine(doc);
            }
            else if (Format == "JSON")
            {
                Console.WriteLine("JSON not supported");
            }
            else
            {
                Console.WriteLine($"Format {Format} not supported");
            }
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

            try
            {
                var doc = await _agent.CurrentAsync(deviceName, at, path, interval);
                ProcessDoc(doc);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
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

            try
            {
                var doc = await _agent.SampleAsync(deviceName, from, path, interval, count);
                ProcessDoc(doc);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
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

            try
            {
                var doc = await _agent.AssetAsync(assetId, type, removed, count);
                ProcessDoc(doc);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
        }

    }
}
