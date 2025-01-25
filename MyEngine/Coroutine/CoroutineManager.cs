using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MyEngine.Coroutine;

public class CoroutineManager
{
    private readonly ILogger _logger;
    private readonly List<CoroutineInfo> _coroutines = new();
    private readonly List<CoroutineInfo> _coroutinesToAdd = new();
    private readonly List<CoroutineInfo> _coroutinesToRemove = new();
    private bool _isProcessing;

    public int ActiveCoroutineCount => _coroutines.Count;

    public CoroutineManager(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public CoroutineInfo Start(IEnumerator<IYieldInstruction> routine)
    {
        if (routine == null)
        {
            throw new ArgumentNullException(nameof(routine));
        }

        var coroutine = new CoroutineInfo(routine, _logger);
        
        // 初回のMoveNextを実行
        if (!routine.MoveNext())
        {
            coroutine.SetState(CoroutineState.Completed);
        }
        else
        {
            var current = routine.Current;
            if (current is IYieldInstruction yieldInstruction)
            {
                coroutine.SetYieldInstruction(yieldInstruction);
            }
            else if (current is IEnumerator<IYieldInstruction> childRoutine)
            {
                var child = new CoroutineInfo(childRoutine, _logger);
                child.SetState(CoroutineState.Running);
                coroutine.AddChild(child);
                _coroutinesToAdd.Add(child);
                coroutine.SetState(CoroutineState.Waiting);
            }
        }

        _coroutinesToAdd.Add(coroutine);
        return coroutine;
    }

    public void Stop(IEnumerator<IYieldInstruction> routine)
    {
        if (routine == null) throw new ArgumentNullException(nameof(routine));
        var coroutine = _coroutines.FirstOrDefault(c => c.Routine == routine);
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
        }
    }

    public void StopAll()
    {
        foreach (var coroutine in _coroutines.ToList())
        {
            StopCoroutine(coroutine);
        }
    }

    public void StopCoroutine(CoroutineInfo coroutine)
    {
        if (coroutine == null) throw new ArgumentNullException(nameof(coroutine));
        if (!_coroutines.Contains(coroutine)) return;

        coroutine.SetState(CoroutineState.Completed);
        _coroutinesToRemove.Add(coroutine);
    }

    public void Update(float deltaTime)
    {
        // 新しいコルーチンを追加
        foreach (var coroutine in _coroutinesToAdd)
        {
            _coroutines.Add(coroutine);
        }
        _coroutinesToAdd.Clear();

        // コルーチンを更新
        var coroutinesToProcess = _coroutines.ToList();
        foreach (var coroutine in coroutinesToProcess)
        {
            if (coroutine.State == CoroutineState.Running || coroutine.State == CoroutineState.Waiting)
            {
                ProcessCoroutine(coroutine, deltaTime);
            }
        }

        // 完了したコルーチンを削除
        foreach (var coroutine in _coroutinesToRemove)
        {
            _coroutines.Remove(coroutine);
        }
        _coroutinesToRemove.Clear();
    }

    private bool ProcessCoroutine(CoroutineInfo coroutine, float deltaTime)
    {
        if (coroutine.State == CoroutineState.Completed || coroutine.State == CoroutineState.Paused)
        {
            return false;
        }

        // 現在のyield instructionの処理
        if (coroutine.CurrentYieldInstruction != null)
        {
            if (coroutine.CurrentYieldInstruction.Update(deltaTime))
            {
                coroutine.SetYieldInstruction(null);
            }
            else
            {
                return true;
            }
        }

        // 子コルーチンの処理
        if (coroutine.Children.Any())
        {
            var anyChildUpdated = false;
            var allChildrenCompleted = true;

            foreach (var child in coroutine.Children.ToList())
            {
                if (child.State != CoroutineState.Completed)
                {
                    allChildrenCompleted = false;
                    if (ProcessCoroutine(child, deltaTime))
                    {
                        anyChildUpdated = true;
                    }
                }
                else
                {
                    coroutine.RemoveChild(child);
                    _coroutinesToRemove.Add(child);
                }
            }

            if (!allChildrenCompleted)
            {
                coroutine.SetState(CoroutineState.Waiting);
                return anyChildUpdated;
            }
        }

        // コルーチンの実行
        if (!coroutine.Routine.MoveNext())
        {
            coroutine.SetState(CoroutineState.Completed);
            return false;
        }

        var current = coroutine.Routine.Current;
        if (current is IYieldInstruction yieldInstruction)
        {
            coroutine.SetYieldInstruction(yieldInstruction);
            return true;
        }
        else if (current is IEnumerator<IYieldInstruction> childRoutine)
        {
            var childInfo = new CoroutineInfo(childRoutine, _logger);
            childInfo.SetState(CoroutineState.Running);
            coroutine.AddChild(childInfo);
            _coroutinesToAdd.Add(childInfo);
            coroutine.SetState(CoroutineState.Waiting);
            return true;
        }

        return true;
    }

    public void Pause(IEnumerator<IYieldInstruction> routine)
    {
        if (routine == null) throw new ArgumentNullException(nameof(routine));
        var coroutine = _coroutines.FirstOrDefault(c => c.Routine == routine);
        if (coroutine != null)
        {
            PauseCoroutine(coroutine);
        }
    }

    public void Resume(IEnumerator<IYieldInstruction> routine)
    {
        if (routine == null) throw new ArgumentNullException(nameof(routine));
        var coroutine = _coroutines.FirstOrDefault(c => c.Routine == routine);
        if (coroutine != null)
        {
            ResumeCoroutine(coroutine);
        }
    }

    public void PauseCoroutine(CoroutineInfo coroutine)
    {
        if (coroutine == null)
        {
            throw new ArgumentNullException(nameof(coroutine));
        }

        if (coroutine.State == CoroutineState.Running || coroutine.State == CoroutineState.Waiting)
        {
            coroutine.SetState(CoroutineState.Paused);
            foreach (var child in coroutine.Children.ToList())
            {
                PauseCoroutine(child);
            }
        }
    }

    public void ResumeCoroutine(CoroutineInfo coroutine)
    {
        if (coroutine == null)
        {
            throw new ArgumentNullException(nameof(coroutine));
        }

        if (coroutine.State == CoroutineState.Paused)
        {
            if (coroutine.Children.Any())
            {
                coroutine.SetState(CoroutineState.Waiting);
                foreach (var child in coroutine.Children.ToList())
                {
                    ResumeCoroutine(child);
                }
            }
            else
            {
                coroutine.SetState(CoroutineState.Running);
            }
        }
    }
} 