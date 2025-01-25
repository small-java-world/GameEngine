using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MyEngine.Coroutine;

public class CoroutineManager
{
    private readonly ILogger _logger;
    private readonly List<CoroutineInfo> _activeCoroutines = new();
    private readonly List<CoroutineInfo> _coroutinesToAdd = new();
    private readonly List<CoroutineInfo> _coroutinesToRemove = new();
    private bool _isProcessing;

    public int ActiveCoroutineCount => _activeCoroutines.Count;

    public CoroutineManager(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public CoroutineInfo Start(IEnumerator routine)
    {
        if (routine == null) throw new ArgumentNullException(nameof(routine));

        var coroutineInfo = new CoroutineInfo(routine, _logger);
        coroutineInfo.StateChanged += OnCoroutineStateChanged;

        if (_isProcessing)
        {
            _coroutinesToAdd.Add(coroutineInfo);
        }
        else
        {
            _activeCoroutines.Add(coroutineInfo);
        }

        return coroutineInfo;
    }

    private void OnCoroutineStateChanged(object? sender, CoroutineState state)
    {
        if (sender is CoroutineInfo coroutine)
        {
            switch (state)
            {
                case CoroutineState.Completed:
                    if (!_coroutinesToRemove.Contains(coroutine))
                    {
                        _coroutinesToRemove.Add(coroutine);
                    }
                    break;
            }
        }
    }

    public void Stop(IEnumerator routine)
    {
        if (routine == null) throw new ArgumentNullException(nameof(routine));

        var coroutine = _activeCoroutines.FirstOrDefault(c => c.Routine == routine);
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
        }
    }

    public void StopCoroutine(CoroutineInfo coroutine)
    {
        if (coroutine == null) throw new ArgumentNullException(nameof(coroutine));

        if (_isProcessing)
        {
            if (!_coroutinesToRemove.Contains(coroutine))
            {
                _coroutinesToRemove.Add(coroutine);
            }
        }
        else
        {
            _activeCoroutines.Remove(coroutine);
        }

        coroutine.SetState(CoroutineState.Completed);
    }

    public void StopAll()
    {
        if (_isProcessing)
        {
            foreach (var coroutine in _activeCoroutines)
            {
                if (!_coroutinesToRemove.Contains(coroutine))
                {
                    _coroutinesToRemove.Add(coroutine);
                }
                coroutine.SetState(CoroutineState.Completed);
            }
        }
        else
        {
            foreach (var coroutine in _activeCoroutines)
            {
                coroutine.SetState(CoroutineState.Completed);
            }
            _activeCoroutines.Clear();
        }
    }

    public void Update(float deltaTime)
    {
        _isProcessing = true;

        for (int i = _activeCoroutines.Count - 1; i >= 0; i--)
        {
            var coroutine = _activeCoroutines[i];
            ProcessCoroutine(coroutine, deltaTime);
        }

        _isProcessing = false;

        // 完了したコルーチンを削除
        foreach (var coroutine in _coroutinesToRemove)
        {
            coroutine.StateChanged -= OnCoroutineStateChanged;
            _activeCoroutines.Remove(coroutine);
        }
        _coroutinesToRemove.Clear();

        // 新しいコルーチンを追加
        foreach (var coroutine in _coroutinesToAdd)
        {
            _activeCoroutines.Add(coroutine);
        }
        _coroutinesToAdd.Clear();
    }

    private bool ProcessCoroutine(CoroutineInfo coroutine, float deltaTime)
    {
        if (coroutine.State == CoroutineState.Completed)
            return true;

        if (coroutine.State == CoroutineState.Paused)
            return false;

        // 子コルーチンの処理
        bool allChildrenCompleted = true;
        foreach (var child in coroutine.Children.ToList())
        {
            bool childCompleted = ProcessCoroutine(child, deltaTime);
            if (!childCompleted)
            {
                allChildrenCompleted = false;
            }
        }

        // 子コルーチンが実行中の場合は待機
        if (!allChildrenCompleted)
        {
            return false;
        }

        // YieldInstructionの処理
        var yieldInstruction = coroutine.CurrentYieldInstruction;
        if (yieldInstruction != null)
        {
            bool isCompleted = yieldInstruction.Update(deltaTime);
            if (!isCompleted)
            {
                return false;
            }
            coroutine.SetYieldInstruction(null);
        }

        // コルーチンの次のステップを実行
        try
        {
            if (!coroutine.IsFirstUpdate)
            {
                if (!coroutine.Routine.MoveNext())
                {
                    coroutine.SetState(CoroutineState.Completed);
                    return true;
                }
            }

            coroutine.MarkFirstUpdateComplete();

            // 子コルーチンの生成
            if (coroutine.Routine.Current is IEnumerator childRoutine)
            {
                var child = new CoroutineInfo(childRoutine, _logger);
                child.StateChanged += OnCoroutineStateChanged;
                coroutine.AddChild(child);
                _activeCoroutines.Add(child);
                return false;
            }

            // YieldInstructionの設定
            if (coroutine.Routine.Current is IYieldInstruction yieldInst)
            {
                coroutine.SetYieldInstruction(yieldInst);
                return false;
            }

            // 次のステップへ進む
            if (!coroutine.Routine.MoveNext())
            {
                coroutine.SetState(CoroutineState.Completed);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"コルーチン実行エラー: {ex.Message}");
            coroutine.SetState(CoroutineState.Completed);
            return true;
        }
    }

    public void Pause(IEnumerator routine)
    {
        if (routine == null) throw new ArgumentNullException(nameof(routine));

        var coroutine = _activeCoroutines.FirstOrDefault(c => c.Routine == routine);
        if (coroutine != null)
        {
            PauseCoroutine(coroutine);
        }
    }

    public void PauseCoroutine(CoroutineInfo coroutine)
    {
        if (coroutine == null) throw new ArgumentNullException(nameof(coroutine));
        coroutine.SetState(CoroutineState.Paused);
    }

    public void Resume(IEnumerator routine)
    {
        if (routine == null) throw new ArgumentNullException(nameof(routine));

        var coroutine = _activeCoroutines.FirstOrDefault(c => c.Routine == routine);
        if (coroutine != null)
        {
            ResumeCoroutine(coroutine);
        }
    }

    public void ResumeCoroutine(CoroutineInfo coroutine)
    {
        if (coroutine == null) throw new ArgumentNullException(nameof(coroutine));
        coroutine.SetState(CoroutineState.Running);
    }
} 