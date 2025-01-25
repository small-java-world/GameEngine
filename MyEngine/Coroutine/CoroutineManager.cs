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

        // 新しいコルーチンを追加
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
                        var child = new CoroutineInfo(childRoutine, _logger);
                        coroutine.AddChild(child);
                        _coroutines.Add(child);

                        // 子コルーチンの初期実行
                        if (childRoutine.MoveNext())
                        {
                            var childCurrent = childRoutine.Current;
                            if (childCurrent != null)
                            {
                                child.SetYieldInstruction(childCurrent);
                            }
                        }
                        else
                        {
                            child.SetState(CoroutineState.Completed);
                            _coroutinesToRemove.Add(child);
                        }

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

        _isProcessing = false;
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
        var hasRunningChildren = false;
        foreach (var child in coroutine.Children.ToList())
        {
            if (child.State != CoroutineState.Completed)
            {
                var childCompleted = ProcessCoroutine(child, deltaTime);
                if (!childCompleted)
                {
                    hasRunningChildren = true;
                }
                else
                {
                    coroutine.RemoveChild(child);
                    _coroutinesToRemove.Add(child);
                }
            }
        }

        // 子コルーチンが実行中の場合は待機
        if (hasRunningChildren)
        {
            coroutine.SetState(CoroutineState.Waiting);
            return false;
        }

        // 現在のyield instructionの処理
        if (coroutine.CurrentYieldInstruction != null)
        {
            try
            {
                if (!coroutine.CurrentYieldInstruction.Update(deltaTime))
                {
                    return false;
                }
                coroutine.SetYieldInstruction(null);
            }
            catch (ObjectDisposedException)
            {
                // 既に破棄されたオブジェクトの場合は次に進む
                coroutine.SetYieldInstruction(null);
            }
        }

        // コルーチンの実行
        if (coroutine.State == CoroutineState.Running || coroutine.State == CoroutineState.Waiting)
        {
            try
            {
                if (!coroutine.IsFirstUpdate)
                {
                    if (!coroutine.Routine.MoveNext())
                    {
                        coroutine.SetState(CoroutineState.Completed);
                        _coroutinesToRemove.Add(coroutine);
                        return true;
                    }
                }

                coroutine.MarkFirstUpdateComplete();

                var current = coroutine.Routine.Current;
                if (current is IEnumerator<IYieldInstruction> childRoutine)
                {
                    var child = new CoroutineInfo(childRoutine, _logger);
                    coroutine.AddChild(child);
                    _coroutines.Add(child);

                    if (coroutine.State == CoroutineState.Paused)
                    {
                        child.SetState(CoroutineState.Paused);
                    }
                    else
                    {
                        // 子コルーチンの初期実行
                        if (childRoutine.MoveNext())
                        {
                            var childCurrent = childRoutine.Current;
                            if (childCurrent != null)
                            {
                                child.SetYieldInstruction(childCurrent);
                            }
                        }
                        else
                        {
                            child.SetState(CoroutineState.Completed);
                            _coroutinesToRemove.Add(child);
                            return true; // 子コルーチンが即座に完了した場合は親も進める
                        }
                    }

                    coroutine.SetState(CoroutineState.Waiting);
                    return false;
                }
                else if (current != null)
                {
                    coroutine.SetYieldInstruction(current);
                    return false;
                }

                // 次のステップへ進む
                if (!coroutine.Routine.MoveNext())
                {
                    coroutine.SetState(CoroutineState.Completed);
                    _coroutinesToRemove.Add(coroutine);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"コルーチン実行エラー: {ex.Message}");
                coroutine.SetState(CoroutineState.Completed);
                _coroutinesToRemove.Add(coroutine);
                return true;
            }
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
        }
    }

    public void ResumeCoroutine(CoroutineInfo coroutine)
    {
        if (coroutine == null) throw new ArgumentNullException(nameof(coroutine));
        if (coroutine.State == CoroutineState.Paused)
        {
            coroutine.SetState(CoroutineState.Running);
        }
    }
} 