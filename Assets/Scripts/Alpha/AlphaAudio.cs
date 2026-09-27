using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class AlphaAudio : MonoBehaviour
{
    private static AlphaAudio instance;
    private AudioSource source;
    private AudioClip click;
    private void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this; DontDestroyOnLoad(gameObject);
        source = GetComponent<AudioSource>(); source.playOnAwake = false;
        int samples = 2205;
        var data = new float[samples];
        for (int i = 0; i < samples; i++) data[i] = Mathf.Sin(2f * Mathf.PI * 440f * i / 22050f) * 0.12f * (1f - (float)i / samples);
        click = AudioClip.Create("Alpha feedback", samples, 1, 22050, false); click.SetData(data, 0);
    }
    public static void Play(float frequency = 440f)
    {
        if (!AlphaSession.Active && instance == null) return;
        if (instance == null) new GameObject("Alpha Audio").AddComponent<AlphaAudio>();
        instance.source.pitch = frequency / 440f; instance.source.PlayOneShot(instance.click);
    }
    private void OnDestroy() { if (instance == this) instance = null; if (click != null) Destroy(click); }
}
