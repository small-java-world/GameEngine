using System;

namespace MyEngine.Coroutine;

public class WaitUntil : IYieldInstruction, IDisposable
{
    private readonly Func<bool> _predicate;
    private bool _isDisposed;

    public WaitUntil(Func<bool> predicate)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public bool Update(float deltaTime)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(WaitUntil));

        return _predicate();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
        }
    }
} 