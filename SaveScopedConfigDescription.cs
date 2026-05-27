using BepInEx.Configuration;
using System;

namespace WheresWolfgang.SaveScopedConfig
{
    public sealed class SaveScopedConfigDescription
    {
        public SaveScopedConfigDescription(string description)
            : this(new ConfigDescription(description))
        {
        }

        public SaveScopedConfigDescription(string description, AcceptableValueBase acceptableValues, params object[] tags)
            : this(new ConfigDescription(description, acceptableValues, tags))
        {
        }

        public SaveScopedConfigDescription(ConfigDescription configDescription)
        {
            ConfigDescription = configDescription ?? new ConfigDescription(string.Empty);
        }

        public ConfigDescription ConfigDescription { get; private set; }
        public Func<bool> VisibleWhen { get; set; }
        public Func<string> DisplayName { get; set; }
        public Func<int?> Order { get; set; }
    }
}
