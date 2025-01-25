using System;

namespace MyEngine.Coroutine;

public class WaitForSeconds : IYieldInstruction, IDisposable
{
    private readonly float _duration;
    private float _elapsed;
    private bool _isCompleted;
    private bool _isDisposed;

    public WaitForSeconds(float duration)
    {
        if (duration < 0)
            throw new ArgumentException("Duration cannot be negative", nameof(duration));
        
        _duration = duration;
        _elapsed = 0;
        _isCompleted = false;
        _isDisposed = false;
    }

    public bool Update(float deltaTime)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(WaitForSeconds));

        if (_isCompleted)
            return true;

        _elapsed += deltaTime;
        if (_elapsed >= _duration)
        {
            _isCompleted = true;
            return true;
        }
        return false;
    }

    public void Reset()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(WaitForSeconds));

        _elapsed = 0;
        _isCompleted = false;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _isCompleted = true;
        }
    }
} 