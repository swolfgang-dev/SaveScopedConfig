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
        public const string PluginVersion = "0.3.1";

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
        private static void Postfix(SaveGameData saveGameData, int saveSlot)
        {
            if (saveGameData != null && saveGameData.playerData != null)
            {
                SaveScopedConfig.SetActiveSave(saveSlot, saveGameData.playerData.permadeathMode);
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
