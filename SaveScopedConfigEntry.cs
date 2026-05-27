using BepInEx.Configuration;
using System;
using System.Collections.Generic;

namespace WheresWolfgang.SaveScopedConfig
{
    public sealed class SaveScopedConfigEntry<T> : ISaveScopedConfigEntry
    {
        private readonly ConfigFile config;
        private readonly string ownerId;
        private readonly string category;
        private readonly string key;
        private readonly T defaultValue;
        private readonly SaveScopedConfigDescription description;
        private readonly SaveScopedDisplayMode displayMode;
        private readonly Dictionary<string, ConfigEntry<T>> entries = new Dictionary<string, ConfigEntry<T>>();

        internal SaveScopedConfigEntry(ConfigFile config, string category, string key, T defaultValue, SaveScopedConfigDescription description, SaveScopedDisplayMode displayMode)
        {
            this.config = config;
            ownerId = GetOwnerId(config);
            this.category = category;
            this.key = key;
            this.defaultValue = defaultValue;
            this.description = description ?? new SaveScopedConfigDescription(string.Empty);
            this.displayMode = displayMode;
        }

        public event EventHandler SettingChanged;

        public ConfigEntry<T> CurrentEntry { get; private set; }

        public T Value
        {
            get
            {
                return CurrentEntry != null ? CurrentEntry.Value : defaultValue;
            }
            set
            {
                if (CurrentEntry != null)
                {
                    CurrentEntry.Value = value;
                }
            }
        }

        public string OwnerId
        {
            get { return ownerId; }
        }

        public string Category
        {
            get { return category; }
        }

        public string Key
        {
            get { return key; }
        }

        public Type SettingType
        {
            get { return typeof(T); }
        }

        public void ApplyActiveSave()
        {
            HideAllEntries();

            string saveKey = SaveScopedConfig.ActiveSaveKey;
            if (string.IsNullOrEmpty(saveKey))
            {
                CurrentEntry = null;
                return;
            }

            CurrentEntry = GetOrCreateEntry(saveKey);
            ApplyDisplayRules(CurrentEntry);
        }

        public void Detach()
        {
            HideAllEntries();
            foreach (ConfigEntry<T> entry in entries.Values)
            {
                entry.SettingChanged -= OnEntryChanged;
            }

            CurrentEntry = null;
        }

        public void ResetForSaveSlot(int saveSlot)
        {
            ResetForSaveKey(SaveScopedConfig.GetSaveKey(saveSlot));
            config.Save();
            ApplyActiveSave();
        }

        private ConfigEntry<T> GetOrCreateEntry(string saveKey)
        {
            ConfigEntry<T> entry;
            if (entries.TryGetValue(saveKey, out entry))
            {
                return entry;
            }

            T initialValue = defaultValue;

            entry = config.Bind(
                GetSection(saveKey),
                key,
                initialValue,
                CreateDescription());
            entry.SettingChanged += OnEntryChanged;
            entries.Add(saveKey, entry);
            return entry;
        }

        private ConfigDescription CreateDescription()
        {
            ConfigurationManagerAttributes attributes = new ConfigurationManagerAttributes
            {
                Browsable = false,
                Category = GetDisplayCategory(),
                ReadOnly = string.IsNullOrEmpty(category) ? true : (bool?)null,
                HideDefaultButton = string.IsNullOrEmpty(category) ? true : (bool?)null,
                HideSettingName = string.IsNullOrEmpty(category) ? true : (bool?)null,
                DispName = EvaluateDisplayName(),
                Order = EvaluateOrder(),
            };

            ConfigDescription configDescription = description.ConfigDescription;
            if (configDescription == null)
            {
                return new ConfigDescription(string.Empty, null, attributes);
            }

            object[] tags = new object[configDescription.Tags.Length + 1];
            configDescription.Tags.CopyTo(tags, 0);
            tags[tags.Length - 1] = attributes;
            return new ConfigDescription(configDescription.Description, configDescription.AcceptableValues, tags);
        }

        private void ApplyDisplayRules(ConfigEntry<T> entry)
        {
            ConfigurationManagerAttributes attributes = GetAttributes(entry);
            if (attributes == null)
            {
                return;
            }

            attributes.Browsable = EvaluateVisibleWhen();
            attributes.DispName = EvaluateDisplayName();
            attributes.Order = EvaluateOrder();
        }

        private bool EvaluateVisibleWhen()
        {
            if (description.VisibleWhen == null)
            {
                return true;
            }

            try
            {
                return description.VisibleWhen();
            }
            catch (Exception ex)
            {
                SaveScopedConfigPlugin.Log.LogWarning("VisibleWhen failed for " + category + "/" + key + ": " + ex.Message);
                return false;
            }
        }

        private string EvaluateDisplayName()
        {
            if (description.DisplayName == null)
            {
                return null;
            }

            try
            {
                return description.DisplayName();
            }
            catch (Exception ex)
            {
                SaveScopedConfigPlugin.Log.LogWarning("DisplayName failed for " + category + "/" + key + ": " + ex.Message);
                return null;
            }
        }

        private int? EvaluateOrder()
        {
            if (description.Order == null)
            {
                return null;
            }

            try
            {
                return description.Order();
            }
            catch (Exception ex)
            {
                SaveScopedConfigPlugin.Log.LogWarning("Order failed for " + category + "/" + key + ": " + ex.Message);
                return null;
            }
        }

        private void ResetForSaveKey(string saveKey)
        {
            ConfigEntry<T> entry = GetOrCreateEntry(saveKey);
            entry.Value = defaultValue;
            SetBrowsable(entry, false);
        }

        private void HideAllEntries()
        {
            foreach (ConfigEntry<T> entry in entries.Values)
            {
                SetBrowsable(entry, false);
            }
        }

        private void SetBrowsable(ConfigEntry<T> entry, bool browsable)
        {
            ConfigurationManagerAttributes attributes = GetAttributes(entry);
            if (attributes != null)
            {
                attributes.Browsable = browsable;
            }
        }

        private ConfigurationManagerAttributes GetAttributes(ConfigEntry<T> entry)
        {
            object[] tags = entry.Description.Tags;
            for (int i = 0; i < tags.Length; i++)
            {
                ConfigurationManagerAttributes attributes = tags[i] as ConfigurationManagerAttributes;
                if (attributes != null)
                {
                    return attributes;
                }
            }

            return null;
        }

        private string GetSection(string saveKey)
        {
            if (string.IsNullOrEmpty(category))
            {
                return saveKey;
            }

            return saveKey + ": " + category;
        }

        private string GetDisplayCategory()
        {
            if (displayMode == SaveScopedDisplayMode.PlainCategories)
            {
                return category;
            }

            if (string.IsNullOrEmpty(category))
            {
                return GetActiveSaveDisplayName();
            }

            return GetActiveSaveDisplayName() + ": " + category;
        }

        private string GetActiveSaveDisplayName()
        {
            return SaveScopedConfig.HasActiveSave ? "Save " + SaveScopedConfig.ActiveSaveSlot : "Save";
        }

        private void OnEntryChanged(object sender, EventArgs args)
        {
            EventHandler handler = SettingChanged;
            if (handler != null)
            {
                handler(this, args);
            }
        }

        private static string GetOwnerId(ConfigFile config)
        {
            return config.ConfigFilePath;
        }
    }
}
