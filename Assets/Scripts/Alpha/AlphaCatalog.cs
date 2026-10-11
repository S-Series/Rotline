using UnityEngine;

[CreateAssetMenu(menuName = "Game/Alpha/Catalog")]
public sealed class AlphaCatalog : ScriptableObject
{
    public CharacterDefinition[] characters;
    public WeaponAttackDefinition[] weapons;
    public EnemyDefinition[] enemies;
    public EnemyDefinition boss;
    public SurvivalRunDefinition run;
}

public static class AlphaSession
{
    private static AlphaProfileStore store;
    private static string profilePathOverride;
    public static AlphaProfileStore Profile => store ??= new AlphaProfileStore(profilePathOverride ?? System.IO.Path.Combine(Application.persistentDataPath, "alpha-profile.json"));
    public static AlphaCatalog Catalog => Resources.Load<AlphaCatalog>("AlphaCatalog");
    public static bool Active { get; private set; }
    public static bool Expedition { get; private set; }
    public static int CharacterIndex { get; private set; }
    public static int Seed { get; private set; }
    public static string RunId { get; private set; }
    public static CharacterDefinition Character => Active ? Catalog.characters[Mathf.Clamp(CharacterIndex, 0, Catalog.characters.Length - 1)] : null;
    public static void Begin(int character, bool expedition, int seed = 0)
    {
        Profile.RetryPendingSettlements();
        Active = true; CharacterIndex = character; Expedition = expedition;
        Seed = seed == 0 ? System.Environment.TickCount : seed;
        RunId = System.Guid.NewGuid().ToString("N");
        Time.timeScale = 1f; ApplySettings();
    }
    public static void End() { Active = false; Time.timeScale = 1f; }
    public static void ApplySettings()
    {
        AudioListener.volume = Profile.Data.volume;
        if (!Application.isEditor) Screen.fullScreen = Profile.Data.fullscreen;
    }
    public static void UseProfileForChecks(string path) { profilePathOverride = path; store = new AlphaProfileStore(path); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { store = null; Active = false; RunId = null; }
}
