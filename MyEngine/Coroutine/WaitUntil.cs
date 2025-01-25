namespace MyEngine.Coroutine;

public class WaitUntil : IYieldInstruction
{
    private readonly Func<bool> _predicate;

    public WaitUntil(Func<bool> predicate)
    {
        _predicate = predicate;
    }

    public bool Update(float deltaTime)
    {
        return _predicate();
    }
} 