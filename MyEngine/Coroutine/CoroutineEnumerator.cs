using System.Collections.Generic;

namespace MyEngine.Coroutine
{
    public class CoroutineEnumerator : IYieldInstruction
    {
        private readonly IEnumerator<IYieldInstruction> _enumerator;
        private IYieldInstruction? _current;

        public CoroutineEnumerator(IEnumerator<IYieldInstruction> enumerator)
        {
            _enumerator = enumerator;
            if (_enumerator.MoveNext())
            {
                _current = _enumerator.Current;
            }
        }

        public bool Update(float deltaTime)
        {
            if (_current == null)
            {
                return true;
            }

            if (_current.Update(deltaTime))
            {
                if (_enumerator.MoveNext())
                {
                    _current = _enumerator.Current;
                    return false;
                }
                return true;
            }

            return false;
        }
    }
} 