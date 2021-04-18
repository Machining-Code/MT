using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mt.Command
{
    [AttributeUsage(AttributeTargets.Property)]
    public class OptionAttribute : Attribute
    {
        private readonly string _name;

        public OptionAttribute(string name)
        {
            _name = name;
        }

        public string OptionName => _name;

    }
}
