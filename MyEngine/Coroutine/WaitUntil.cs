namespace MyEngine.Coroutine;

public class WaitUntil : IYieldInstruction
{
    private readonly Func<bool> _predicate;
    private bool _isCompleted;

    public WaitUntil(Func<bool> predicate)
    {
        _predicate = predicate;
        _isCompleted = false;
    }

    public bool Update(float deltaTime)
    {
        if (_isCompleted)
        {
            return true;
        }

        if (_predicate())
        {
            _isCompleted = true;
            return true;
        }
        return false;
    }
} 