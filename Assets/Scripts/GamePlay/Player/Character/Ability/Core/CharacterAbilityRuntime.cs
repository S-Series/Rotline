using System;

public abstract class CharacterAbilityRuntime : IDisposable
{
    public CharacterAbilityDefinition Definition { get; }

    protected PlayerStats Stats { get; }

    public bool IsActive { get; private set; }

    public bool IsDisposed { get; private set; }


    protected CharacterAbilityRuntime(
        CharacterAbilityDefinition definition,
        PlayerStats stats)
    {
        Definition = definition
            ?? throw new ArgumentNullException(nameof(definition));

        Stats = stats != null
            ? stats
            : throw new ArgumentNullException(nameof(stats));
    }


    public void Activate()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(GetType().Name);

        if (IsActive)
            return;

        OnActivate();

        IsActive = true;
    }


    public void Tick(float deltaTime)
    {
        if (!IsActive || IsDisposed)
            return;

        OnTick(deltaTime);
    }


    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;

        OnDeactivate();
    }


    public void Dispose()
    {
        if (IsDisposed)
            return;

        Deactivate();

        OnDispose();

        IsDisposed = true;
    }


    // 각 능력에서 반드시 구현할 함수

    protected abstract void OnActivate();

    protected abstract void OnDeactivate();


    // 필요한 능력만 Override

    protected virtual void OnTick(float deltaTime)
    {
    }


    protected virtual void OnDispose()
    {
    }
}