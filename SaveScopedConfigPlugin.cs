using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace WheresWolfgang.SaveScopedConfig
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SaveScopedConfigPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "whereswolfgang.savescopedconfig";
        public const string PluginName = "Save Scoped Config";
        public const string PluginVersion = "0.3.2";

        private static readonly List<ISaveScopedConfigEntry> Entries = new List<ISaveScopedConfigEntry>();

        private Harmony harmony;
        private ConfigEntry<bool> resetSettingsOnDeleteSave;
        private ConfigEntry<string> activeSaveStatus;
        private ConfigEntry<string> steelSoulStatus;

        internal static SaveScopedConfigPlugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Config.Remove(new ConfigDefinition("Settings", "Clear Saved Config On Delete Save"));
            resetSettingsOnDeleteSave = Config.Bind(
                "Settings",
                "Reset Saved Config On Delete Save",
                false,
                "When enabled, deleting a save resets saved config for that slot to default values. When disabled, saved config is kept and will be reused by a new save in the same slot, regardless of save type.");
            activeSaveStatus = Config.Bind(
                "Status",
                "Active Save",
                "No active save",
                new ConfigDescription(
                    "Save slot currently receiving save-scoped settings.",
                    null,
                    new ConfigurationManagerAttributes { ReadOnly = true, HideDefaultButton = true }));
            steelSoulStatus = Config.Bind(
                "Status",
                "Steel Soul",
                "No active save",
                new ConfigDescription(
                    "Save type currently receiving save-scoped settings.",
                    null,
                    new ConfigurationManagerAttributes { ReadOnly = true, HideDefaultButton = true }));
            UpdateStatusEntries();

            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(SaveScopedConfigPlugin).Assembly);
            Logger.LogInfo(PluginName + " loaded.");
        }

        private void OnDestroy()
        {
            if (harmony != null)
            {
                harmony.UnpatchSelf();
                harmony = null;
            }

            if (Instance == this)
            {
                Instance = null;
                Log = null;
            }
        }

        internal static void Register(ISaveScopedConfigEntry entry)
        {
            RemoveMatchingEntry(entry);
            Entries.Add(entry);
            entry.ApplyActiveSave();
            RefreshConfigManager(true);
        }

        internal static void Unregister(ISaveScopedConfigEntry entry)
        {
            if (Entries.Remove(entry))
            {
                entry.Detach();
                RefreshConfigManager(true);
            }
        }

        internal static void UnregisterOwner(string ownerId)
        {
            bool removedAny = false;
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                ISaveScopedConfigEntry entry = Entries[i];
                if (entry.OwnerId == ownerId)
                {
                    Entries.RemoveAt(i);
                    entry.Detach();
                    removedAny = true;
                }
            }

            if (removedAny)
            {
                RefreshConfigManager(true);
            }
        }

        private static void RemoveMatchingEntry(ISaveScopedConfigEntry newEntry)
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                ISaveScopedConfigEntry existingEntry = Entries[i];
                if (existingEntry.OwnerId == newEntry.OwnerId &&
                    existingEntry.Category == newEntry.Category &&
                    existingEntry.Key == newEntry.Key &&
                    existingEntry.SettingType == newEntry.SettingType)
                {
                    Entries.RemoveAt(i);
                    existingEntry.Detach();
                }
            }
        }

        internal static void ApplyActiveSave()
        {
            foreach (ISaveScopedConfigEntry entry in Entries)
            {
                try
                {
                    entry.ApplyActiveSave();
                }
                catch (Exception ex)
                {
                    Log.LogError("Failed to apply save-scoped setting " + entry.Category + "/" + entry.Key + ": " + ex);
                }
            }

            if (Instance != null)
            {
                Instance.UpdateStatusEntries();
                Log.LogInfo(SaveScopedConfig.HasActiveSave ? "Showing settings for " + SaveScopedConfig.ActiveSaveKey + "." : "No active save; hiding save-specific settings.");
            }

            RefreshConfigManager(true);
        }

        internal static void ClearSaveConfigForSlot(int saveSlot)
        {
            if (Instance != null && !Instance.resetSettingsOnDeleteSave.Value)
            {
                Log.LogInfo("Preserved save-scoped settings for save slot " + saveSlot + " because Reset Saved Config On Delete Save is disabled.");
                return;
            }

            foreach (ISaveScopedConfigEntry entry in Entries)
            {
                entry.ResetForSaveSlot(saveSlot);
            }

            RefreshConfigManager(true);
            SaveScopedConfig.RaiseSaveSlotResetToDefaults(saveSlot);
            Log.LogInfo("Reset save-scoped settings for save slot " + saveSlot + " to defaults.");
        }

        private void UpdateStatusEntries()
        {
            activeSaveStatus.Value = SaveScopedConfig.HasActiveSave ? "Save " + SaveScopedConfig.ActiveSaveSlot : "No active save";
            steelSoulStatus.Value = SaveScopedConfig.HasActiveSave ? SaveScopedConfig.ActiveSaveStatusLabel : "No active save";
        }

        internal static void RefreshConfigManager(bool reopenWindow)
        {
            Type managerType = Type.GetType("ConfigurationManager.ConfigurationManager, ConfigurationManager");
            if (managerType == null)
            {
                return;
            }

            UnityEngine.Object manager = UnityEngine.Object.FindFirstObjectByType(managerType);
            if (!manager)
            {
                return;
            }

            MethodInfo buildSettingList = managerType.GetMethod("BuildSettingList", BindingFlags.Instance | BindingFlags.Public);
            if (buildSettingList != null)
            {
                buildSettingList.Invoke(manager, null);
            }

            SortConfigManagerCategoryGroups(managerType, manager);
            SaveScopedConfig.RaiseConfigManagerRefreshed();

            if (!reopenWindow)
            {
                return;
            }

            PropertyInfo displayingWindow = managerType.GetProperty("DisplayingWindow", BindingFlags.Instance | BindingFlags.Public);
            if (displayingWindow == null || !displayingWindow.CanRead || !displayingWindow.CanWrite)
            {
                return;
            }

            bool wasOpen = (bool)displayingWindow.GetValue(manager, null);
            PropertyInfo windowRect = managerType.GetProperty("SettingWindowRect", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object savedWindowRect = windowRect != null && windowRect.CanRead ? windowRect.GetValue(manager, null) : null;
            FieldInfo scrollPosition = managerType.GetField("_settingWindowScrollPos", BindingFlags.Instance | BindingFlags.NonPublic);
            object savedScrollPosition = scrollPosition != null ? scrollPosition.GetValue(manager) : null;
            Dictionary<FieldInfo, object> savedFields = CaptureConfigManagerFields(managerType, manager, "_windowWasMoved", "_tipsWindowWasMoved", "_windowYmax");

            displayingWindow.SetValue(manager, false, null);

            if (wasOpen && Instance != null)
            {
                Instance.StartCoroutine(ReopenConfigManagerNextFrame(manager, displayingWindow, windowRect, savedWindowRect, scrollPosition, savedScrollPosition, savedFields));
            }
        }

        private static IEnumerator ReopenConfigManagerNextFrame(
            UnityEngine.Object manager,
            PropertyInfo displayingWindow,
            PropertyInfo windowRect,
            object savedWindowRect,
            FieldInfo scrollPosition,
            object savedScrollPosition,
            Dictionary<FieldInfo, object> savedFields)
        {
            yield return null;

            if (manager)
            {
                RestoreConfigManagerWindowState(manager, windowRect, savedWindowRect, scrollPosition, savedScrollPosition, savedFields);
                displayingWindow.SetValue(manager, true, null);
                RestoreConfigManagerWindowState(manager, windowRect, savedWindowRect, scrollPosition, savedScrollPosition, savedFields);
            }
        }

        private static void RestoreConfigManagerWindowState(
            UnityEngine.Object manager,
            PropertyInfo windowRect,
            object savedWindowRect,
            FieldInfo scrollPosition,
            object savedScrollPosition,
            Dictionary<FieldInfo, object> savedFields)
        {
            if (windowRect != null && windowRect.CanWrite && savedWindowRect != null)
            {
                windowRect.SetValue(manager, savedWindowRect, null);
            }

            if (scrollPosition != null && savedScrollPosition != null)
            {
                scrollPosition.SetValue(manager, savedScrollPosition);
            }

            foreach (KeyValuePair<FieldInfo, object> savedField in savedFields)
            {
                savedField.Key.SetValue(manager, savedField.Value);
            }
        }

        private static Dictionary<FieldInfo, object> CaptureConfigManagerFields(Type managerType, UnityEngine.Object manager, params string[] fieldNames)
        {
            Dictionary<FieldInfo, object> values = new Dictionary<FieldInfo, object>();
            foreach (string fieldName in fieldNames)
            {
                FieldInfo field = managerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    values.Add(field, field.GetValue(manager));
                }
            }

            return values;
        }

        internal static void SortConfigManagerCategoryGroups(Type managerType, object manager)
        {
            foreach (FieldInfo field in managerType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                object value = field.GetValue(manager);
                SortPluginSettingsData(value);

                IEnumerable enumerable = value as IEnumerable;
                if (enumerable == null || value is string)
                {
                    continue;
                }

                foreach (object item in enumerable)
                {
                    SortPluginSettingsData(item);

                    object dictionaryValue = GetDictionaryValue(item);
                    if (dictionaryValue != null)
                    {
                        SortPluginSettingsData(dictionaryValue);
                    }
                }
            }
        }

        private static object GetDictionaryValue(object item)
        {
            if (item is DictionaryEntry)
            {
                return ((DictionaryEntry)item).Value;
            }

            if (item == null)
            {
                return null;
            }

            PropertyInfo valueProperty = item.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
            return valueProperty == null || !valueProperty.CanRead ? null : valueProperty.GetValue(item, null);
        }

        private static void SortPluginSettingsData(object pluginSettingsData)
        {
            if (pluginSettingsData == null)
            {
                return;
            }

            FieldInfo categoriesField = pluginSettingsData.GetType().GetField("Categories", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (categoriesField == null)
            {
                return;
            }

            IList categories = categoriesField.GetValue(pluginSettingsData) as IList;
            if (categories == null || categories.Count < 2 || categories.IsReadOnly || categories.IsFixedSize)
            {
                return;
            }

            List<CategorySortEntry> sortedCategories = new List<CategorySortEntry>();
            for (int i = 0; i < categories.Count; i++)
            {
                object category = categories[i];
                sortedCategories.Add(new CategorySortEntry(category, i, GetCategorySortOrder(category)));
            }

            sortedCategories.Sort(CompareCategorySortEntries);

            for (int i = 0; i < sortedCategories.Count; i++)
            {
                categories[i] = sortedCategories[i].Category;
            }
        }

        private static int CompareCategorySortEntries(CategorySortEntry left, CategorySortEntry right)
        {
            int orderComparison = right.Order.CompareTo(left.Order);
            return orderComparison != 0 ? orderComparison : left.Index.CompareTo(right.Index);
        }

        private static int GetCategorySortOrder(object category)
        {
            return GetCategoryScopeOrder(category) + GetCategoryOrder(category);
        }

        private static int GetCategoryScopeOrder(object category)
        {
            string name = GetCategoryName(category);
            if (name == null)
            {
                return 0;
            }

            if (name.StartsWith("Global:", StringComparison.Ordinal))
            {
                return 1000000;
            }

            if (name.StartsWith("Save ", StringComparison.Ordinal))
            {
                return 0;
            }

            return 0;
        }

        private static string GetCategoryName(object category)
        {
            if (category == null)
            {
                return null;
            }

            FieldInfo nameField = category.GetType().GetField("Name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return nameField == null ? null : nameField.GetValue(category) as string;
        }

        private static int GetCategoryOrder(object category)
        {
            if (category == null)
            {
                return int.MinValue;
            }

            FieldInfo settingsField = category.GetType().GetField("Settings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            IEnumerable settings = settingsField == null ? null : settingsField.GetValue(category) as IEnumerable;
            if (settings == null)
            {
                return int.MinValue;
            }

            int order = int.MinValue;
            foreach (object setting in settings)
            {
                int? settingOrder = GetSettingOrder(setting);
                if (settingOrder.HasValue && settingOrder.Value > order)
                {
                    order = settingOrder.Value;
                }
            }

            return order;
        }

        private static int? GetSettingOrder(object setting)
        {
            if (setting == null)
            {
                return null;
            }

            PropertyInfo orderProperty = setting.GetType().GetProperty("Order", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (orderProperty == null || !orderProperty.CanRead)
            {
                return null;
            }

            object order = orderProperty.GetValue(setting, null);
            if (order is int)
            {
                return (int)order;
            }

            return null;
        }

        private sealed class CategorySortEntry
        {
            public CategorySortEntry(object category, int index, int order)
            {
                Category = category;
                Index = index;
                Order = order;
            }

            public object Category { get; private set; }
            public int Index { get; private set; }
            public int Order { get; private set; }
        }
    }

    [HarmonyPatch]
    internal static class ConfigurationManagerCategorySortPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type managerType = Type.GetType("ConfigurationManager.ConfigurationManager, ConfigurationManager");
            if (managerType == null)
            {
                yield break;
            }

            MethodInfo buildSettingList = managerType.GetMethod("BuildSettingList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (buildSettingList != null)
            {
                yield return buildSettingList;
            }

            MethodInfo buildFilteredSettingList = managerType.GetMethod("BuildFilteredSettingList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (buildFilteredSettingList != null)
            {
                yield return buildFilteredSettingList;
            }
        }

        private static void Postfix(object __instance)
        {
            if (__instance != null)
            {
                SaveScopedConfigPlugin.SortConfigManagerCategoryGroups(__instance.GetType(), __instance);
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), "ClearSaveFile")]
    internal static class GameManagerClearSaveFilePatch
    {
        private static void Prefix(int saveSlot, ref Action<bool> callback)
        {
            Action<bool> originalCallback = callback;
            callback = delegate(bool didClear)
            {
                if (didClear)
                {
                    SaveScopedConfigPlugin.ClearSaveConfigForSlot(saveSlot);
                }

                if (originalCallback != null)
                {
                    originalCallback(didClear);
                }
            };
        }
    }

    [HarmonyPatch(typeof(GameManager), "SetLoadedGameData", new Type[] { typeof(SaveGameData), typeof(int) })]
    internal static class GameManagerSetLoadedGameDataPatch
    {
        private static void Postfix(GameManager __instance, SaveGameData saveGameData, int saveSlot)
        {
            if (saveGameData != null && saveGameData.playerData != null)
            {
                // Save deserialization may run on the game's async save worker.
                // Config refresh and consumer callbacks use Unity objects and must run on the game thread.
                var mode = saveGameData.playerData.permadeathMode;
                var loadedPlayerData = saveGameData.playerData;
                CoreLoop.InvokeSafe(() =>
                {
                    // A newer load or return to menu can supersede this queued update.
                    if (__instance != null && __instance.profileID == saveSlot &&
                        ReferenceEquals(__instance.playerData, loadedPlayerData))
                    {
                        SaveScopedConfig.SetActiveSave(saveSlot, mode);
                    }
                });
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), "StartNewGame", new Type[] { typeof(bool), typeof(bool) })]
    internal static class GameManagerStartNewGamePatch
    {
        private static void Postfix(GameManager __instance)
        {
            if (__instance != null && __instance.playerData != null)
            {
                SaveScopedConfig.SetActiveSave(__instance.profileID, __instance.playerData.permadeathMode);
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), "ReturnToMainMenu", new Type[] { typeof(bool), typeof(Action<bool>), typeof(bool), typeof(bool) })]
    internal static class GameManagerReturnToMainMenuPatch
    {
        private static void Prefix()
        {
            SaveScopedConfig.ClearActiveSave();
        }
    }
}
