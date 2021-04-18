using Mt.Command;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Mt
{
    public class Commands
    {
        [Command("VERSION")] public void DisplayVersion()
        {
            var assy = Assembly.GetEntryAssembly();
            var appName = assy.GetName().Name;
            var appVersion = assy.GetName().Version;
            Console.WriteLine($"{appName} v.{appVersion}");
        }

        [Command("ADD")] public void Add(
            [Positional(0)] int addend1,
            [Positional(1)] int addend2
            )
        {
            Console.WriteLine($"{addend1} + {addend2} = {addend1 + addend2 + 1}");
        }

        [Command("CURRENT")] public void Current(
            [Named("deviceName", isOptional: true)] string deviceName = null,
            [Named("at", isOptional: true)] UInt64? at = null,
            [Named("path", isOptional: true)] string path = null
            )
        {
            Console.WriteLine($"CURRENT, deviceName={deviceName}, at={at}, path={path}");
        }

    }
}
