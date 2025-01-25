using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MyEngine.Coroutine
{
    public class CoroutineInfo
    {
        private readonly ILogger _logger;
        private readonly List<CoroutineInfo> _children = new();
        private CoroutineState _state = CoroutineState.Running;
        private IYieldInstruction? _currentYieldInstruction;
        private bool _isFirstUpdate = true;

        public event EventHandler<CoroutineState>? StateChanged;

        public IEnumerator<IYieldInstruction> Routine { get; }
        public IReadOnlyList<CoroutineInfo> Children => _children;
        public CoroutineState State => _state;
        public IYieldInstruction? CurrentYieldInstruction => _currentYieldInstruction;
        public bool IsFirstUpdate => _isFirstUpdate;

        public CoroutineInfo(IEnumerator<IYieldInstruction> routine, ILogger logger)
        {
            Routine = routine ?? throw new ArgumentNullException(nameof(routine));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void AddChild(CoroutineInfo child)
        {
            _children.Add(child);
            if (_state == CoroutineState.Paused)
            {
                child.SetState(CoroutineState.Paused);
            }
        }

        public void RemoveChild(CoroutineInfo child)
        {
            _children.Remove(child);
        }

        public void SetYieldInstruction(IYieldInstruction? instruction)
        {
            if (_currentYieldInstruction is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // ignore
                }
            }

            _currentYieldInstruction = instruction;
        }

        public void SetState(CoroutineState newState)
        {
            if (_state == newState)
            {
                return;
            }
            var oldState = _state;
            _state = newState;

            if (newState == CoroutineState.Paused)
            {
                // 子も一緒にPause
                foreach (var c in _children)
                {
                    c.SetState(CoroutineState.Paused);
                }
            }
            else if (newState == CoroutineState.Completed)
            {
                // 子も完了
                foreach (var c in _children.ToList())
                {
                    c.SetState(CoroutineState.Completed);
                    RemoveChild(c);
                }
                // Dispose current yield
                if (_currentYieldInstruction is IDisposable d2)
                {
                    d2.Dispose();
                }
                _currentYieldInstruction = null;
            }

            // StateChangedイベント呼び出し
            StateChanged?.Invoke(this, _state);
        }

        public void MarkFirstUpdateComplete()
        {
            _isFirstUpdate = false;
        }

        public void ResetFirstUpdate()
        {
            _isFirstUpdate = true;
        }
    }
}
