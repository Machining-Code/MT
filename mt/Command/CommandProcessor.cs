using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mt.Command
{
    public class CommandProcessor
    {

        private record CommandInfo
        {
            public string Name { get; init; }
            public string Description { get; init; }
            public Func<Context, Task> Runner { get; init; }
            public ParameterInfo[] Parameters { get; init; }
            public int PositionalArgumentCount { get; init; }
        }

        public (Context, int) ParseFirstCommand(string[] args, IReadOnlyDictionary<string, object> options, CancellationToken cancelToken = default(CancellationToken))
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
            for(i = 0; i < named.Length; i+=2)
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

        private readonly Dictionary<string, CommandInfo> _commands = new Dictionary<string, CommandInfo>(StringComparer.OrdinalIgnoreCase);

        private object ConvertRawValue(string value, Type targetType)
        {
            //TODO: better conversion
            if (Nullable.GetUnderlyingType(targetType) != null)
                targetType = Nullable.GetUnderlyingType(targetType);

            return Convert.ChangeType(value, targetType);
        }

        public Func<Context, Task> GetCommand(string name)
        {
            if (_commands.ContainsKey(name))
                return _commands[name].Runner;

            return null;
        }

        private CommandInfo GetCommandInfo(string name)
        {
            if (_commands.ContainsKey(name))
                return _commands[name];

            return null;
        }

        public void ProcessObject(object instance)
        {
            var t = instance.GetType();
            foreach (var method in t.GetMethods())
            {
                if (method.GetCustomAttribute<CommandAttribute>() is CommandAttribute ca)
                {
                    var name = ca.CommandName ?? method.Name;
                    var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
                    var invoker = GetAsyncInvoker(method, instance);
                    _commands[name] = new CommandInfo
                    {
                        Name = name,
                        Description = description,
                        Runner = invoker,
                        PositionalArgumentCount = GetPositionalAttributeCount(method),
                        Parameters = method.GetParameters()
                    };
                }
            }
        }

        public void AddHelpCommand()
        {
            // TODO: store parameter info (positional vs named)
            // so we can display parameter info for commands.
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
                }
            });

            var info = new CommandInfo
            {
                Name = "Help",
                Description = "Displays this help text.",
                Parameters = new ParameterInfo[] { },
                PositionalArgumentCount = 0,
                Runner = helpFunc
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
                Runner = versionFunc
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
                Runner = exitFunc
            };

            _commands[info.Name] = info;
        }

        private int GetPositionalAttributeCount(MethodInfo method)
        {
            return method.GetParameters().Count(p => Attribute.IsDefined(p, typeof(PositionalAttribute)));
        }

        private Func<Context, Task> GetAsyncInvoker(MethodInfo method, object instance = null)
        {
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
                    var name = na.ArgumentName ?? param.Name;
                    paramConverters.Add(ctxt =>
                    {
                        string rawValue;
                        if (!ctxt.NamedArguments.TryGetValue(name, out rawValue))
                        {
                            // Named parameter missing.
                            // Emit Type.Missing if optional. Otherwise, error
                            if (na.IsOptional)
                                return Type.Missing;
                            else
                                throw new ArgumentException($"Argument {name} was not provided.");
                        }

                        // Otherwise, the parameter exists, so convert it to the right type and return it.
                        return ConvertRawValue(rawValue, param.ParameterType);
                    });
                    continue;
                }
                else
                {
                    // Treat it as a named argument, whose name is the name of the parameter
                    // And is required.
                    var name = param.Name;
                    paramConverters.Add(ctxt =>
                    {
                        string rawValue;
                        if (!ctxt.NamedArguments.TryGetValue(name, out rawValue))
                            throw new ArgumentException($"Argument {name} was not provided.");

                        // Otherwise, the parameter exists, so convert it to the right type and return it.
                        return ConvertRawValue(rawValue, param.ParameterType);
                    });
                    continue;
                }

            }

            if (method.ReturnType == typeof(Task))
            {
                // This is an async method.
                return async (context) =>
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
                return async (context) =>
                {
                    await Task.Run(() =>
                    {
                        // Build parameter list
                        var paramsList = paramConverters.Select(converter => converter(context)).ToArray();
                        method.Invoke(instance, paramsList);
                    });
                };
            }

        }

    }
}
