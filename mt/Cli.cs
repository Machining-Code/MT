using Mt.Command;
using Newtonsoft.Json;
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
        // add better CSV support
        // support regex match on name/id filters

        private readonly CliBuilder _proc;
        private Agent _agent;
        private Options _options;


        private void AssertNotErrorsDoc(XDocument doc)
        {
            var errors = doc.Descendants().Where(e => e.Name.LocalName == "Errors");
            if (!errors.Any())
                return;

                var message = ($"MTConnect agent reported one or more errors:");
                foreach (var child in errors)
                    message += ($"- {child.Value}");
            throw new Exception(message);
        }

        private void Output(XDocument doc)
        {
            if(Format == Format.Xml)
            {
                if(HeaderOnly)
                {
                    var header = doc.Descendants().Where(e => e.Name.LocalName == "Header").FirstOrDefault();
                    Console.WriteLine(header);
                }
                else
                {
                    Console.WriteLine(doc);
                }
            }
            else if(Format == Format.Json)
            {
                string json =JsonConvert.SerializeXNode(doc);
                Console.WriteLine(json);
            }
            else if (Format == Format.PrettyJson)
            {
                string json = JsonConvert.SerializeXNode(doc, Formatting.Indented);
                Console.WriteLine(json);
            }
            else
                throw new NotSupportedException($"Cannot output document in format {Format}.");
        }

        private void Output(XElement elem)
        {
            if (Format == Format.Xml)
                Console.WriteLine(elem);
            else if (Format == Format.Json)
                Console.WriteLine(JsonConvert.SerializeXNode(elem));
            else if (Format == Format.PrettyJson)
                Console.WriteLine(JsonConvert.SerializeXNode(elem, Formatting.Indented));
            else if (Format == Format.CsvNoHeader || Format == Format.Csv)
            {
                //TODO: better CSV handling -- this is broken because no escaping is done
                foreach (var attr in elem.Attributes())
                    Console.Write(attr.Value + ", ");
                Console.WriteLine(elem.Value);
            }
            else
                throw new NotSupportedException($"Cannot output document in format {Format}.");
        }

        private void ProcessDoc(XDocument doc, params Func<IEnumerable<XElement>, IEnumerable<XElement>>[] filters)
        {
            // Verify that the document does not contain MTConnect errors
            AssertNotErrorsDoc(doc);

            // If there are no filters, output the entire document
            if(!filters.Any())
            {
                Output(doc);
                return;
            }

            // Apply filters and output results
            // TODO: support formats
            var elements = doc.Root.DescendantsAndSelf();
            foreach (var filter in filters)
                elements = filter(elements);

            foreach (var element in elements)
                Output(element);

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

            // Quick check: if no protocol, assume http://
            if (!agentUri.Contains("://"))
                agentUri = "http://" + agentUri;

            _agent = new Agent(agentUri);
        }

        [Command]
        [Description("Tests whether the URI is a valid MTConnect agent.")]
        public async Task Test([Positional(0)] string agentUri)
        {
            if (Verbose) Console.WriteLine($"TEST {agentUri}");

            // Quick check: if no protocol, assume http://
            if (!agentUri.Contains("://"))
                agentUri = "http://" + agentUri;

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
        [Description("Show current value of all options.")]
        public void ShowOptions()
        {
            if (Verbose) Console.WriteLine($"SHOWOPTIONS");

            var options = _proc.GetOptions();
            foreach(var option in options)
            {
                Console.WriteLine($"{option.Key}: {option.Value}");
            }
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
        public async Task Probe([Named] string deviceName = null)
        {
            if (Verbose) Console.WriteLine($"PROBE {deviceName}");
            if (_agent == null)
                throw new Exception("No connection to an MTConnect Agent has been configured.");

            var doc = await _agent.ProbeAsync(deviceName);
            ProcessDoc(doc);
        }

        private Func<IEnumerable<XElement>, IEnumerable<XElement>> FilterByCategory(Category category) =>
            elements => elements.Where(elem => elem.Name.LocalName == category.ToString()).SelectMany(elem => elem.Descendants());

        private Func<IEnumerable<XElement>, IEnumerable<XElement>> FilterByDataItemId(string dataItemId) =>
            elements => elements.Where(elem => elem.Attribute("dataItemId")?.Value == dataItemId);

        private Func<IEnumerable<XElement>, IEnumerable<XElement>> FilterByDataItemName(string dataItemName) =>
            elements => elements.Where(elem => elem.Attribute("name")?.Value == dataItemName);

        private Func<IEnumerable<XElement>, IEnumerable<XElement>> FilterByDataItemType(string dataItemType) =>
            elements => elements.Where(elem => elem.Name.LocalName == dataItemType);

        private Func<IEnumerable<XElement>, IEnumerable<XElement>> FilterByDataItemSubType(string dataItemSubType) =>
            elements => elements.Where(elem => elem.Attribute("subType")?.Value == dataItemSubType);

        private void OutputStreamsDocument(XDocument doc, Category? category = null, string dataItemId = null, string dataItemName = null, string dataItemType = null, string dataItemSubType = null)
        {
            var filters = new List<Func<IEnumerable<XElement>, IEnumerable<XElement>>>();
            if (category != null)
                filters.Add(FilterByCategory(category.Value));
            if (!string.IsNullOrWhiteSpace(dataItemId))
                filters.Add(FilterByDataItemId(dataItemId));
            if (!string.IsNullOrWhiteSpace(dataItemName))
                filters.Add(FilterByDataItemName(dataItemName));
            if (!string.IsNullOrWhiteSpace(dataItemType))
                filters.Add(FilterByDataItemType(dataItemType));
            if (!string.IsNullOrWhiteSpace(dataItemSubType))
                filters.Add(FilterByDataItemSubType(dataItemSubType));

            ProcessDoc(doc, filters.ToArray());
        }

        [Command]
        [Description("Sends a current request to the MTConnect agent.")]
        public async Task Current(
            [Named] string deviceName = null,
            [Named] ulong? at = null,
            [Named] string path = null,
            [Named] ulong? interval = null,
            [Named] Category? category = null,
            [Named("id")] string dataItemId = null,
            [Named("name")] string dataItemName = null,
            [Named("type")] string dataItemType = null,
            [Named("subType")] string dataItemSubType = null
            )
        {
            if (Verbose) Console.WriteLine($"CURRENT {deviceName} {at} {path} {interval}");
            if (_agent == null)
                throw new Exception("No connection to an MTConnect Agent has been configured.");

            if(interval!=null && interval.Value > 0)
                await foreach (var doc in _agent.CurrentAsync(interval.Value, deviceName, at, path))
                    OutputStreamsDocument(doc, category, dataItemId, dataItemName, dataItemType, dataItemSubType);
            else
            {
                var doc = await _agent.CurrentAsync(deviceName, at, path);
                OutputStreamsDocument(doc, category, dataItemId, dataItemName, dataItemType, dataItemSubType);
            }
        }

        [Command]
        [Description("Sends a sample request to the MTConnect agent.")]
        public async Task Sample(
            [Named] string deviceName = null,
            [Named] ulong? from = null,
            [Named] string path = null,
            [Named] ulong? interval = null,
            [Named] ulong? count = null,
            [Named] Category? category = null,
            [Named("id")] string dataItemId = null,
            [Named("name")] string dataItemName = null,
            [Named("type")] string dataItemType = null,
            [Named("subType")] string dataItemSubType = null
            )
        {
            if (Verbose) Console.WriteLine($"SAMPLE {deviceName} {from} {path} {interval} {count}");
            if (_agent == null)
                throw new Exception("No connection to an MTConnect Agent has been configured.");

            if (interval != null && interval.Value > 0)
                await foreach (var doc in _agent.SampleAsync(interval.Value, deviceName, from, path, count))
                    OutputStreamsDocument(doc, category, dataItemId, dataItemName, dataItemType, dataItemSubType);
            else
            {
                var doc = await _agent.SampleAsync(deviceName, from, path, count);
                OutputStreamsDocument(doc, category, dataItemId, dataItemName, dataItemType, dataItemSubType);
            }
        }

        [Command]
        [Description("Sends an asset request to the MTConnect agent.")]
        public async Task Asset(
            [Named] string assetId = null,
            [Named] string type = null,
            [Named] string removed = null,
            [Named] ulong? count = null
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
