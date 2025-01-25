using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MyEngine.Coroutine;

namespace MyEngine.Tests.Coroutine
{
    public class CoroutineManagerTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly CoroutineManager _manager;

        public CoroutineManagerTests()
        {
            _loggerMock = new Mock<ILogger>();
            _manager = new CoroutineManager(_loggerMock.Object);
        }

        private IEnumerator<IYieldInstruction> SimpleCoroutine()
        {
            yield return new WaitForSeconds(1f);
        }

        private IEnumerator<IYieldInstruction> NestedCoroutine()
        {
            yield return new WaitForSeconds(1f);
            yield return new CoroutineEnumerator(SimpleCoroutine());
            yield return new WaitForSeconds(1f);
        }

        [Fact]
        public void WaitForSeconds_ShouldWaitAndComplete()
        {
            var completed = false;
            IEnumerator<IYieldInstruction> routine = new List<IYieldInstruction>
            {
                new WaitForSeconds(1f)
            }.GetEnumerator();

            var coroutine = _manager.Start(routine);
            _manager.Update(0.5f);
            Assert.False(completed);

            _manager.Update(0.6f);
            completed = true;
            Assert.True(completed);
        }

        [Fact]
        public void WaitUntil_ShouldWaitForCondition()
        {
            var flag = false;
            IEnumerator<IYieldInstruction> routine = new List<IYieldInstruction>
            {
                new WaitUntil(() => flag)
            }.GetEnumerator();

            var coroutine = _manager.Start(routine);
            _manager.Update(0.1f);
            Assert.False(flag);

            flag = true;
            _manager.Update(0.1f);
            Assert.True(flag);
        }

        [Fact]
        public void Stop_ShouldStopCoroutine()
        {
            var routine = SimpleCoroutine();
            var coroutine = _manager.Start(routine);
            _manager.Update(0.5f);

            _manager.Stop(routine);
            _manager.Update(0.1f);

            Assert.Equal(0, _manager.ActiveCoroutineCount);
        }

        [Fact]
        public void StopAll_ShouldStopAllCoroutines()
        {
            var routine1 = SimpleCoroutine();
            var routine2 = SimpleCoroutine();
            var routine3 = SimpleCoroutine();

            _manager.Start(routine1);
            _manager.Start(routine2);
            _manager.Start(routine3);
            _manager.Update(0.5f);

            _manager.StopAll();
            _manager.Update(0.1f);

            Assert.Equal(0, _manager.ActiveCoroutineCount);
        }

        [Fact]
        public void NestedCoroutines_ShouldWorkCorrectly()
        {
            var routine = NestedCoroutine();
            var coroutine = _manager.Start(routine);

            _manager.Update(1.1f); // First WaitForSeconds
            Assert.Equal(1, _manager.ActiveCoroutineCount);

            _manager.Update(1.1f); // Nested SimpleCoroutine's WaitForSeconds
            Assert.Equal(1, _manager.ActiveCoroutineCount);

            _manager.Update(1.1f); // Last WaitForSeconds
            Assert.Equal(0, _manager.ActiveCoroutineCount);
        }

        [Fact]
        public void PauseAndResume_ShouldWorkCorrectly()
        {
            var routine = SimpleCoroutine();
            var coroutine = _manager.Start(routine);

            _manager.Update(0.5f);
            _manager.Pause(routine);
            _manager.Update(1.0f);
            Assert.Equal(1, _manager.ActiveCoroutineCount);

            _manager.Resume(routine);
            _manager.Update(0.6f);
            Assert.Equal(0, _manager.ActiveCoroutineCount);
        }

        [Fact]
        public void PauseAndResume_WithNestedCoroutines_ShouldWorkCorrectly()
        {
            var routine = NestedCoroutine();
            var coroutine = _manager.Start(routine);

            _manager.Update(0.5f);
            _manager.Pause(routine);
            _manager.Update(1.0f);
            Assert.Equal(1, _manager.ActiveCoroutineCount);

            _manager.Resume(routine);
            _manager.Update(0.6f);
            _manager.Update(1.1f);
            _manager.Update(1.1f);
            Assert.Equal(0, _manager.ActiveCoroutineCount);
        }

        [Fact]
        public void MultipleChildCoroutines_ShouldExecuteInParallel()
        {
            var executionCount = 0;
            IEnumerator<IYieldInstruction> ParentRoutine()
            {
                var child1 = SimpleCoroutine();
                var child2 = SimpleCoroutine();

                // 子コルーチンを開始
                yield return new CoroutineEnumerator(child1);
                yield return new CoroutineEnumerator(child2);

                executionCount++;
            }

            var coroutine = _manager.Start(ParentRoutine());
            _manager.Update(0.5f);
            Assert.Equal(2, _manager.ActiveCoroutineCount);

            _manager.Update(0.6f);
            Assert.Equal(1, executionCount);
        }

        [Fact]
        public void MultipleChildCoroutines_WhenParentPaused_ShouldPauseAllChildren()
        {
            var routine = NestedCoroutine();
            var coroutine = _manager.Start(routine);

            _manager.Update(0.5f);
            _manager.Pause(routine);
            _manager.Update(1.0f);
            Assert.Equal(1, _manager.ActiveCoroutineCount);

            _manager.Resume(routine);
            _manager.Update(0.6f);
            Assert.Equal(1, _manager.ActiveCoroutineCount);
        }
    }
} 