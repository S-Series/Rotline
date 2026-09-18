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

        Stats = stats
            ? stats
            : throw new ArgumentNullException(nameof(stats));
    }


    // 플레이어가 능력 버튼을 눌렀을 때 호출하는 공용 진입점.
    // 능력마다 사용 조건과 동작이 다르므로 하위 클래스에서 재정의한다.
    public virtual bool RequestAbility()
    {
        return false;
    }


    // 활성/비활성 여부와 관계없이 매 프레임 호출하는 공용 진입점.
    // 일반 능력은 기존 Tick()을 사용한다.
    // 버서커처럼 비활성 상태에도 회복이 필요한 능력은 재정의한다.
    public virtual void UpdateAbility(float deltaTime)
    {
        Tick(deltaTime);
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


    protected abstract void OnActivate();

    protected abstract void OnDeactivate();

    protected virtual void OnTick(float deltaTime)
    {
    }

    protected virtual void OnDispose()
    {
    }
}