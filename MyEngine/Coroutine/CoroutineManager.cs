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
        if (routine == null) throw new ArgumentNullException(nameof(routine));
        var coroutine = new CoroutineInfo(routine, _logger);
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
        _isProcessing = true;

        try
        {
            // 新しいコルーチンを追加して初期化
            if (_coroutinesToAdd.Count > 0)
            {
                foreach (var coroutine in _coroutinesToAdd.ToList())
                {
                    _coroutines.Add(coroutine);
                    
                    // コルーチンの初期実行
                    if (coroutine.Routine.MoveNext())
                    {
                        var current = coroutine.Routine.Current;
                        if (current is IEnumerator<IYieldInstruction> childRoutine)
                        {
                            // 子コルーチンは次のフレームで開始
                            var child = new CoroutineInfo(childRoutine, _logger);
                            _coroutinesToAdd.Add(child);
                            coroutine.AddChild(child);
                            coroutine.SetState(CoroutineState.Waiting);
                        }
                        else if (current != null)
                        {
                            coroutine.SetYieldInstruction(current);
                        }
                    }
                    else
                    {
                        coroutine.SetState(CoroutineState.Completed);
                        _coroutinesToRemove.Add(coroutine);
                    }
                }
                _coroutinesToAdd.Clear();
            }

            // コルーチンを更新
            var coroutinesToProcess = _coroutines.ToList();
            foreach (var coroutine in coroutinesToProcess)
            {
                if (coroutine.State != CoroutineState.Paused)
                {
                    ProcessCoroutine(coroutine, deltaTime);
                }
            }

            // 完了したコルーチンを削除
            if (_coroutinesToRemove.Count > 0)
            {
                foreach (var coroutine in _coroutinesToRemove.ToList())
                {
                    _coroutines.Remove(coroutine);
                }
                _coroutinesToRemove.Clear();
            }

            // 子コルーチンの状態を更新
            foreach (var coroutine in _coroutines.ToList())
            {
                if (coroutine.State == CoroutineState.Waiting && !coroutine.Children.Any())
                {
                    coroutine.SetState(CoroutineState.Running);
                }
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private bool ProcessCoroutine(CoroutineInfo coroutine, float deltaTime)
    {
        if (coroutine.State == CoroutineState.Completed)
        {
            return true;
        }

        if (coroutine.State == CoroutineState.Paused)
        {
            return false;
        }

        // 子コルーチンの処理
        if (coroutine.Children.Any())
        {
            var allChildrenCompleted = true;
            var anyChildProcessed = false;

            foreach (var child in coroutine.Children.ToList())
            {
                if (child.State != CoroutineState.Completed)
                {
                    allChildrenCompleted = false;
                    if (ProcessCoroutine(child, deltaTime))
                    {
                        anyChildProcessed = true;
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
                if (anyChildProcessed)
                {
                    coroutine.SetState(CoroutineState.Waiting);
                }
                return false;
            }
        }

        // 現在のyield instructionの処理
        if (coroutine.CurrentYieldInstruction != null)
        {
            if (!coroutine.CurrentYieldInstruction.Update(deltaTime))
            {
                return false;
            }
            coroutine.SetYieldInstruction(null);
        }

        // コルーチンの次のステップを実行
        if (!coroutine.Routine.MoveNext())
        {
            coroutine.SetState(CoroutineState.Completed);
            _coroutinesToRemove.Add(coroutine);
            return true;
        }

        var current = coroutine.Routine.Current;
        if (current is IEnumerator<IYieldInstruction> childRoutine)
        {
            var child = new CoroutineInfo(childRoutine, _logger);
            coroutine.AddChild(child);
            _coroutinesToAdd.Add(child);
            coroutine.SetState(CoroutineState.Waiting);
            return false;
        }
        else if (current is IYieldInstruction yieldInstr)
        {
            coroutine.SetYieldInstruction(yieldInstr);
            return false;
        }

        return false;
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
        if (coroutine == null) throw new ArgumentNullException(nameof(coroutine));
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
        if (coroutine == null) throw new ArgumentNullException(nameof(coroutine));
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