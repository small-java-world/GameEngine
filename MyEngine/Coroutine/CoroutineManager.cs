using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MyEngine.Coroutine;

public class CoroutineManager
{
    private readonly ILogger<CoroutineManager> _logger;
    private readonly List<CoroutineInfo> _coroutines = new();
    private readonly List<CoroutineInfo> _coroutinesToRemove = new();

    public event EventHandler<CoroutineInfo>? CoroutineStateChanged;

    public int ActiveCoroutineCount => _coroutines.Count;

    public CoroutineManager(ILogger<CoroutineManager> logger)
    {
        _logger = logger;
    }

    private void OnCoroutineStateChanged(CoroutineInfo info)
    {
        _logger.LogDebug($"コルーチン状態変更: {info.State}");
        CoroutineStateChanged?.Invoke(this, info);
    }

    public void Start(IEnumerator routine)
    {
        _logger.LogDebug("Starting coroutine");
        var info = new CoroutineInfo(routine);
        _coroutines.Add(info);
        info.OnStateChanged += (coroutine) => OnCoroutineStateChanged(coroutine);
        
        OnCoroutineStateChanged(info);
        ExecuteNextStep(info);
    }

    public void Stop(IEnumerator routine)
    {
        var info = _coroutines.Find(x => x.Routine == routine);
        if (info != null)
        {
            StopCoroutine(info);
        }
    }

    private void StopCoroutine(CoroutineInfo info)
    {
        foreach (var child in info.Children.ToList())
        {
            StopCoroutine(child);
        }
        info.SetState(CoroutineState.Completed);
        _coroutinesToRemove.Add(info);
    }

    public void StopAll()
    {
        foreach (var info in _coroutines.ToList())
        {
            StopCoroutine(info);
        }
        _coroutines.Clear();
        _coroutinesToRemove.Clear();
    }

    public void Update(float deltaTime)
    {
        // ルートコルーチンを優先して処理
        foreach (var info in _coroutines.Where(x => x.Parent == null).ToList())
        {
            ExecuteCoroutine(info, deltaTime);
        }

        // 完了したコルーチンを削除
        foreach (var completed in _coroutinesToRemove)
        {
            _coroutines.Remove(completed);
        }
        _coroutinesToRemove.Clear();
    }

    public void Pause(IEnumerator routine)
    {
        var info = _coroutines.Find(c => c.Routine == routine);
        if (info != null)
        {
            PauseCoroutine(info);
        }
    }

    private void PauseCoroutine(CoroutineInfo info)
    {
        if (info.State != CoroutineState.Paused)
        {
            // 子コルーチンを先に一時停止
            foreach (var child in info.Children)
            {
                PauseCoroutine(child);
            }

            info.SetState(CoroutineState.Paused);
        }
    }

    public void Resume(IEnumerator routine)
    {
        var info = _coroutines.Find(c => c.Routine == routine);
        if (info != null)
        {
            ResumeCoroutine(info);
        }
    }

    private void ResumeCoroutine(CoroutineInfo info)
    {
        if (info.State == CoroutineState.Paused)
        {
            info.SetState(CoroutineState.Running);

            // 子コルーチンを後で再開
            foreach (var child in info.Children)
            {
                ResumeCoroutine(child);
            }
        }
    }

    private bool ExecuteCoroutine(CoroutineInfo info, float deltaTime)
    {
        try
        {
            return ExecuteCoroutineInternal(info, deltaTime);
        }
        catch (Exception ex)
        {
            LogCoroutineError(info, ex);
            info.SetState(CoroutineState.Completed);
            return false;
        }
    }

    private bool ExecuteCoroutineInternal(CoroutineInfo info, float deltaTime)
    {
        // 一時停止中は処理しない
        if (info.State == CoroutineState.Paused)
        {
            return true;
        }

        // 子コルーチンの処理
        var hasActiveChildren = ProcessChildren(info, deltaTime);
        if (hasActiveChildren)
        {
            return true;
        }

        // YieldInstructionの処理
        if (info.CurrentYieldInstruction != null)
        {
            if (!info.CurrentYieldInstruction.Update(deltaTime))
            {
                return true;
            }
            info.SetYieldInstruction(null);
        }

        // コルーチンの次のステップを実行
        return ExecuteNextStep(info);
    }

    private bool ProcessChildren(CoroutineInfo info, float deltaTime)
    {
        var hasActiveChildren = false;
        var completedChildren = new List<CoroutineInfo>();

        foreach (var child in info.Children.ToList())
        {
            if (!ExecuteCoroutine(child, deltaTime))
            {
                completedChildren.Add(child);
            }
            else if (child.State != CoroutineState.Completed)
            {
                hasActiveChildren = true;
            }
        }

        foreach (var child in completedChildren)
        {
            info.RemoveChild(child);
            _coroutinesToRemove.Add(child);
        }

        return hasActiveChildren;
    }

    private bool ExecuteNextStep(CoroutineInfo info)
    {
        if (info.State != CoroutineState.Running)
        {
            return true;
        }

        try
        {
            if (!info.Routine.MoveNext())
            {
                info.SetState(CoroutineState.Completed);
                _coroutinesToRemove.Add(info);
                return false;
            }

            var current = info.Routine.Current;
            if (current is IEnumerator childRoutine)
            {
                var child = new CoroutineInfo(childRoutine, info);
                info.AddChild(child);
                child.OnStateChanged += (coroutine) => OnCoroutineStateChanged(coroutine);
                _coroutines.Add(child);
                OnCoroutineStateChanged(child);
                return true;
            }

            if (current is IYieldInstruction yieldInstruction)
            {
                info.SetYieldInstruction(yieldInstruction);
                return true;
            }

            return true;
        }
        catch (Exception ex)
        {
            LogCoroutineError(info, ex);
            info.SetState(CoroutineState.Completed);
            _coroutinesToRemove.Add(info);
            return false;
        }
    }

    private void LogCoroutineError(CoroutineInfo info, Exception ex)
    {
        _logger.LogError(
            "コルーチンエラー: State={State}, Parent={Parent}, Error={Error}",
            info.State,
            info.Parent?.State,
            ex.ToString()
        );
    }
} 