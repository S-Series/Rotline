using UnityEngine;

[RequireComponent(typeof(InputReader))]
public sealed class AppRoot : MonoSingleton<AppRoot>
{
    public InputReader Input { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
            return;

        Input = GetComponent<InputReader>();

        DontDestroyOnLoad(gameObject);
    }
}