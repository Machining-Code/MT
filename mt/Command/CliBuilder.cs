using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mt.Command
{

    public class CliBuilder
    {

        private record OptionInfo
        {
            public string Name { get; init; }
            public string Description { get; init; }
            public Func<string, object> Converter { get; init; }
            public Action<string> Assign { get; init; }
            public object Instance { get; init; }
            public PropertyInfo Property { get; init; }
        }

        private readonly Dictionary<string, CommandInfo> _commands = new Dictionary<string, CommandInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, OptionInfo> _options = new Dictionary<string, OptionInfo>(StringComparer.OrdinalIgnoreCase);

        private CommandInfo GetCommandInfo(string name)
        {
            if (_commands.ContainsKey(name))
                return _commands[name];

            return null;
        }

        private int GetPositionalAttributeCount(MethodInfo method)
        {
            return method.GetParameters().Count(p => Attribute.IsDefined(p, typeof(PositionalAttribute)));
        }

        private CommandInfo BuildCommandInfo(MethodInfo method, object instance = null)
        {
            var name = method.GetCustomAttribute<CommandAttribute>()?.CommandName ?? method.Name;
            var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
            var positionalArgs = new List<ParameterInfo>();
            var namedArgs = new Dictionary<string, ParameterInfo>(StringComparer.OrdinalIgnoreCase);
            Func<Context, Task> invoker;

            // Critical check
            if (method.IsAbstract)
                throw new ArgumentException("Method cannot be abstract.");
            if (method.ReturnType != typeof(Task) && method.ReturnType != typeof(void))
                throw new ArgumentException("Method must return Task or void.");

            // Figure out what parameters are needed
            var methodParams = method.GetParameters();
            var paramConverters = new List<Func<Context, object>>();
            foreach (var param in methodParams)
            {
                if (Attribute.IsDefined(param, typeof(ContextAttribute)))
                {
                    // This parameter just takes the command context, so pass it along.
                    paramConverters.Add(ctxt => ctxt);
                    continue;
                }
                else if (param.GetCustomAttribute<PositionalAttribute>() is PositionalAttribute pa)
                {
                    // This is a positional argument, so look it up in the context and get its value.
                    // The value will be a string, so it'll be necessary to parse/convert to the appropriate type.
                    var index = pa.Index;
                    positionalArgs.Add(param);
                    paramConverters.Add(ctxt =>
                    {
                        // Verify that there are enough parameters
                        if (index >= ctxt.PositionalArguments?.Length)
                            throw new ArgumentException($"Argument {pa.Index} was not provided.");

                        // Convert parameter it to the right type and return it.
                        return ConvertRawValue(ctxt.PositionalArguments[index], param.ParameterType);
                    });
                    continue;
                }
                else if (param.GetCustomAttribute<NamedAttribute>() is NamedAttribute na)
                {
                    // This is a named argument
                    var paramName = na.ArgumentName ?? param.Name;
                    namedArgs[paramName] = param;
                    paramConverters.Add(ctxt =>
                    {
                        string rawValue;
                        if (!ctxt.NamedArguments.TryGetValue(paramName, out rawValue))
                                return Type.Missing;

                        // Otherwise, the parameter exists, so convert it to the right type and return it.
                        return ConvertRawValue(rawValue, param.ParameterType);
                    });
                    continue;
                }
                else
                {
                    // Treat it as a named argument, whose name is the name of the parameter
                    // And is required.
                    var paramName = param.Name;
                    namedArgs[paramName] = param;
                    paramConverters.Add(ctxt =>
                    {
                        string rawValue;
                        if (!ctxt.NamedArguments.TryGetValue(paramName, out rawValue))
                            throw new ArgumentException($"Argument {paramName} was not provided.");

                        // Otherwise, the parameter exists, so convert it to the right type and return it.
                        return ConvertRawValue(rawValue, param.ParameterType);
                    });
                    continue;
                }

            }

            if (method.ReturnType == typeof(Task))
            {
                // This is an async method.
                invoker = async (context) =>
                {
                    // Build parameter list
                    var paramsList = paramConverters.Select(converter => converter(context)).ToArray();
                    var result = method.Invoke(instance, paramsList);
                    await (Task)result;
                };
            }
            else
            {
                // This is not an async method, so wrap it in one.
                invoker = async (context) =>
                {
                    // Build parameter list
                    var paramsList = paramConverters.Select(converter => converter(context)).ToArray();
                    method.Invoke(instance, paramsList);
                };
            }

            return new CommandInfo
            {
                Name = name,
                Description = description,
                Runner = invoker,
                PositionalArgumentCount = GetPositionalAttributeCount(method),
                Parameters = method.GetParameters(),
                PositionalArguments = positionalArgs.ToArray(),
                NamedArguments = namedArgs
            };

        }

        public object ConvertRawValue(string value, Type targetType)
        {
            //TODO: better conversion

            // If nullable, get the underlying type of the nullable.
            if (Nullable.GetUnderlyingType(targetType) != null)
                targetType = Nullable.GetUnderlyingType(targetType);

            // If enum, parse string value
            if (targetType.IsEnum)
                return Enum.Parse(targetType, value, true);

            // Fallback to Convert.ChangeType
            return Convert.ChangeType(value, targetType);
        }

        public (Context, int) ParseFirstCommand(string[] args, object options, CancellationToken cancelToken = default(CancellationToken))
        {
            if (args.Length == 0) return (null, 0);

            // parse a command context
            // first arg should be the command
            var command = args[0];
            var info = GetCommandInfo(command);
            if (info == null) return (null, 1);

            // Extract positional arguments
            var positionalArgs = args.Skip(1).Take(info.PositionalArgumentCount).ToArray();

            // Extract named arguments
            var named = args.Skip(info.PositionalArgumentCount + 1).ToArray();

            var namedDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var i = 0;
            for (i = 0; i < named.Length; i += 2)
            {
                var key = named[i];
                if (i + 1 >= named.Length) break;

                var value = named[i + 1];

                if (!key.StartsWith('-')) break;
                key = key.TrimStart('-');
                namedDictionary[key] = value;
            }

            var context = new Context
            {
                Name = command,
                Options = options,
                CancellationToken = cancelToken,
                NamedArguments = namedDictionary,
                PositionalArguments = positionalArgs
            };

            return (context, 1 + positionalArgs.Length + i);
        }

        public async Task RunCommand(Context context)
        {
            if (context == null)
                return;

            try
            {
                var cmd = GetCommand(context.Name);
                await cmd(context);
            }
            catch (TargetInvocationException te)
            {
                throw te.InnerException;
            }
        }

        public Func<Context, Task> GetCommand(string name)
        {
            if (_commands.ContainsKey(name))
                return _commands[name].Runner;

            return null;
        }

        public void ProcessObjectForCommands(object instance)
        {
            var t = instance.GetType();
            foreach (var method in t.GetMethods())
            {
                if (method.GetCustomAttribute<CommandAttribute>() is CommandAttribute ca)
                {
                    var info = BuildCommandInfo(method, instance);
                    _commands[info.Name] = info;
                }
            }
        }

        public void ProcessObjectForOptions(object instance)
        {
            var t = instance.GetType();
            foreach (var property in t.GetProperties())
            {
                if (property.GetCustomAttribute<OptionAttribute>() is OptionAttribute oa)
                {
                    var name = oa.OptionName ?? property.Name;
                    var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
                    Func<string, object> converter = s => ConvertRawValue(s, property.PropertyType);
                    Action<string> assigner = s =>
                    {
                        object value = converter(s);
                        property.SetValue(instance, value);
                    };

                    _options[name] = new OptionInfo
                    {
                        Name = name,
                        Description = description,
                        Converter = converter,
                        Instance = instance,
                        Property = property,
                        Assign = assigner
                    };
                }
            }
        }

        public object GetOption(string key)
        {
            OptionInfo info;
            if (!_options.TryGetValue(key, out info))
                throw new Exception($"No option named {key} exists.");

            return info.Property.GetValue(info.Instance);
        }

        public T GetOption<T>(string key) => (T)GetOption(key);

        public void SetOption(string key, string value)
        {
            OptionInfo info;
            if (!_options.TryGetValue(key, out info))
                throw new Exception($"No option named {key} exists.");

            info.Assign(value);
        }

        public IEnumerable<KeyValuePair<string, object>> GetOptions()
            => _options.Select(kvp => new KeyValuePair<string, object>(kvp.Key, GetOption(kvp.Key))).OrderBy(kvp => kvp.Key);

        public void AddHelpCommand()
        {
            Func<Context, Task> helpFunc = ctx => Task.Run(() =>
            {
                Console.WriteLine("Supported Commands: ");
                Console.WriteLine();

                var longestCommandLength = _commands.Keys.Max(s => s.Length);

                foreach (var kvp in _commands.OrderBy(item => item.Key))
                {
                    var name = kvp.Key.ToUpperInvariant();
                    var description = kvp.Value.Description;

                    var spacer = new string(' ', longestCommandLength - name.Length + 2);

                    Console.Write(name);
                    Console.Write(spacer);
                    Console.Write(description);
                    Console.WriteLine();

                    if (kvp.Value.PositionalArguments.Any() || kvp.Value.NamedArguments.Any())
                    {

                        Console.Write(new string(' ', longestCommandLength + 2));
                        Console.Write("Args: ");
                        foreach (var arg in kvp.Value.PositionalArguments)
                        {
                            Console.Write(arg.GetCustomAttribute<PositionalAttribute>()?.ArgumentName ?? arg.Name);
                            Console.Write(' ');
                        }

                        foreach (var arg in kvp.Value.NamedArguments.OrderBy(k => k.Key).Select(k => k.Value))
                        {
                            Console.Write("[");
                            Console.Write(arg.GetCustomAttribute<NamedAttribute>()?.ArgumentName ?? arg.Name);
                            Console.Write("] ");
                        }
                        Console.WriteLine();
                    }

                }
            });

            var info = new CommandInfo
            {
                Name = "Help",
                Description = "Displays this help text.",
                Parameters = new ParameterInfo[] { },
                PositionalArgumentCount = 0,
                Runner = helpFunc,
                PositionalArguments = new ParameterInfo[] { },
                NamedArguments = new Dictionary<string, ParameterInfo>()
            };

            _commands[info.Name] = info;
        }

        public void AddVersionCommand()
        {
            Func<Context, Task> versionFunc = ctx => Task.Run(() =>
            {
                var assy = Assembly.GetEntryAssembly();
                var appName = assy.GetName().Name;
                var appVersion = assy.GetName().Version;
                Console.WriteLine($"{appName} v.{appVersion}");
            });

            var info = new CommandInfo
            {
                Name = "Version",
                Description = "Displays the version.",
                Parameters = new ParameterInfo[] { },
                PositionalArgumentCount = 0,
                Runner = versionFunc,
                PositionalArguments = new ParameterInfo[] { },
                NamedArguments = new Dictionary<string, ParameterInfo>()
            };

            _commands[info.Name] = info;
        }

        public void AddExitCommand()
        {
            Func<Context, Task> exitFunc = ctx => Task.Run(() =>
            {
                Environment.Exit(0);
            });

            var info = new CommandInfo
            {
                Name = "Exit",
                Description = "Exits the application.",
                Parameters = new ParameterInfo[] { },
                PositionalArgumentCount = 0,
                Runner = exitFunc,
                PositionalArguments = new ParameterInfo[] { },
                NamedArguments = new Dictionary<string, ParameterInfo>()
            };

            _commands[info.Name] = info;
        }

    }
}
