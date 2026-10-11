using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class AlphaProfile
{
    public int version = 1;
    public int shards;
    public int vitality;
    public int power;
    public int magnet;
    public int runs;
    public int wins;
    public int bestKills;
    public float bestSeconds;
    public float volume = 0.6f;
    public bool fullscreen;
    public List<string> discoveries = new();
    public List<string> settledRuns = new();
}

// A caller supplies the path, allowing automated checks to use an isolated profile.
public sealed class AlphaProfileStore
{
    private readonly string path;
    private readonly HashSet<string> pendingDiscoveries = new();
    private readonly Dictionary<string, Settlement> pendingSettlements = new();
    private bool preserveBackupOnNextSave;
    private readonly struct Settlement
    {
        public readonly bool Won;
        public readonly int Kills;
        public readonly float Seconds;
        public int Reward => (Kills > 0 ? Mathf.Max(1, Kills / 5) : 0) + (Won ? 25 : 0);
        public Settlement(bool won, int kills, float seconds) { Won = won; Kills = kills; Seconds = seconds; }
    }
    public AlphaProfile Data { get; private set; }
    public string Error { get; private set; }
    public int PendingSettlementCount => pendingSettlements.Count;
    public bool IsSettlementPending(string runId) => runId != null && pendingSettlements.ContainsKey(runId);
    public AlphaProfileStore(string path)
    {
        this.path = path;
        Data = Read(path);
        if (Data == null)
        {
            Data = Read(path + ".bak");
            preserveBackupOnNextSave = Data != null;
        }
        Data ??= new AlphaProfile();
        Data.discoveries ??= new List<string>(); Data.settledRuns ??= new List<string>();
        Data.shards = Mathf.Max(0, Data.shards);
        Data.vitality = Mathf.Clamp(Data.vitality, 0, 5); Data.power = Mathf.Clamp(Data.power, 0, 5); Data.magnet = Mathf.Clamp(Data.magnet, 0, 5);
        Data.volume = Mathf.Clamp01(Data.volume);
    }
    private AlphaProfile Read(string filename)
    {
        if (!File.Exists(filename)) return null;
        try
        {
            string json = File.ReadAllText(filename);
            if (!json.TrimStart().StartsWith("{") || !json.Contains("\"version\"")) throw new IOException("Invalid profile.");
            var value = JsonUtility.FromJson<AlphaProfile>(json);
            if (value == null || value.version != 1) throw new IOException("Unsupported profile version.");
            return value;
        }
        catch (Exception ex) { Error = "저장 데이터를 읽지 못했습니다. 백업 또는 새 프로필을 사용합니다. " + ex.Message; return null; }
    }
    public bool Save()
    {
        // Pending results belong to this store, not the scene that earned them.
        // Apply them only inside the save transaction and retain them on failure.
        string before = JsonUtility.ToJson(Data);
        foreach (var entry in pendingSettlements)
        {
            if (Data.settledRuns.Contains(entry.Key)) continue;
            var result = entry.Value;
            Data.shards += result.Reward; Data.runs++; if (result.Won) Data.wins++;
            Data.bestKills = Mathf.Max(Data.bestKills, result.Kills);
            Data.bestSeconds = Mathf.Max(Data.bestSeconds, result.Seconds);
            Data.settledRuns.Add(entry.Key);
        }
        foreach (string id in pendingDiscoveries)
            if (!Data.discoveries.Contains(id)) Data.discoveries.Add(id);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(Data, true));
            // A recovered profile must not rotate its corrupt primary into the good backup.
            if (File.Exists(path)) File.Replace(temp, path, preserveBackupOnNextSave ? null : path + ".bak");
            else File.Move(temp, path);
            preserveBackupOnNextSave = false;
            pendingSettlements.Clear();
            pendingDiscoveries.Clear();
            Error = null; return true;
        }
        catch (Exception ex)
        {
            Data = JsonUtility.FromJson<AlphaProfile>(before);
            Error = "저장 실패: " + ex.Message;
            return false;
        }
    }
    public int UpgradeLevel(int kind) => kind == 0 ? Data.vitality : kind == 1 ? Data.power : Data.magnet;
    public int UpgradeCost(int kind) => 15 + UpgradeLevel(kind) * 15;
    public bool BuyUpgrade(int kind)
    {
        if (kind < 0 || kind > 2 || UpgradeLevel(kind) >= 5 || Data.shards < UpgradeCost(kind)) return false;
        string before = JsonUtility.ToJson(Data);
        Data.shards -= UpgradeCost(kind);
        if (kind == 0) Data.vitality++; else if (kind == 1) Data.power++; else Data.magnet++;
        if (Save()) return true;
        Data = JsonUtility.FromJson<AlphaProfile>(before); return false;
    }
    public int Settle(string runId, bool won, int kills, float seconds)
    {
        if (string.IsNullOrEmpty(runId) || Data.settledRuns.Contains(runId)) return 0;
        // First terminal result wins even if a caller retries with different arguments.
        if (!pendingSettlements.ContainsKey(runId)) pendingSettlements.Add(runId, new Settlement(won, kills, seconds));
        int reward = pendingSettlements[runId].Reward;
        return Save() ? reward : -1;
    }
    public bool RetryPendingSettlements() => pendingSettlements.Count == 0 || Save();
    public bool Discover(string id)
    {
        if (string.IsNullOrEmpty(id) || Data.discoveries.Contains(id)) return false;
        pendingDiscoveries.Add(id);
        return Save();
    }
}
