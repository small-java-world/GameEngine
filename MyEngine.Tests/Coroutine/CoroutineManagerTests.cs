using System.Collections;
using Microsoft.Extensions.Logging;
using Xunit;
using MyEngine.Coroutine;

namespace MyEngine.Tests.Coroutine
{
    public class CoroutineManagerTests
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly CoroutineManager _coroutineManager;

        public CoroutineManagerTests()
        {
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddConsole();
            });

            _coroutineManager = new CoroutineManager(
                _loggerFactory.CreateLogger<CoroutineManager>()
            );
        }

        [Fact]
        public void SimpleCoroutine_ShouldComplete()
        {
            bool completed = false;
            IEnumerator TestRoutine()
            {
                completed = true;
                yield break;
            }

            _coroutineManager.Start(TestRoutine());
            _coroutineManager.Update(0.1f);

            Assert.True(completed);
            Assert.Equal(0, _coroutineManager.ActiveCoroutineCount);
        }

        [Fact]
        public void WaitForSeconds_ShouldWaitAndComplete()
        {
            bool completed = false;
            IEnumerator TestRoutine()
            {
                yield return new WaitForSeconds(1.0f);
                completed = true;
            }

            _coroutineManager.Start(TestRoutine());
            
            // 0.5秒経過
            _coroutineManager.Update(0.5f);
            Assert.False(completed);
            Assert.Equal(1, _coroutineManager.ActiveCoroutineCount);

            // さらに0.6秒経過（合計1.1秒）
            _coroutineManager.Update(0.6f);
            Assert.True(completed);
            Assert.Equal(0, _coroutineManager.ActiveCoroutineCount);
        }

        [Fact]
        public void WaitUntil_ShouldWaitForCondition()
        {
            bool flag = false;
            bool completed = false;
            IEnumerator TestRoutine()
            {
                yield return new WaitUntil(() => flag);
                completed = true;
            }

            _coroutineManager.Start(TestRoutine());
            
            // フラグがfalseの間は待機
            _coroutineManager.Update(0.1f);
            Assert.False(completed);
            Assert.Equal(1, _coroutineManager.ActiveCoroutineCount);

            // フラグをtrueにすると完了
            flag = true;
            _coroutineManager.Update(0.1f);
            Assert.True(completed);
            Assert.Equal(0, _coroutineManager.ActiveCoroutineCount);
        }

        [Fact]
        public void StopCoroutine_ShouldStopImmediately()
        {
            bool completed = false;
            IEnumerator TestRoutine()
            {
                yield return new WaitForSeconds(1.0f);
                completed = true;
            }

            var routine = TestRoutine();
            _coroutineManager.Start(routine);
            _coroutineManager.Update(0.1f);
            Assert.Equal(1, _coroutineManager.ActiveCoroutineCount);

            _coroutineManager.Stop(routine);
            _coroutineManager.Update(0.1f);
            Assert.False(completed);
            Assert.Equal(0, _coroutineManager.ActiveCoroutineCount);
        }

        [Fact]
        public void StopAll_ShouldStopAllCoroutines()
        {
            int completedCount = 0;
            IEnumerator TestRoutine()
            {
                yield return new WaitForSeconds(1.0f);
                completedCount++;
            }

            // 3つのコルーチンを開始
            _coroutineManager.Start(TestRoutine());
            _coroutineManager.Start(TestRoutine());
            _coroutineManager.Start(TestRoutine());
            _coroutineManager.Update(0.1f);
            Assert.Equal(3, _coroutineManager.ActiveCoroutineCount);

            // 全て停止
            _coroutineManager.StopAll();
            _coroutineManager.Update(0.1f);
            Assert.Equal(0, completedCount);
            Assert.Equal(0, _coroutineManager.ActiveCoroutineCount);
        }

        [Fact]
        public void NestedCoroutines_ShouldWorkCorrectly()
        {
            int sequence = 0;
            IEnumerator InnerRoutine()
            {
                yield return new WaitForSeconds(0.5f);
                sequence++;
            }

            IEnumerator OuterRoutine()
            {
                sequence++;
                yield return InnerRoutine();
                sequence++;
            }

            _coroutineManager.Start(OuterRoutine());
            
            Assert.Equal(1, sequence); // 外側のコルーチンが開始
            
            _coroutineManager.Update(0.3f); // 待機中
            Assert.Equal(1, sequence);
            
            _coroutineManager.Update(0.3f); // 内側のコルーチンが完了
            Assert.Equal(2, sequence);
            
            _coroutineManager.Update(0.1f); // 外側のコルーチンが完了
            Assert.Equal(3, sequence);
            Assert.Equal(0, _coroutineManager.ActiveCoroutineCount);
        }
    }
} 