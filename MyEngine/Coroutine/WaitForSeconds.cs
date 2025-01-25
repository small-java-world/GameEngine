namespace MyEngine.Coroutine;

public class WaitForSeconds : IYieldInstruction
{
    private float _remainingTime;

    public WaitForSeconds(float seconds)
    {
        _remainingTime = seconds;
    }

    public bool Update(float deltaTime)
    {
        _remainingTime -= deltaTime;
        return _remainingTime <= 0;
    }
} 