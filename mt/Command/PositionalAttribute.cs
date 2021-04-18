using System;

namespace Mt.Command
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class PositionalAttribute : Attribute
    {
        private readonly int _index;
        private readonly string _argumentName;

        public PositionalAttribute(int index, string paramName = null)
        {
            _index = index;
            _argumentName = paramName;
        }

        public int Index => _index;
        public string ArgumentName => _argumentName;
    }
}
