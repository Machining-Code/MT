using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mt.Command
{
    [AttributeUsage(AttributeTargets.Method)]
    public class DescriptionAttribute : Attribute
    {
        private string _description;

        public DescriptionAttribute(string description)
        {
            _description = description;
        }

        public string Description => _description;
    }
}
