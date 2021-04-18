using Mt.Command;

namespace Mt
{
    public record Options
    {
        [Option("Verbose")] public bool Verbose { get; set; }
        [Option("HeaderOnly")] public bool HeaderOnly { get; set; }
        [Option("Format")] public Format Format { get; set; }
    }
}
