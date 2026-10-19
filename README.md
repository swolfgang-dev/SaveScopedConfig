# Save Scoped Config

Save Scoped Config is a small BepInEx helper plugin for Silksong mods that want config values to follow the currently loaded save instead of being global.

It watches save load, new game, return-to-menu, and save erase events. In Configuration Manager it keeps the mod entry visible, shows only the active save's scoped settings, and refreshes the window when the active save changes.

Current API version: `0.4.0`.

## Install

Build or install `SaveScopedConfig.dll` into `BepInEx/plugins`.

Consumer mods should declare the dependency:

```csharp
[BepInDependency(SaveScopedConfigPlugin.PluginGuid)]
```

And import:

```csharp
using WheresWolfgang.SaveScopedConfig;
```

## Basic Usage

Normal BepInEx config:

```csharp
private ConfigEntry<bool> enabled;

private void Awake()
{
    enabled = Config.Bind("General", "Enabled", true, "Enable this mod.");
}
```

Save-scoped config:

```csharp
private SaveScopedConfigFile saveConfig;
private SaveScopedConfigEntry<bool> enabled;

private void Awake()
{
    saveConfig = SaveScopedConfig.For(this);
    enabled = saveConfig.Bind("General", "Enabled", true, "Enable this mod for this save.");
}

private void OnDestroy()
{
    saveConfig.Dispose();
}
```

Use it like a normal config entry:

```csharp
if (enabled.Value)
{
    // Current save has this enabled.
}
```

Developers do not need to store one value per save. A `SaveScopedConfigEntry<T>` always points at the active save's underlying `ConfigEntry<T>`.

When no save is active, `Value` returns the default value and writes are ignored.

## Supported Bind Forms

```csharp
saveConfig.Bind("Section", "Key", defaultValue, "Description");
saveConfig.Bind("Section", "Key", defaultValue, configDescription);
saveConfig.Bind("Section", "Key", defaultValue, saveScopedDescription);
saveConfig.Bind(configDefinition, defaultValue, "Description");
saveConfig.Bind(configDefinition, defaultValue, configDescription);
saveConfig.Bind(configDefinition, defaultValue, saveScopedDescription);
```

Convenience overload:

```csharp
saveConfig.Bind("Enabled", true, "Enable this mod for this save.");
saveConfig.Bind("Enabled", true, configDescription);
saveConfig.Bind("Enabled", true, saveScopedDescription);
```

The convenience overload uses `General` as the section.

## Display Metadata

Use `SaveScopedConfigDescription` for Configuration Manager metadata that depends on the active save:

```csharp
saveConfig.Bind(
    "Exploration (Yellow)",
    "Shell Satchel",
    false,
    new SaveScopedConfigDescription("Force Shell Satchel for this save.")
    {
        VisibleWhen = () => SaveScopedConfig.ActiveSaveMode != PermadeathModes.Off,
        DisplayName = () => "Shell Satchel",
        Order = () => 10,
    });
```

`VisibleWhen`, `DisplayName`, and `Order` are reevaluated when the active save changes.

Display metadata does not change the storage key. For example, a setting can display as `Druids Eyes` after an upgrade is unlocked while still being stored under the stable key `Druids Eye`.

Complete example with visibility, display name, and ordering:

```csharp
private SaveScopedConfigEntry<bool> shellSatchel;

private void Awake()
{
    SaveScopedConfigFile saveConfig = SaveScopedConfig.For(this);

    shellSatchel = saveConfig.Bind(
        "Exploration",
        "Shell Satchel",
        false,
        new SaveScopedConfigDescription("Enable Shell Satchel behavior for this save.")
        {
            VisibleWhen = () => SaveScopedConfig.ActiveSaveMode != PermadeathModes.Off,
            DisplayName = () => SaveScopedConfig.ActiveSaveMode == PermadeathModes.Off ? "Dead Bugs Purse" : "Shell Satchel",
            Order = () => 20,
        });
}
```

## Category Display

By default, Configuration Manager categories are save-prefixed:

```text
Save 1: General
Save 1: Exploration (Yellow)
```

To show plain categories instead:

```csharp
saveConfig = SaveScopedConfig.For(this, SaveScopedDisplayMode.PlainCategories);
```

## Unload Cleanup

If you keep a `SaveScopedConfigFile` wrapper, dispose it when your plugin unloads:

```csharp
private void OnDestroy()
{
    saveConfig.Dispose();
}
```

This unregisters the entries owned by that plugin instance so stale entries are not left in the helper's registry. The helper also replaces duplicate bindings with the same config file, section, key, and type as a fallback.

## Config Layout

Entries are stored under save-prefixed sections:

```toml
[Save 1: General]
Enabled = true

[Save 2: General]
Enabled = false
```

Save type is tracked separately from the storage key. Use these values when you need save-mode-specific behavior:

- `Normal`
- `Steel Soul`
- `Steel Soul Dead`

This means changing a save slot from Normal to Steel Soul does not create a separate config bucket. The slot keeps its settings and `ActiveSaveMode` updates to the current mode.

## Runtime Behavior

