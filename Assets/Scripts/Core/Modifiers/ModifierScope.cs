using System;
using System.Collections.Generic;

public sealed class ModifierScope : IDisposable
{
    private readonly PlayerStats stats;

    private readonly List<(StatKey Stat, int Id)> handles = new();

    private bool disposed;
    private bool disposeStarted;
    private bool isDisposing;

    public bool IsDisposed => disposed;

    public int Count => handles.Count;


    public ModifierScope(PlayerStats stats)
    {
        if (stats == null)
            throw new ArgumentNullException(nameof(stats));

        this.stats = stats;
    }


    public int AddModifier(
        StatKey stat,
        ModifierType type,
        float amount)
    {
        ThrowIfDisposed();

        // 등록에 성공한 Modifier만 관리한다.
        int id = stats.AddModifier(stat, type, amount);

        handles.Add((stat, id));

        return id;
    }

    public void Dispose()
    {
        // 정리 완료 또는 정리 진행 중에는 중복 실행하지 않는다.
        if (disposed || isDisposing)
            return;

        // 실패하더라도 새 Modifier 등록은 영구적으로 금지한다.
        disposeStarted = true;
        isDisposing = true;

        List<Exception> failures = null;

        try
        {
            // 마지막으로 등록한 효과부터 제거한다.
            for (int i = handles.Count - 1; i >= 0; i--)
            {
                var handle = handles[i];

                try
                {
                    // false라면 이미 제거됐거나 무효화된 ID.
                    // 이 경우에도 Scope에서는 관리 대상에서 제외한다.
                    stats.RemoveModifier(
                        handle.Stat,
                        handle.Id
                    );

                    handles.RemoveAt(i);
                }
                catch (Exception ex)
                {
                    // 실패한 ID는 남겨 두고 다음 효과 정리를 계속한다.
                    failures ??= new List<Exception>();
                    failures.Add(ex);
                }
            }

            // 모든 ID를 정리했을 때만 완료 상태로 변경.
            disposed = handles.Count == 0;
        }
        finally
        {
            // 예외가 발생해도 재시도할 수 있도록 실행 잠금을 해제.
            isDisposing = false;
        }

        if (failures != null)
        {
            throw new AggregateException(
                "One or more modifiers could not be removed.",
                failures
            );
        }
    }
        
    private void ThrowIfDisposed()
    {
        if (disposeStarted)
        {
            throw new ObjectDisposedException(
                nameof(ModifierScope),
                "Cannot add modifiers after disposal has started."
            );
        }
    }

}