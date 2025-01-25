using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MyEngine.Coroutine
{
    public enum CoroutineState
    {
        初期化中,
        実行中,
        待機中,
        完了
    }

    public class CoroutineInfo
    {
        public IEnumerator Routine { get; }
        public CoroutineState State { get; set; }
        public CoroutineInfo? Parent { get; set; }
        public CoroutineInfo? Child { get; set; }

        public CoroutineInfo(IEnumerator routine)
        {
            Routine = routine;
            State = CoroutineState.初期化中;
        }
    }

    public class CoroutineManager
    {
        private readonly ILogger<CoroutineManager> _logger;
        private readonly List<CoroutineInfo> _coroutines = new();
        private readonly List<IEnumerator> _coroutinesToAdd = new();
        private readonly List<IEnumerator> _coroutinesToRemove = new();

        public event EventHandler<CoroutineInfo>? CoroutineStateChanged;

        public int ActiveCoroutineCount => _coroutines.Count + _coroutinesToAdd.Count;

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
            _logger.LogDebug("コルーチン開始");
            var info = new CoroutineInfo(routine);
            _coroutines.Add(info);
            
            // 初期化
            info.State = CoroutineState.初期化中;
            OnCoroutineStateChanged(info);
            
            // 最初のMoveNextを呼び出し
            info.State = CoroutineState.実行中;
            OnCoroutineStateChanged(info);
            info.Routine.MoveNext();
        }

        public void Stop(IEnumerator routine)
        {
            _coroutinesToRemove.Add(routine);
        }

        public void StopAll()
        {
            foreach (var info in _coroutines)
            {
                _coroutinesToRemove.Add(info.Routine);
            }
            _coroutinesToAdd.Clear();
        }

        public void Update(float deltaTime)
        {
            // 停止したコルーチンの削除
            foreach (var routine in _coroutinesToRemove)
            {
                var info = _coroutines.Find(x => x.Routine == routine);
                if (info != null)
                {
                    if (info.Child != null)
                    {
                        _coroutinesToRemove.Add(info.Child.Routine);
                    }
                    info.State = CoroutineState.完了;
                    OnCoroutineStateChanged(info);
                    _coroutines.Remove(info);
                }
            }
            _coroutinesToRemove.Clear();

            // アクティブなコルーチンの更新
            var completedRoutines = new List<CoroutineInfo>();
            foreach (var info in _coroutines.ToList())
            {
                if (!ProcessCoroutine(info, deltaTime))
                {
                    completedRoutines.Add(info);
                }
            }

            // 完了したコルーチンの削除
            foreach (var completed in completedRoutines)
            {
                completed.State = CoroutineState.完了;
                OnCoroutineStateChanged(completed);
                _coroutines.Remove(completed);
            }
        }

        private bool ProcessCoroutine(CoroutineInfo info, float deltaTime)
        {
            try
            {
                // 子コルーチンの処理を優先
                if (info.Child != null)
                {
                    if (!ProcessCoroutine(info.Child, deltaTime))
                    {
                        // 子コルーチンが完了したら、その結果を処理
                        var childResult = info.Child.Routine.Current;
                        info.Child = null;
                        
                        // 親コルーチンを再開
                        info.State = CoroutineState.実行中;
                        OnCoroutineStateChanged(info);
                        return info.Routine.MoveNext();
                    }
                    return true;
                }

                var current = info.Routine.Current;

                // ネストされたコルーチンの処理
                if (current is IEnumerator nestedRoutine)
                {
                    var childInfo = new CoroutineInfo(nestedRoutine)
                    {
                        Parent = info,
                        State = CoroutineState.初期化中
                    };
                    info.Child = childInfo;
                    OnCoroutineStateChanged(childInfo);
                    
                    childInfo.State = CoroutineState.実行中;
                    OnCoroutineStateChanged(childInfo);
                    nestedRoutine.MoveNext();
                    
                    info.State = CoroutineState.待機中;
                    OnCoroutineStateChanged(info);
                    return true;
                }

                // 待機命令の処理
                if (current is IYieldInstruction yieldInstruction)
                {
                    if (info.State != CoroutineState.待機中)
                    {
                        info.State = CoroutineState.待機中;
                        OnCoroutineStateChanged(info);
                    }

                    if (yieldInstruction.Update(deltaTime))
                    {
                        info.State = CoroutineState.実行中;
                        OnCoroutineStateChanged(info);
                        return info.Routine.MoveNext();
                    }
                    return true;
                }

                // その他の場合は次のステップへ
                if (info.State != CoroutineState.実行中)
                {
                    info.State = CoroutineState.実行中;
                    OnCoroutineStateChanged(info);
                }
                return info.Routine.MoveNext();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "コルーチン実行エラー");
                return false;
            }
        }
    }

    public interface IYieldInstruction
    {
        bool Update(float deltaTime);
    }

    public class WaitForSeconds : IYieldInstruction
    {
        private float _remainingTime;

        public WaitForSeconds(float seconds)
        {
            _remainingTime = seconds;
        }

        public bool Update(float deltaTime)
        {
            _remainingTime -= deltaTime;
            return _remainingTime <= 0;
        }
    }

    public class WaitUntil : IYieldInstruction
    {
        private readonly Func<bool> _predicate;

        public WaitUntil(Func<bool> predicate)
        {
            _predicate = predicate;
        }

        public bool Update(float deltaTime)
        {
            return _predicate();
        }
    }
} 