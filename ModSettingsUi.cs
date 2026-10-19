using System;
using BepInEx.Configuration;
using UnityEngine;

namespace WheresWolfgang.SaveScopedConfig
{
    /// <summary>Configuration Manager panels that do not persist changing status text.</summary>
    public static class ModSettingsUi
    {
        public static void BindPanel(ConfigFile config, string name, Action draw, string description)
        {
            config.Bind("Controls and Status", name, string.Empty,
                new ConfigDescription(description, null, new ConfigurationManagerAttributes
                {
                    HideDefaultButton = true, HideSettingName = true, Order = 10000,
                    CustomDrawer = entry =>
                    {
                        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                        try { draw(); }
                        finally { GUILayout.EndVertical(); }
                    },
                }));
        }

        public static string ExplainEnablement(bool acquired, bool global, bool save)
        {
            string source = global && save ? "global and this save" : global ? "global" : save ? "this save" : null;
            if (acquired) return source == null ? "Available normally" : "Available normally; also enabled by " + source;
            return source == null ? "Not enabled" : "Enabled by " + source;
        }

        public static void Label(string text)
        {
            GUILayout.Label(text, new GUIStyle(GUI.skin.label) { wordWrap = true });
        }

        public static bool Button(string label, bool available = true)
        {
            bool previous = GUI.enabled;
            try { GUI.enabled = previous && available; return GUILayout.Button(label, new GUIStyle(GUI.skin.button) { wordWrap = true }); }
            finally { GUI.enabled = previous; }
        }
    }
}
