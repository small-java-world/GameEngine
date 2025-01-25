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

        // 子コルーチンの状態を更新
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
                    else if (child.State == CoroutineState.Waiting)
                    {
                        // 親が再開したとき、待機中の子も再開
                        child.SetState(CoroutineState.Running);
                    }
                    break;
                case CoroutineState.Waiting:
                    // 子コルーチンの状態は変更しない
                    break;
                case CoroutineState.Completed:
                    child.SetState(CoroutineState.Completed);
                    RemoveChild(child);
                    break;
            }
        }

        // 状態変更時の処理
        switch (newState)
        {
            case CoroutineState.Running:
                if (oldState == CoroutineState.Waiting && _currentYieldInstruction == null)
                {
                    // Waiting状態から復帰時、yield instructionがなければ次のステップへ
                    _state = CoroutineState.Running;
                }
                break;
            case CoroutineState.Waiting:
                // 子コルーチンが存在する場合は、子コルーチンの状態を維持
                if (_children.Count > 0)
                {
                    foreach (var child in _children)
                    {
                        if (child.State == CoroutineState.Paused)
                        {
                            child.SetState(CoroutineState.Running);
                        }
                    }
                }
                break;
            case CoroutineState.Completed:
                // 完了時は子コルーチンも完了
                foreach (var child in _children.ToList())
                {
                    child.SetState(CoroutineState.Completed);
                    RemoveChild(child);
                }

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
                break;
            case CoroutineState.Paused:
                // 一時停止時は子コルーチンも一時停止
                foreach (var child in _children)
                {
                    child.SetState(CoroutineState.Paused);
                }
                break;
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