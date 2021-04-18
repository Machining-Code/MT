using System;

namespace Mt.Command
{
    [AttributeUsage(AttributeTargets.Method)]
    public class CommandAttribute : Attribute
    {
        private readonly string _commandName;

        public CommandAttribute(string name)
        {
            _commandName = name;
        }

        public CommandAttribute()
        {

        }

        public string CommandName => _commandName;
    }
}
