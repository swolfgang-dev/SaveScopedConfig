using BepInEx.Configuration;

namespace WheresWolfgang.SaveScopedConfig
{
    public sealed class SaveScopedConfigScope
    {
        private readonly ConfigFile config;
        private readonly string category;
        private readonly SaveScopedDisplayMode displayMode;

        internal SaveScopedConfigScope(ConfigFile config, string category, SaveScopedDisplayMode displayMode)
        {
            this.config = config;
            this.category = category;
            this.displayMode = displayMode;
        }

        public SaveScopedConfigEntry<T> Bind<T>(string key, T defaultValue, string description)
        {
            return Bind(key, defaultValue, new SaveScopedConfigDescription(description));
        }

        public SaveScopedConfigEntry<T> Bind<T>(string key, T defaultValue, ConfigDescription description)
        {
            return Bind(key, defaultValue, new SaveScopedConfigDescription(description));
        }

        public SaveScopedConfigEntry<T> Bind<T>(string key, T defaultValue, SaveScopedConfigDescription description)
        {
            return SaveScopedConfig.Bind(config, category, key, defaultValue, description, displayMode);
        }
    }
}
