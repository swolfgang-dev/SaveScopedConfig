using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace WheresWolfgang.SaveScopedConfig
{
    public static class ModCompatibility
    {
        public const string TestedGameVersion = "1.0.30000";

        public static bool TryPatch(Harmony harmony, Assembly assembly, ManualLogSource log,
            out string status, params MemberInfo[] requiredMembers)
        {
            foreach (MemberInfo member in requiredMembers)
            {
                if (member == null)
                {
                    status = "Unavailable: a required game API is missing. Game " + Application.version;
                    log.LogError(status);
                    return false;
                }
            }
            try
            {
                harmony.PatchAll(assembly);
                status = "Ready (game " + Application.version + "; verified against " + TestedGameVersion + ")";
                if (Application.version != TestedGameVersion)
                    log.LogWarning("Game version differs from the tested version: " + status);
                return true;
            }
            catch (Exception error)
            {
                // Roll back this mod's hooks rather than leave a partially patched plugin.
                harmony.UnpatchSelf();
                status = "Unavailable: game hooks could not be installed. See BepInEx log.";
                log.LogError(status + " " + error);
                return false;
            }
        }
    }
}
