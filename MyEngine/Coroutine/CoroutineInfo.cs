using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MyEngine.Coroutine;

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
        if (child == null) throw new ArgumentNullException(nameof(child));
        _children.Add(child);

        if (_state == CoroutineState.Paused)
        {
            child.SetState(CoroutineState.Paused);
        }
    }

    public void RemoveChild(CoroutineInfo child)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));
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
                // 既に破棄されている場合は無視
            }
        }

        _currentYieldInstruction = instruction;
        if (instruction == null && _state == CoroutineState.Waiting)
        {
            SetState(CoroutineState.Running);
        }
        else if (instruction != null && _state == CoroutineState.Running)
        {
            SetState(CoroutineState.Waiting);
        }
    }

    public void SetState(CoroutineState newState)
    {
        if (_state == newState) return;

        var oldState = _state;
        _state = newState;
        StateChanged?.Invoke(this, newState);

        foreach (var child in _children.ToList())
        {
            switch (newState)
            {
                case CoroutineState.Paused:
                    child.SetState(CoroutineState.Paused);
                    break;
                case CoroutineState.Running:
                    if (child.State == CoroutineState.Paused)
                    {
                        child.SetState(CoroutineState.Running);
                    }
                    break;
                case CoroutineState.Completed:
                    child.SetState(CoroutineState.Completed);
                    RemoveChild(child);
                    break;
            }
        }

        if (newState == CoroutineState.Completed)
        {
            if (_currentYieldInstruction is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // 既に破棄されている場合は無視
                }
            }
            _currentYieldInstruction = null;
        }
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