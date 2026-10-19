namespace WheresWolfgang.SaveScopedConfig
{
    internal sealed class ConfigurationManagerAttributes
    {
        public System.Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer;
        public bool? Browsable;
        public string Category;
        public bool? ReadOnly;
        public bool? HideDefaultButton;
        public bool? HideSettingName;
        public string DispName;
        public int? Order;
    }
}
