using System;

namespace TypeMate.Core.Platform
{
    public readonly struct Hotcode
    {
        public uint Modifiers { get; }
        public uint Key { get; }
        public string Name { get; }

        public Hotcode(uint modifiers, uint key, string name)
        {
            Modifiers = modifiers;
            Key = key;
            Name = name;
        }
    }
}
