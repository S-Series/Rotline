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

        // Gameplay currently nests this object under a scene container.
        // Persistence requires a root object; leave other scene objects in their scene.
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
    }
}
