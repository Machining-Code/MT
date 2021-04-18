using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Mt.Command
{
    internal record CommandInfo
    {
        public string Name { get; init; }
        public string Description { get; init; }
        public Func<Context, Task> Runner { get; init; }
        public ParameterInfo[] Parameters { get; init; }
        public int PositionalArgumentCount { get; init; }
    }
}