- Loading an existing save shows that save's settings.
- Starting a new save shows that save's settings.
- Returning to the title menu hides save-specific settings.
- `Reset Saved Config On Delete Save` is disabled by default.
- If enabled, erasing a save resets scoped settings for that slot to their default values while keeping those entries in the config file.
- If disabled, deleting a save preserves that slot's scoped settings so a later save in the same slot can use them, regardless of save type.
- If Configuration Manager is open, the helper rebuilds and reopens the window while preserving position, scroll, and visual state.

## API Notes

- `SaveScopedConfig.For(plugin)` returns a disposable wrapper for bindings owned by that plugin.
- `SaveScopedConfig.For(plugin, SaveScopedDisplayMode.PlainCategories)` keeps Configuration Manager category names plain instead of prefixing them with `Save N`.
- `SaveScopedConfigEntry<T>.Value` reads and writes the active save's value. If no save is active, it reads the default and ignores writes.
- `SaveScopedConfig.ActiveSaveChanged` fires after entries have been moved to the new active save.
- `SaveScopedConfig.SaveSlotResetToDefaults` fires after a save slot's scoped values have been reset because a save was deleted.
- `SaveScopedConfig.ConfigManagerRefreshed` fires after the helper asks Configuration Manager to rebuild its setting list.
- `SaveScopedConfig.RefreshConfigManager(true)` can be used if a mod changes display metadata outside an active-save transition.

## Active Save Event

Mods can react to save changes:

```csharp
private void Awake()
{
    SaveScopedConfig.ActiveSaveChanged += OnActiveSaveChanged;
}

private void OnDestroy()
{
    SaveScopedConfig.ActiveSaveChanged -= OnActiveSaveChanged;
}

private void OnActiveSaveChanged()
{
    Logger.LogInfo("Active save: " + SaveScopedConfig.ActiveSaveKey);
}
```

`SaveScopedConfig.HasActiveSave` is `false` on the title menu.

Structured active-save values are also available:

```csharp
SaveScopedConfig.ActiveSaveSlot   // 1, 2, 3, 4, or 0 when inactive
SaveScopedConfig.ActiveSaveMode   // PermadeathModes.Off/On/Dead
SaveScopedConfig.ActiveSaveKey    // Save 1, Save 2, Save 3, Save 4, or null when inactive
SaveScopedConfig.ActiveSaveLabel  // Normal, Steel Soul, Steel Soul Dead, or null when inactive
SaveScopedConfig.ActiveSaveStatusLabel  // Normal, Steel Soul, Steel Soul (Dead), or null when inactive
```

Other runtime events:

```csharp
SaveScopedConfig.SaveSlotResetToDefaults += saveSlot =>
{
    Logger.LogInfo("Save " + saveSlot + " scoped settings were reset.");
};

SaveScopedConfig.ConfigManagerRefreshed += () =>
{
    Logger.LogInfo("Configuration Manager rebuilt its setting list.");
};
```

Remember to unsubscribe from events in `OnDestroy` if your plugin can unload or reload.

## Shared UI and compatibility helpers

`ModSettingsUi.BindPanel` adds a wrapped custom Configuration Manager panel. `ExplainEnablement` describes normal acquisition, global overrides, and save overrides without changing their values. A global or save enable remains additive; a false override never revokes normal acquisition.

`PresetLibrary` reads, saves, and deletes bounded case-insensitive named values in one config string. Names and values are URI escaped; malformed rows are ignored. Callers must validate their own preset payload before applying it.

`ModCompatibility.TryPatch` validates required reflected members before Harmony patching. If patching throws, it removes that Harmony owner's patches and returns an unavailable status. A different game version emits a warning rather than disabling otherwise valid APIs. The reference version is 1.0.30000; checks do not prove compatibility with future versions.

Public active-save, reset, and Configuration Manager notification subscribers are invoked independently. A throwing subscriber is logged and does not prevent later subscribers from receiving the notification. This isolation does not suppress failures inside an individual mod's config entry callbacks.

## Tests

```sh
dotnet run --project Tests/CoreTests.csproj -c Release
```

These core tests need no game assemblies and cover subscriber isolation and named-preset serialization, validation, capacity, and deletion. The sibling CrestLoadouts `Tests/run.py` provides disposable in-game integration tests for all five mods.

## Build and package

Requires the .NET 8 SDK and a local Silksong installation with BepInEx 5. Run from this repository:

```sh
python3 build.py --game-dir "/path/to/Hollow Knight Silksong"
python3 build.py --game-dir "/path/to/Hollow Knight Silksong" --package
```

`SILKSONG_GAME_DIR` can supply the game directory. Use `--dotnet /path/to/dotnet` for a local SDK.
Builds go to `bin/Release/netstandard2.1`. Packaging writes `dist/SaveScopedConfig-<version>.zip` with the plugin, README, and SHA-256 checksum; it includes no game assemblies. `--check` validates version metadata without building. Identical inputs produce identical package contents and timestamps. Release metadata excludes Git revision suffixes so checked-in binaries do not identify an earlier source commit.

Builds do not deploy by default. Install the DLL under `BepInEx/plugins/SaveScopedConfig/` after closing the game. On Windows, `build.ps1 -GameDir "C:\path\to\game"` also builds locally; explicit `-Target Plugin` deploys a plugin and `-Target Dev` deploys for ScriptEngine. Do not install both copies.
