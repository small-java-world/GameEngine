using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MyEngine.Coroutine
{
    public class CoroutineManager
    {
        private readonly ILogger<CoroutineManager> _logger;
        private readonly List<IEnumerator> _coroutines = new();
        private readonly List<IEnumerator> _coroutinesToAdd = new();
        private readonly List<IEnumerator> _coroutinesToRemove = new();

        public CoroutineManager(ILogger<CoroutineManager> logger)
        {
            _logger = logger;
        }

        public void Start(IEnumerator routine)
        {
            _logger.LogDebug("コルーチン開始");
            _coroutinesToAdd.Add(routine);
            routine.MoveNext(); // 最初のyieldまで進める
        }

        public void Stop(IEnumerator routine)
        {
            _logger.LogDebug("コルーチン停止");
            _coroutinesToRemove.Add(routine);
        }

        public void StopAll()
        {
            _logger.LogInformation("全コルーチン停止");
            _coroutinesToRemove.AddRange(_coroutines);
            _coroutinesToAdd.Clear();
        }

        public void Update(float deltaTime)
        {
            // 新しいコルーチンを追加
            if (_coroutinesToAdd.Count > 0)
            {
                _coroutines.AddRange(_coroutinesToAdd);
                _coroutinesToAdd.Clear();
            }

            // 停止要求のあるコルーチンを削除
            if (_coroutinesToRemove.Count > 0)
            {
                foreach (var routine in _coroutinesToRemove)
                {
                    _coroutines.Remove(routine);
                }
                _coroutinesToRemove.Clear();
            }

            // 各コルーチンを更新
            for (int i = _coroutines.Count - 1; i >= 0; i--)
            {
                var routine = _coroutines[i];
                bool shouldContinue = true;

                try
                {
                    var current = routine.Current;
                    if (current is IYieldInstruction yieldInstruction)
                    {
                        shouldContinue = yieldInstruction.Update(deltaTime);
                    }
                    else if (current is IEnumerator nestedRoutine)
                    {
                        // ネストされたコルーチンを初回実行または継続実行
                        if (nestedRoutine.Current == null)
                        {
                            if (!nestedRoutine.MoveNext())
                            {
                                // ネストされたコルーチンが即座に完了した場合
                                shouldContinue = routine.MoveNext();
                                continue;
                            }
                        }

                        // ネストされたコルーチンの現在の状態を処理
                        if (nestedRoutine.Current is IYieldInstruction nestedYield)
                        {
                            // 待機命令を処理
                            shouldContinue = nestedYield.Update(deltaTime);
                            if (shouldContinue)
                            {
                                // 待機が完了したら次のステップへ
                                if (!nestedRoutine.MoveNext())
                                {
                                    // ネストされたコルーチンが完了したら親コルーチンを進める
                                    shouldContinue = routine.MoveNext();
                                    continue;
                                }
                            }
                        }
                        else if (nestedRoutine.Current == null)
                        {
                            // ネストされたコルーチンが完了したら親コルーチンを進める
                            shouldContinue = routine.MoveNext();
                            continue;
                        }
                        
                        // ネストされたコルーチンがまだ実行中
                        shouldContinue = false;
                    }

                    if (shouldContinue && !routine.MoveNext())
                    {
                        _logger.LogDebug("コルーチン完了");
                        _coroutines.RemoveAt(i);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "コルーチン実行中にエラー発生");
                    _coroutines.RemoveAt(i);
                }
            }
        }

        public int ActiveCoroutineCount => _coroutines.Count;
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