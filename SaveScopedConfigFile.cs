using BepInEx.Configuration;
using System;
using System.Collections.Generic;

namespace WheresWolfgang.SaveScopedConfig
{
    public sealed class SaveScopedConfigFile : IDisposable
    {
        private readonly ConfigFile config;
        private readonly SaveScopedDisplayMode displayMode;
        private readonly List<ISaveScopedConfigEntry> entries = new List<ISaveScopedConfigEntry>();
        private bool disposed;

        internal SaveScopedConfigFile(ConfigFile config, SaveScopedDisplayMode displayMode)
        {
            this.config = config;
            this.displayMode = displayMode;
        }

        public SaveScopedConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
        {
            return Bind(section, key, defaultValue, new SaveScopedConfigDescription(description));
        }

        public SaveScopedConfigEntry<T> Bind<T>(string section, string key, T defaultValue, ConfigDescription description)
        {
            return Bind(section, key, defaultValue, new SaveScopedConfigDescription(description));
        }

        public SaveScopedConfigEntry<T> Bind<T>(string section, string key, T defaultValue, SaveScopedConfigDescription description)
        {
            return Track(SaveScopedConfig.Bind(config, section, key, defaultValue, description, displayMode));
        }

        public SaveScopedConfigEntry<T> Bind<T>(ConfigDefinition definition, T defaultValue, string description)
        {
            return Track(SaveScopedConfig.Bind(config, definition, defaultValue, description, displayMode));
        }

        public SaveScopedConfigEntry<T> Bind<T>(ConfigDefinition definition, T defaultValue, ConfigDescription description)
        {
            return Track(SaveScopedConfig.Bind(config, definition, defaultValue, new SaveScopedConfigDescription(description), displayMode));
        }

        public SaveScopedConfigEntry<T> Bind<T>(ConfigDefinition definition, T defaultValue, SaveScopedConfigDescription description)
        {
            return Track(SaveScopedConfig.Bind(config, definition, defaultValue, description, displayMode));
        }

        public SaveScopedConfigEntry<T> Bind<T>(string key, T defaultValue, string description)
        {
            return Bind("General", key, defaultValue, new SaveScopedConfigDescription(description));
        }

        public SaveScopedConfigEntry<T> Bind<T>(string key, T defaultValue, ConfigDescription description)
        {
            return Bind("General", key, defaultValue, new SaveScopedConfigDescription(description));
        }

        public SaveScopedConfigEntry<T> Bind<T>(string key, T defaultValue, SaveScopedConfigDescription description)
        {
            return Bind("General", key, defaultValue, description);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (ISaveScopedConfigEntry entry in entries)
            {
                SaveScopedConfigPlugin.Unregister(entry);
            }

            entries.Clear();
        }

        private SaveScopedConfigEntry<T> Track<T>(SaveScopedConfigEntry<T> entry)
        {
            if (disposed)
            {
                throw new ObjectDisposedException("SaveScopedConfigFile");
            }

            entries.Add(entry);
            return entry;
        }
    }
}
