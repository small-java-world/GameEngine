using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MyEngine.Coroutine;

public class CoroutineManager
{
    private readonly List<CoroutineInfo> _coroutines = new();
    private readonly List<CoroutineInfo> _coroutinesToRemove = new();
    private readonly ILogger _logger;

    public event Action<CoroutineInfo>? CoroutineStateChanged;

    public int ActiveCoroutineCount => _coroutines.Count;

    public CoroutineManager(ILogger logger)
    {
        _logger = logger;
    }

    public void Start(IEnumerator routine)
    {
        var info = new CoroutineInfo(routine, _logger);
        info.OnStateChanged += OnCoroutineStateChanged;
        _coroutines.Add(info);
        _logger.LogDebug("Starting coroutine");
        OnCoroutineStateChanged(info);
    }

    public void Stop(IEnumerator routine)
    {
        var info = _coroutines.FirstOrDefault(c => c.Routine == routine);
        if (info != null)
        {
            StopCoroutineInternal(info);
            _coroutines.Remove(info);
            _coroutinesToRemove.Remove(info);
        }
    }

    public void StopAll()
    {
        foreach (var coroutine in _coroutines.ToList())
        {
            StopCoroutineInternal(coroutine);
        }
        _coroutines.Clear();
        _coroutinesToRemove.Clear();
    }

    private void StopCoroutineInternal(CoroutineInfo info)
    {
        foreach (var child in info.Children.ToList())
        {
            StopCoroutineInternal(child);
            info.RemoveChild(child);
        }
        info.SetState(CoroutineState.Completed);
    }

    public void Update(float deltaTime)
    {
        _coroutinesToRemove.Clear();

        // アクティブなコルーチンを処理
        var coroutines = _coroutines.ToList();
        foreach (var coroutine in coroutines)
        {
            if (coroutine.State != CoroutineState.Completed)
            {
                ProcessCoroutine(coroutine, deltaTime);
            }
        }

        // 完了したコルーチンを削除
        foreach (var coroutine in _coroutinesToRemove)
        {
            _coroutines.Remove(coroutine);
        }
    }

    private bool ProcessCoroutine(CoroutineInfo info, float deltaTime)
    {
        if (info.State == CoroutineState.Paused || info.State == CoroutineState.Completed)
        {
            return info.State != CoroutineState.Completed;
        }

        // 子コルーチンの処理
        var hasRunningChildren = false;
        foreach (var child in info.Children.ToList())
        {
            var childResult = ProcessCoroutine(child, deltaTime);
            if (!childResult || child.State == CoroutineState.Completed)
            {
                info.RemoveChild(child);
                if (!_coroutinesToRemove.Contains(child))
                {
                    _coroutinesToRemove.Add(child);
                }
            }
            else if (child.State == CoroutineState.Running || child.State == CoroutineState.Waiting)
            {
                hasRunningChildren = true;
            }
        }

        // 子コルーチンが実行中の場合は待機
        if (hasRunningChildren)
        {
            if (info.State != CoroutineState.Waiting)
            {
                info.SetState(CoroutineState.Waiting);
            }
            return true;
        }

        // YieldInstructionの処理
        if (info.CurrentYieldInstruction != null)
        {
            var yieldResult = info.CurrentYieldInstruction.Update(deltaTime);
            if (!yieldResult)
            {
                if (info.State != CoroutineState.Waiting)
                {
                    info.SetState(CoroutineState.Waiting);
                }
                return true;
            }
            
            info.SetYieldInstruction(null);
            if (info.State != CoroutineState.Running)
            {
                info.SetState(CoroutineState.Running);
            }
        }

        // コルーチンの次のステップを実行
        try
        {
            if (!info.Routine.MoveNext())
            {
                info.SetState(CoroutineState.Completed);
                if (!_coroutinesToRemove.Contains(info))
                {
                    _coroutinesToRemove.Add(info);
                }
                return false;
            }

            // 子コルーチンの生成
            if (info.Routine.Current is IEnumerator childRoutine)
            {
                var child = new CoroutineInfo(childRoutine, _logger);
                child.OnStateChanged += OnCoroutineStateChanged;
                info.AddChild(child);
                if (!_coroutines.Contains(child))
                {
                    _coroutines.Add(child);
                    if (info.State == CoroutineState.Paused)
                    {
                        child.SetState(CoroutineState.Paused);
                    }
                    else
                    {
                        ProcessCoroutine(child, deltaTime);
                    }
                }
                return true;
            }

            // YieldInstructionの設定
            if (info.Routine.Current is IYieldInstruction yieldInstruction)
            {
                info.SetYieldInstruction(yieldInstruction);
                ProcessCoroutine(info, deltaTime);
                return true;
            }

            // 通常のステップ実行
            if (info.State != CoroutineState.Running)
            {
                info.SetState(CoroutineState.Running);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"コルーチン実行エラー: {ex.Message}");
            info.SetState(CoroutineState.Completed);
            if (!_coroutinesToRemove.Contains(info))
            {
                _coroutinesToRemove.Add(info);
            }
            return false;
        }
    }

    private void OnCoroutineStateChanged(CoroutineInfo info)
    {
        _logger.LogDebug($"コルーチン状態変更: {info.State}");
        CoroutineStateChanged?.Invoke(info);
    }

    public void Pause(IEnumerator routine)
    {
        var info = _coroutines.FirstOrDefault(c => c.Routine == routine);
        if (info != null)
        {
            PauseCoroutineInternal(info);
        }
    }

    private void PauseCoroutineInternal(CoroutineInfo info)
    {
        if (info.State != CoroutineState.Paused && info.State != CoroutineState.Completed)
        {
            info.SetState(CoroutineState.Paused);
            foreach (var child in info.Children)
            {
                PauseCoroutineInternal(child);
            }
        }
    }

    public void Resume(IEnumerator routine)
    {
        var info = _coroutines.FirstOrDefault(c => c.Routine == routine);
        if (info != null)
        {
            ResumeCoroutineInternal(info);
        }
    }

    private void ResumeCoroutineInternal(CoroutineInfo info)
    {
        if (info.State == CoroutineState.Paused)
        {
            var newState = info.CurrentYieldInstruction != null ? CoroutineState.Waiting : CoroutineState.Running;
            info.SetState(newState);
            foreach (var child in info.Children)
            {
                ResumeCoroutineInternal(child);
            }
            if (newState == CoroutineState.Waiting)
            {
                ProcessCoroutine(info, 0);
            }
        }
    }
} 