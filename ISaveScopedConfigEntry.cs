namespace WheresWolfgang.SaveScopedConfig
{
    internal interface ISaveScopedConfigEntry
    {
        string OwnerId { get; }
        string Category { get; }
        string Key { get; }
        System.Type SettingType { get; }

        void ApplyActiveSave();
        void Detach();
        void ResetForSaveSlot(int saveSlot);
    }
}
