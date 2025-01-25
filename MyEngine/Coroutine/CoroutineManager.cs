using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MyEngine.Coroutine
{
    public class CoroutineManager
    {
        private readonly ILogger _logger;

        // アクティブなコルーチン
        private readonly List<CoroutineInfo> _coroutines = new();
        // 追加予定のコルーチン（次フレームで初期化）
        private readonly List<CoroutineInfo> _coroutinesToAdd = new();
        // 削除予定のコルーチン（当フレーム終了後に除去）
        private readonly List<CoroutineInfo> _coroutinesToRemove = new();
    
        public int ActiveCoroutineCount => _coroutines.Count;

        public CoroutineManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 新しいコルーチン開始。ここではMoveNext()を呼ばず、
        /// 次のUpdate()で初回実行する。
        /// </summary>
        public CoroutineInfo Start(IEnumerator<IYieldInstruction> routine)
        {
            if (routine == null) throw new ArgumentNullException(nameof(routine));
            var info = new CoroutineInfo(routine, _logger);
            _coroutinesToAdd.Add(info);
            return info;
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
            foreach (var c in _coroutines.ToList())
            {
                StopCoroutine(c);
            }
        }

        private void StopCoroutine(CoroutineInfo info)
        {
            if (!_coroutines.Contains(info)) return;
            info.SetState(CoroutineState.Completed);
            _coroutinesToRemove.Add(info);
        }

        public void Update(float deltaTime)
        {
            // 1) まず _coroutinesToAdd に登録されているものを追加・初回MoveNext
            if (_coroutinesToAdd.Count > 0)
            {
                foreach (var newInfo in _coroutinesToAdd)
                {
                    _coroutines.Add(newInfo);

                    // ここで1回だけMoveNext() → 初期yieldを処理
                    if (newInfo.Routine.MoveNext())
                    {
                        var current = newInfo.Routine.Current;
                        HandleCurrentYield(newInfo, current);
                    }
                    else
                    {
                        // すでに完了
                        newInfo.SetState(CoroutineState.Completed);
                        _coroutinesToRemove.Add(newInfo);
                    }
                }
                _coroutinesToAdd.Clear();
            }

            // 2) 既存のコルーチンを1フレーム分進める
            var copy = _coroutines.ToList();
            foreach (var c in copy)
            {
                if (c.State == CoroutineState.Paused || c.State == CoroutineState.Completed)
                {
                    // なにもしない
                    continue;
                }

                // 現在のyieldがある場合はまず待機処理
                if (c.CurrentYieldInstruction != null)
                {
                    bool done = c.CurrentYieldInstruction.Update(deltaTime);
                    if (!done)
                    {
                        // 今回はまだ続く → 次フレームへ
                        continue;
                    }
                    // 終わったら次へ
                    c.SetYieldInstruction(null);
                }

                // 次の MoveNext() を実行
                if (!c.Routine.MoveNext())
                {
                    // 完了
                    c.SetState(CoroutineState.Completed);
                    _coroutinesToRemove.Add(c);
                }
                else
                {
                    // 次のyieldを確認
                    var current = c.Routine.Current;
                    HandleCurrentYield(c, current);
                }
            }

            // 3) このフレームでCompleteになったものを削除
            if (_coroutinesToRemove.Count > 0)
            {
                foreach (var r in _coroutinesToRemove)
                {
                    _coroutines.Remove(r);
                }
                _coroutinesToRemove.Clear();
            }
        }

        private void HandleCurrentYield(CoroutineInfo info, object? current)
        {
            if (current is IEnumerator<IYieldInstruction> childRoutine)
            {
                // 子コルーチンは次フレームで初回MoveNextする
                var child = new CoroutineInfo(childRoutine, _logger);
                info.AddChild(child);
                _coroutinesToAdd.Add(child);
                // 親はひとまず待機 (次フレームに再開)
                info.SetYieldInstruction(null);
            }
            else if (current is IYieldInstruction y)
            {
                info.SetYieldInstruction(y);
            }
            else
            {
                // 何もない場合は特に処理しない
                info.SetYieldInstruction(null);
            }
        }

        public void Pause(IEnumerator<IYieldInstruction> routine)
        {
            if (routine == null) throw new ArgumentNullException(nameof(routine));
            var info = _coroutines.FirstOrDefault(c => c.Routine == routine);
            if (info != null)
            {
                PauseCoroutine(info);
            }
        }

        public void Resume(IEnumerator<IYieldInstruction> routine)
        {
            if (routine == null) throw new ArgumentNullException(nameof(routine));
            var info = _coroutines.FirstOrDefault(c => c.Routine == routine);
            if (info != null)
            {
                ResumeCoroutine(info);
            }
        }

        private void PauseCoroutine(CoroutineInfo info)
        {
            if (info.State == CoroutineState.Running || info.State == CoroutineState.Waiting)
            {
                info.SetState(CoroutineState.Paused);
            }
        }

        private void ResumeCoroutine(CoroutineInfo info)
        {
            if (info.State == CoroutineState.Paused)
            {
                info.SetState(CoroutineState.Running);
            }
        }
    }
}
