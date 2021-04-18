using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mt.Command
{
    public record Context
    {
        public string Name { get; init; }
        public IReadOnlyDictionary<string, object> Options { get; init; }
        public string[] PositionalArguments { get; init; }
        public IReadOnlyDictionary<string, string> NamedArguments { get; init; }
        public CancellationToken CancellationToken { get; init; }
    }
}
