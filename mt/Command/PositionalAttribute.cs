using System;

namespace Mt.Command
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class PositionalAttribute : Attribute
    {
        private readonly int _index;
        private readonly string _argumentName;

        public PositionalAttribute(int index, string argumentName = null)
        {
            _index = index;
            _argumentName = argumentName;
        }

        public int Index => _index;
        public string ArgumentName => _argumentName;
    }
}
