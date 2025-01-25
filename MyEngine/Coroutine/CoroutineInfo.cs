using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MyEngine.Coroutine;

public class CoroutineInfo
{
    private readonly ILogger _logger;
    private readonly List<CoroutineInfo> _children = new();
    private CoroutineState _state;
    private IYieldInstruction? _currentYieldInstruction;

    public IEnumerator Routine { get; }
    public CoroutineState State => _state;
    public CoroutineInfo? Parent { get; private set; }
    public IReadOnlyList<CoroutineInfo> Children => _children;
    public IYieldInstruction? CurrentYieldInstruction => _currentYieldInstruction;

    public event Action<CoroutineInfo>? OnStateChanged;

    public CoroutineInfo(IEnumerator routine, ILogger logger)
    {
        Routine = routine;
        _logger = logger;
        _state = CoroutineState.Running;
    }

    internal void AddChild(CoroutineInfo child)
    {
        if (!_children.Contains(child))
        {
            child.Parent?.RemoveChild(child);
            child.Parent = this;
            _children.Add(child);
        }
    }

    internal void RemoveChild(CoroutineInfo child)
    {
        if (_children.Contains(child))
        {
            child.Parent = null;
            _children.Remove(child);
        }
    }

    internal void SetState(CoroutineState newState)
    {
        if (_state != newState)
        {
            var oldState = _state;
            _state = newState;

            if (newState == CoroutineState.Paused)
            {
                foreach (var child in _children.ToList())
                {
                    child.SetState(CoroutineState.Paused);
                }
            }
            else if (oldState == CoroutineState.Paused && newState == CoroutineState.Running)
            {
                foreach (var child in _children.ToList())
                {
                    child.SetState(CoroutineState.Running);
                }
            }

            OnStateChanged?.Invoke(this);
        }
    }

    internal void SetYieldInstruction(IYieldInstruction? instruction)
    {
        if (_currentYieldInstruction != instruction)
        {
            _currentYieldInstruction = instruction;
            if (instruction != null)
            {
                SetState(CoroutineState.Waiting);
            }
        }
    }
} 