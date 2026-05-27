using BepInEx.Configuration;
using BepInEx;
using GlobalEnums;
using System;

namespace WheresWolfgang.SaveScopedConfig
{
    public static class SaveScopedConfig
    {
        public static event Action ActiveSaveChanged;
        public static event Action<int> SaveSlotResetToDefaults;
        public static event Action ConfigManagerRefreshed;

        public static string ActiveSaveKey { get; private set; }
        public static int ActiveSaveSlot { get; private set; }
        public static PermadeathModes ActiveSaveMode { get; private set; }

        public static string ActiveSaveLabel
        {
            get
            {
                return HasActiveSave ? GetModeName(ActiveSaveMode) : null;
            }
        }

        public static string ActiveSaveStatusLabel
        {
            get
            {
                return HasActiveSave ? GetStatusModeName(ActiveSaveMode) : null;
            }
        }

        public static bool HasActiveSave
        {
            get { return !string.IsNullOrEmpty(ActiveSaveKey); }
        }

        public static SaveScopedConfigScope CreateScope(ConfigFile config, string category, SaveScopedDisplayMode displayMode = SaveScopedDisplayMode.SavePrefixedCategories)
        {
            return new SaveScopedConfigScope(config, category, displayMode);
        }

        public static SaveScopedConfigFile For(ConfigFile config, SaveScopedDisplayMode displayMode = SaveScopedDisplayMode.SavePrefixedCategories)
        {
            return new SaveScopedConfigFile(config, displayMode);
        }

        public static SaveScopedConfigFile For(BaseUnityPlugin plugin, SaveScopedDisplayMode displayMode = SaveScopedDisplayMode.SavePrefixedCategories)
        {
            return new SaveScopedConfigFile(plugin.Config, displayMode);
        }

        public static SaveScopedConfigEntry<T> Bind<T>(ConfigFile config, string category, string key, T defaultValue, string description)
        {
            return Bind(config, category, key, defaultValue, new SaveScopedConfigDescription(description), SaveScopedDisplayMode.SavePrefixedCategories);
        }

        public static SaveScopedConfigEntry<T> Bind<T>(ConfigFile config, string category, string key, T defaultValue, ConfigDescription description)
        {
            return Bind(config, category, key, defaultValue, new SaveScopedConfigDescription(description), SaveScopedDisplayMode.SavePrefixedCategories);
        }

        public static SaveScopedConfigEntry<T> Bind<T>(ConfigFile config, string category, string key, T defaultValue, SaveScopedConfigDescription description, SaveScopedDisplayMode displayMode = SaveScopedDisplayMode.SavePrefixedCategories)
        {
            SaveScopedConfigEntry<T> entry = new SaveScopedConfigEntry<T>(config, category, key, defaultValue, description, displayMode);
            SaveScopedConfigPlugin.Register(entry);
            return entry;
        }

        public static SaveScopedConfigEntry<T> Bind<T>(ConfigFile config, ConfigDefinition definition, T defaultValue, string description)
        {
            return Bind(config, definition.Section, definition.Key, defaultValue, description);
        }

        public static SaveScopedConfigEntry<T> Bind<T>(ConfigFile config, ConfigDefinition definition, T defaultValue, string description, SaveScopedDisplayMode displayMode)
        {
            return Bind(config, definition.Section, definition.Key, defaultValue, new SaveScopedConfigDescription(description), displayMode);
        }

        public static SaveScopedConfigEntry<T> Bind<T>(ConfigFile config, ConfigDefinition definition, T defaultValue, ConfigDescription description)
        {
            return Bind(config, definition.Section, definition.Key, defaultValue, description);
        }

        public static SaveScopedConfigEntry<T> Bind<T>(ConfigFile config, ConfigDefinition definition, T defaultValue, ConfigDescription description, SaveScopedDisplayMode displayMode)
        {
            return Bind(config, definition.Section, definition.Key, defaultValue, new SaveScopedConfigDescription(description), displayMode);
        }

        public static SaveScopedConfigEntry<T> Bind<T>(ConfigFile config, ConfigDefinition definition, T defaultValue, SaveScopedConfigDescription description, SaveScopedDisplayMode displayMode = SaveScopedDisplayMode.SavePrefixedCategories)
        {
            return Bind(config, definition.Section, definition.Key, defaultValue, description, displayMode);
        }

        public static SaveScopedConfigEntry<T> Bind<T>(ConfigFile config, string key, T defaultValue, string description)
        {
            return Bind(config, "General", key, defaultValue, description);
        }

        public static SaveScopedConfigEntry<T> Bind<T>(ConfigFile config, string key, T defaultValue, ConfigDescription description)
        {
            return Bind(config, "General", key, defaultValue, description);
        }

        public static SaveScopedConfigEntry<T> Bind<T>(ConfigFile config, string key, T defaultValue, SaveScopedConfigDescription description, SaveScopedDisplayMode displayMode = SaveScopedDisplayMode.SavePrefixedCategories)
        {
            return Bind(config, "General", key, defaultValue, description, displayMode);
        }

        public static void RefreshConfigManager(bool reopenWindow)
        {
            SaveScopedConfigPlugin.RefreshConfigManager(reopenWindow);
        }

        internal static void SetActiveSave(int saveSlot, PermadeathModes mode)
        {
            string saveKey = GetSaveKey(saveSlot);
            if (saveKey == ActiveSaveKey && mode == ActiveSaveMode)
            {
                return;
            }

            ActiveSaveKey = saveKey;
            ActiveSaveSlot = saveSlot;
            ActiveSaveMode = mode;
            SaveScopedConfigPlugin.ApplyActiveSave();
            RaiseActiveSaveChanged();
        }

        internal static void ClearActiveSave()
        {
            if (ActiveSaveKey == null)
            {
                return;
            }

            ActiveSaveKey = null;
            ActiveSaveSlot = 0;
            ActiveSaveMode = PermadeathModes.Off;
            SaveScopedConfigPlugin.ApplyActiveSave();
            RaiseActiveSaveChanged();
        }

        internal static string GetSaveKey(int saveSlot)
        {
            return "Save " + saveSlot;
        }

        public static string GetModeName(PermadeathModes mode)
        {
            if (mode == PermadeathModes.On)
            {
                return "Steel Soul";
            }

            if (mode == PermadeathModes.Dead)
            {
                return "Steel Soul Dead";
            }

            return "Normal";
        }

        public static string GetStatusModeName(PermadeathModes mode)
        {
            if (mode == PermadeathModes.Dead)
            {
                return "Steel Soul (Dead)";
            }

            return GetModeName(mode);
        }

        private static void RaiseActiveSaveChanged()
        {
            Action handler = ActiveSaveChanged;
            if (handler != null)
            {
                handler();
            }
        }

        internal static void RaiseSaveSlotResetToDefaults(int saveSlot)
        {
            Action<int> handler = SaveSlotResetToDefaults;
            if (handler != null)
            {
                handler(saveSlot);
            }
        }

        internal static void RaiseConfigManagerRefreshed()
        {
            Action handler = ConfigManagerRefreshed;
            if (handler != null)
            {
                handler();
            }
        }
    }
}
