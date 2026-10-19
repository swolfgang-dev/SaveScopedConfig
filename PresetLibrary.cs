using System;
using System.Collections.Generic;
using System.Linq;

namespace WheresWolfgang.SaveScopedConfig
{
    /// <summary>Named presets stored in one existing config entry; no extra files.</summary>
    public static class PresetLibrary
    {
        public static Dictionary<string, string> Read(string data)
        {
            var presets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(data) || data.Length > 65536) return presets;
            foreach (string row in data.Split(';'))
            {
                int equals = row.IndexOf('=');
                if (equals <= 0) continue;
                try
                {
                    string name = Uri.UnescapeDataString(row.Substring(0, equals)).Trim();
                    string value = Uri.UnescapeDataString(row.Substring(equals + 1));
                    if (name.Length > 0 && name.Length <= 80 && value.Length <= 1024 && (presets.Count < 32 || presets.ContainsKey(name)))
                        presets[name] = value;
                }
                catch (UriFormatException) { }
            }
            return presets;
        }

        public static bool TrySave(string data, string name, string value, out string updated, out string error)
        {
            updated = data;
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0 || name.Length > 80)
            { error = "Enter a preset name of 1–80 characters."; return false; }
            if (value == null || value.Length > 1024)
            { error = "Preset data is invalid."; return false; }
            var presets = Read(data);
            if (!presets.ContainsKey(name) && presets.Count >= 32)
            { error = "There are already 32 presets. Delete one first."; return false; }
            presets[name] = value;
            string serialized = Write(presets);
            if (serialized.Length > 65536)
            { error = "Preset storage is full. Delete a preset or shorten its contents."; return false; }
            updated = serialized;
            error = null;
            return true;
        }

        public static string Delete(string data, string name)
        {
            var presets = Read(data);
            presets.Remove(name ?? string.Empty);
            return Write(presets);
        }

        private static string Write(Dictionary<string, string> presets)
        {
            return string.Join(";", presets.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value)));
        }
    }
}
