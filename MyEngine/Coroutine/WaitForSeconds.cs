namespace MyEngine.Coroutine;

public class WaitForSeconds : IYieldInstruction
{
    private float _remainingTime;
    private bool _isCompleted;

    public WaitForSeconds(float seconds)
    {
        _remainingTime = seconds;
        _isCompleted = false;
    }

    public bool Update(float deltaTime)
    {
        if (_isCompleted)
        {
            return true;
        }

        _remainingTime -= deltaTime;
        if (_remainingTime <= 0)
        {
            _isCompleted = true;
            return true;
        }
        return false;
    }
} 