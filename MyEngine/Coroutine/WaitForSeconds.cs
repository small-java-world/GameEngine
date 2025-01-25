using System;

namespace MyEngine.Coroutine;

public class WaitForSeconds : IYieldInstruction, IDisposable
{
    private readonly float _duration;
    private float _elapsedTime;
    private bool _isDisposed;

    public WaitForSeconds(float duration)
    {
        if (duration < 0)
        {
            throw new ArgumentException("Duration cannot be negative.", nameof(duration));
        }
        _duration = duration;
        _elapsedTime = 0f;
        _isDisposed = false;
    }

    public bool Update(float deltaTime)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(WaitForSeconds));
        }

        _elapsedTime += deltaTime;
        return _elapsedTime >= _duration;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
        }
    }
} 