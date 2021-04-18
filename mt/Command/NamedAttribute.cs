using System;

namespace Mt.Command
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class NamedAttribute : Attribute
    {
        private readonly string _argumentName;
        private readonly bool _isOptional;

        public NamedAttribute()
        {

        }

        public NamedAttribute(string name, bool isOptional = false)
        {
            _argumentName = name;
            _isOptional = isOptional;
        }

        public string ArgumentName => _argumentName;
        public bool IsOptional => _isOptional;
    }
}
