using WheresWolfgang.SaveScopedConfig;

int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
int calls = 0, errors = 0;
Action listeners = () => throw new InvalidOperationException("broken consumer");
listeners += () => calls++;
EventDispatch.Invoke(listeners, error => errors++);
Check(calls == 1 && errors == 1, "A throwing consumer must not block the next one");
Action<int> slots = value => throw new Exception("broken slot consumer");
slots += value => calls += value;
EventDispatch.Invoke(slots, 3, error => errors++);
Check(calls == 4 && errors == 2, "Typed subscribers must also be isolated");
EventDispatch.Invoke((Action)null, error => throw error);
Check(calls == 4, "No subscribers is valid");
Action snapshot = null;
snapshot += () => snapshot += () => calls += 100;
snapshot += () => calls++;
EventDispatch.Invoke(snapshot, error => throw error);
Check(calls == 5, "Subscriptions added during dispatch run next time");
string data, errorText;
Check(PresetLibrary.TrySave("", " My / crest; = 雪 ", "Hunter,Witch,Reaper", out data, out errorText), "Save punctuation and Unicode");
Check(PresetLibrary.Read(data)["My / crest; = 雪"] == "Hunter,Witch,Reaper", "Preset round-trip");
Check(PresetLibrary.TrySave(data, "my / CREST; = 雪", "Reaper", out data, out errorText), "Overwrite ignores case");
Check(PresetLibrary.Read(data).Count == 1, "Overwrite must not duplicate a preset");
string previous = data;
Check(!PresetLibrary.TrySave(data, " ", "x", out data, out errorText) && data == previous, "Invalid names leave existing data unchanged");
Check(!PresetLibrary.TrySave(data, "name", new string('x', 1025), out data, out errorText), "Reject oversized values");
Check(PresetLibrary.Read("broken;=bad;Good=Hunter").Count == 1, "Malformed rows do not hide valid rows");
Check(PresetLibrary.Read(PresetLibrary.Delete(previous, "MY / CREST; = 雪")).Count == 0, "Delete ignores case");
data = "";
for (int i = 0; i < 32; i++) Check(PresetLibrary.TrySave(data, "P" + i, "Hunter", out data, out errorText), "Save within limit");
Check(!PresetLibrary.TrySave(data, "Overflow", "Hunter", out data, out errorText), "Bound the preset count");
Check(PresetLibrary.TrySave(data, "P0", "Witch", out data, out errorText), "Overwrite still works at capacity");
Check(PresetLibrary.Read(data + ";p0=Reaper")["P0"] == "Reaper", "Duplicate names overwrite at capacity");
string large = "";
for (int i = 0; i < 7; i++)
    Check(PresetLibrary.TrySave(large, "U" + i, new string('雪', 1024), out large, out errorText), "Unicode payload within serialized capacity");
previous = large;
Check(!PresetLibrary.TrySave(large, "Overflow", new string('雪', 1024), out large, out errorText) && large == previous,
    "Encoded size cannot exceed readable storage or destroy existing presets");
Check(PresetLibrary.Read(large).Count == 7, "All successfully saved payloads remain readable");
Console.WriteLine($"PASS {checks} core checks");
