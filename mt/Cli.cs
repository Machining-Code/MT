using CsvHelper;
using Mt.Command;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Mt
{
    /// <summary>
    /// Implements the CLI commands and REPL.
    /// </summary>
    public class Cli
    {

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
            if (Format == Format.Xml)
            {
                if (HeaderOnly)
                {
                    var header = doc.Descendants().Where(e => e.Name.LocalName == "Header").FirstOrDefault();
                    Console.WriteLine(header);
                }
                else
                {
                    Console.WriteLine(doc);
                }
            }
            else if (Format == Format.Json)
            {
                string json = JsonConvert.SerializeXNode(doc);
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

        private void Output(IEnumerable<XElement> elements)
        {
            if (!elements.Any()) return;

            // Write header text, if any.
            if (Format == Format.Csv)
            {
                var sampleItem = elements.FirstOrDefault();
                if (sampleItem == null) return;

                Console.Write("Type");
                Console.Write(",");
                foreach (var attr in sampleItem.Attributes())
                    Console.Write(attr.Name.LocalName + ",");
                Console.WriteLine("Value");
            }

            // Write content
            foreach (var elem in elements)
            {

                if (Format == Format.Xml)
                    Console.WriteLine(elem);
                else if (Format == Format.Json)
                    Console.WriteLine(JsonConvert.SerializeXNode(elem));
                else if (Format == Format.PrettyJson)
                    Console.WriteLine(JsonConvert.SerializeXNode(elem, Formatting.Indented));
                else if (Format == Format.CsvNoHeader || Format == Format.Csv)
                {
                    using var writer = new CsvWriter(Console.Out, CultureInfo.CurrentCulture);
                    writer.WriteField(elem.Name.LocalName);
                    foreach (var attr in elem.Attributes())
                        writer.WriteField(attr.Value);
                    writer.WriteField(elem.Value);
                    writer.NextRecord();
                }
                else
                    throw new NotSupportedException($"Cannot output document in format {Format}.");
            }
        }

        private void ProcessDoc(XDocument doc, params Func<IEnumerable<XElement>, IEnumerable<XElement>>[] filters)
        {
            // Verify that the document does not contain MTConnect errors
            AssertNotErrorsDoc(doc);

            // If there are no filters, output the entire document
            if (!filters.Any())
            {
                Output(doc);
                return;
            }

            // Apply filters and output results
            var elements = doc.Root.DescendantsAndSelf();
            foreach (var filter in filters)
                elements = filter(elements);

            Output(elements);
        }

        private bool MatchString(string target, string pattern)
        {
            var patternExists = !string.IsNullOrWhiteSpace(pattern);
            var targetExists = !string.IsNullOrWhiteSpace(target);
            if (patternExists && targetExists && pattern.Length >= 2 && pattern.StartsWith('/') && pattern.EndsWith('/'))
                return Regex.IsMatch(target, pattern.Substring(1, pattern.Length - 2), RegexOptions.IgnoreCase);
            else
                return string.Compare(target, pattern, true) == 0;
        }

        private Func<IEnumerable<XElement>, IEnumerable<XElement>> FilterByCategory(Category category) =>
            elements => elements.Where(elem => MatchString(elem.Name.LocalName, category.ToString())).SelectMany(elem => elem.Descendants());

        private Func<IEnumerable<XElement>, IEnumerable<XElement>> FilterByDataItemId(string dataItemId) =>
            elements => elements.Where(elem => MatchString(elem.Attribute("dataItemId")?.Value, dataItemId));

        private Func<IEnumerable<XElement>, IEnumerable<XElement>> FilterByDataItemName(string dataItemName) =>
            elements => elements.Where(elem => MatchString(elem.Attribute("name")?.Value, dataItemName));

        private Func<IEnumerable<XElement>, IEnumerable<XElement>> FilterByDataItemType(string dataItemType) =>
            elements => elements.Where(elem => MatchString(elem.Name.LocalName, dataItemType));

        private Func<IEnumerable<XElement>, IEnumerable<XElement>> FilterByDataItemSubType(string dataItemSubType) =>
            elements => elements.Where(elem => MatchString(elem.Attribute("subType")?.Value, dataItemSubType));

        private Func<IEnumerable<XElement>, IEnumerable<XElement>> FilterByGeneric(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return elem => elem;
            var tokens = filter.Split(';');
            var filters = new List<Predicate<XElement>>();

            foreach(var token in tokens)
            {
                var kvpTokens = token.Split('=', 2);
                if (kvpTokens.Length != 2)
                    throw new Exception($"Invalid filter: {token}");

                var key = kvpTokens[0];
                var value = kvpTokens[1];

                if (MatchString(key, "tag"))
                    filters.Add(elem => MatchString(elem.Name.LocalName, value));
                else if (MatchString(key, "value"))
                    filters.Add(elem => MatchString(elem.Value, value));
                else
                    filters.Add(elem => MatchString(elem.Attribute(key)?.Value, value));
            }

            return elements => elements.Where(elem => filters.TrueForAll(filter => filter(elem)));
        }

        private void OutputStreamsDocument(XDocument doc, Category? category = null, string dataItemId = null, string dataItemName = null, string dataItemType = null, string dataItemSubType = null, string filter = null)
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
            if (!string.IsNullOrWhiteSpace(filter))
                filters.Add(FilterByGeneric(filter));

            ProcessDoc(doc, filters.ToArray());
        }


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="proc"></param>
        public Cli(CliBuilder proc)
        {
            _proc = proc;
        }


        /// <summary>
        /// Sets up the application
        /// </summary>
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

        /// <summary>
        /// Processes through an args array, running each command until the args are exhausted or an error occurs.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Is verbose mode on
        /// </summary>
        public bool Verbose => _proc.GetOption<bool>(nameof(Verbose));

        /// <summary>
        /// Display only header of MTConnect responses
        /// </summary>
        public bool HeaderOnly => _proc.GetOption<bool>(nameof(HeaderOnly));

        /// <summary>
        /// Output format
        /// </summary>
        public Format Format => _proc.GetOption<Format>(nameof(Format));

        /// <summary>
        /// Creates an agent for use by other commands
        /// </summary>
        /// <param name="agentUri"></param>
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

        /// <summary>
        /// Tests whether the given URI answers as an MTConnect Agent
        /// </summary>
        /// <param name="agentUri"></param>
        /// <returns></returns>
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

            if (doc.Root.Name.LocalName != "MTConnectDevices")
                throw new Exception("Probe did not return an MTConnectDevices document. Not an MTConnect agent.");


            Console.WriteLine("OK");
        }

        /// <summary>
        /// Enters interactive mode
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Sets an application option
        /// </summary>
        /// <param name="key">The option name</param>
        /// <param name="value">The option value</param>
        [Command]
        [Description("Sets an option specified by a key to a value.")]
        public void Option([Positional(0)] string key, [Positional(1)] string value)
        {
            if (Verbose) Console.WriteLine($"OPTION {key}={value}");
            _proc.SetOption(key, value);
        }

        /// <summary>
        /// Displays all application options and values
        /// </summary>
        [Command]
        [Description("Show current value of all options.")]
        public void ShowOptions()
        {
            if (Verbose) Console.WriteLine($"SHOWOPTIONS");

            var options = _proc.GetOptions();
            foreach (var option in options)
            {
                Console.WriteLine($"{option.Key}: {option.Value}");
            }
        }

        /// <summary>
        /// Clears the screen
        /// </summary>
        [Command]
        [Description("Clears the screen.")]
        public void Clear()
        {
            if (Verbose) Console.WriteLine($"CLEAR");
            Console.Clear();
        }

        /// <summary>
        /// Sends a probe request to the current agent
        /// </summary>
        /// <param name="deviceName"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Sends a current request to the current agent
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="at"></param>
        /// <param name="path"></param>
        /// <param name="interval"></param>
        /// <param name="category"></param>
        /// <param name="dataItemId"></param>
        /// <param name="dataItemName"></param>
        /// <param name="dataItemType"></param>
        /// <param name="dataItemSubType"></param>
        /// <returns></returns>
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
            [Named("subType")] string dataItemSubType = null,
            [Named("filter")] string filter = null
            )
        {
            if (Verbose) Console.WriteLine($"CURRENT {deviceName} {at} {path} {interval}");
            if (_agent == null)
                throw new Exception("No connection to an MTConnect Agent has been configured.");

            if (interval != null && interval.Value > 0)
                await foreach (var doc in _agent.CurrentAsync(interval.Value, deviceName, at, path))
                    OutputStreamsDocument(doc, category, dataItemId, dataItemName, dataItemType, dataItemSubType, filter);
            else
            {
                var doc = await _agent.CurrentAsync(deviceName, at, path);
                OutputStreamsDocument(doc, category, dataItemId, dataItemName, dataItemType, dataItemSubType, filter);
            }
        }

        /// <summary>
        /// Sends a sample request to the current agent
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="from"></param>
        /// <param name="path"></param>
        /// <param name="interval"></param>
        /// <param name="count"></param>
        /// <param name="category"></param>
        /// <param name="dataItemId"></param>
        /// <param name="dataItemName"></param>
        /// <param name="dataItemType"></param>
        /// <param name="dataItemSubType"></param>
        /// <returns></returns>
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
            [Named("subType")] string dataItemSubType = null,
            [Named("filter")] string filter = null
            )
        {
            if (Verbose) Console.WriteLine($"SAMPLE {deviceName} {from} {path} {interval} {count}");
            if (_agent == null)
                throw new Exception("No connection to an MTConnect Agent has been configured.");

            if (interval != null && interval.Value > 0)
                await foreach (var doc in _agent.SampleAsync(interval.Value, deviceName, from, path, count))
                    OutputStreamsDocument(doc, category, dataItemId, dataItemName, dataItemType, dataItemSubType, filter);
            else
            {
                var doc = await _agent.SampleAsync(deviceName, from, path, count);
                OutputStreamsDocument(doc, category, dataItemId, dataItemName, dataItemType, dataItemSubType, filter);
            }
        }
        
        /// <summary>
        /// Sends an asset or assets request to the current agent
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="type"></param>
        /// <param name="removed"></param>
        /// <param name="count"></param>
        /// <returns></returns>
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

        [Command]
        [Description("Displays basic status of all devices.")]
        public async Task Status()
        {
            if (Verbose) Console.WriteLine($"Status");
            if (_agent == null)
                throw new Exception("No connection to an MTConnect Agent has been configured.");

            // Get current status
            var doc = await _agent.CurrentAsync();
            AssertNotErrorsDoc(doc);

            // Find device streams
            var devices = doc.Descendants().Where(elem => elem.Name.LocalName == "DeviceStream");
            foreach (var devElem in devices)
            {
                var name = devElem.Attribute("name")?.Value ?? devElem.Attribute("uuid")?.Value ?? "unknown";
                Console.BackgroundColor = ConsoleColor.Blue;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(name);
                Console.Write(new string(' ', Console.WindowWidth - name.Length));
                Console.ResetColor();
                Console.WriteLine();

                // Find component streams
                var components = devElem.Descendants().Where(elem => elem.Name.LocalName == "ComponentStream");
                foreach (var componentElem in components)
                {
                    var componentName = componentElem.Attribute("name")?.Value ?? componentElem.Attribute("componentId")?.Value ?? "unknown";
                    Console.Write("|-");
                    Console.Write(componentName);
                    Console.WriteLine();

                    // Find condition
                    var condition = componentElem.Descendants().FirstOrDefault(elem => elem.Name.LocalName == "Condition");
                    if (condition != null)
                        foreach (var conditionElem in condition.Descendants())
                        {
                            var actualCondition = conditionElem.Name.LocalName;
                            var conditionName = conditionElem.Attribute("name")?.Value ?? conditionElem.Attribute("dataItemId")?.Value ?? "unknown";
                            var timestamp = conditionElem.Attribute("timestamp");
                            Console.Write("  |-");

                            if (actualCondition == "Normal")
                            {
                                Console.BackgroundColor = ConsoleColor.Black;
                                Console.ForegroundColor = ConsoleColor.Green;
                            }
                            else if (actualCondition == "Warning")
                            {
                                Console.BackgroundColor = ConsoleColor.Yellow;
                                Console.ForegroundColor = ConsoleColor.Black;
                            }
                            else if (actualCondition == "Fault")
                            {
                                Console.BackgroundColor = ConsoleColor.DarkRed;
                                Console.ForegroundColor = ConsoleColor.White;
                            }
                            else if (actualCondition == "Unavailable")
                            {
                                Console.BackgroundColor = ConsoleColor.DarkGray;
                                Console.ForegroundColor = ConsoleColor.Black;
                            }

                            Console.Write(conditionName);
                            if (timestamp != null)
                            {
                                Console.Write("\t( since ");
                                Console.Write(timestamp.Value);
                                Console.Write(")");
                            }

                            Console.ResetColor();
                            Console.WriteLine();
                        }
                }

                Console.WriteLine();
            }
        }
    }
}
