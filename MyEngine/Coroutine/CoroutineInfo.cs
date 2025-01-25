using System;
using System.Collections;
using System.Collections.Generic;

namespace MyEngine.Coroutine;

public class CoroutineInfo
{
    public event Action<CoroutineInfo>? OnStateChanged;

    public IEnumerator Routine { get; }
    public CoroutineState State { get; private set; }
    public CoroutineInfo? Parent { get; }
    public IReadOnlyList<CoroutineInfo> Children => _children;
    public IYieldInstruction? CurrentYieldInstruction { get; private set; }

    private readonly List<CoroutineInfo> _children = new();

    public CoroutineInfo(IEnumerator routine, CoroutineInfo? parent = null)
    {
        Routine = routine;
        Parent = parent;
        State = CoroutineState.Running;
    }

    public void AddChild(CoroutineInfo child)
    {
        _children.Add(child);
    }

    public void RemoveChild(CoroutineInfo child)
    {
        _children.Remove(child);
    }

    public void SetState(CoroutineState state)
    {
        if (State != state)
        {
            State = state;
            OnStateChanged?.Invoke(this);
        }
    }

    public void SetYieldInstruction(IYieldInstruction? instruction)
    {
        CurrentYieldInstruction = instruction;
    }
} 