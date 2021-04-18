using System;

namespace Mt.Command
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class NamedAttribute : Attribute
    {
        private readonly string _argumentName;

        public NamedAttribute()
        {

        }

        public NamedAttribute(string name)
        {
            _argumentName = name;
        }

        public string ArgumentName => _argumentName;
    }
}
