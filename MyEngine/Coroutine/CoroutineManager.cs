using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MyEngine.Coroutine
{
    public enum CoroutineState
    {
        Initializing,
        Running,
        Waiting,
        Paused,
        Completed
    }

    public class CoroutineInfo
    {
        public IEnumerator Routine { get; }
        public CoroutineState State { get; set; }
        public CoroutineInfo? Parent { get; set; }
        public CoroutineInfo? Child { get; set; }
        public bool NeedsAdvance { get; set; }
        public CoroutineState PreviousState { get; set; }

        public CoroutineInfo(IEnumerator routine)
        {
            Routine = routine;
            State = CoroutineState.Initializing;
            NeedsAdvance = false;
            PreviousState = CoroutineState.Initializing;
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
            _logger.LogDebug("Starting coroutine");
            var info = new CoroutineInfo(routine);
            _coroutines.Add(info);
            
            // Initialize
            info.State = CoroutineState.Initializing;
            OnCoroutineStateChanged(info);
            
            // Call first MoveNext
            info.State = CoroutineState.Running;
            OnCoroutineStateChanged(info);
            info.Routine.MoveNext();

            // Handle nested coroutine
            if (info.Routine.Current is IEnumerator nestedRoutine)
            {
                var childInfo = new CoroutineInfo(nestedRoutine)
                {
                    Parent = info,
                    State = CoroutineState.Initializing
                };
                info.Child = childInfo;
                OnCoroutineStateChanged(childInfo);
                
                childInfo.State = CoroutineState.Running;
                OnCoroutineStateChanged(childInfo);
                nestedRoutine.MoveNext();
                
                info.State = CoroutineState.Waiting;
                OnCoroutineStateChanged(info);
            }
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
            // Remove stopped coroutines
            foreach (var routine in _coroutinesToRemove)
            {
                var info = _coroutines.Find(x => x.Routine == routine);
                if (info != null)
                {
                    if (info.Child != null)
                    {
                        _coroutinesToRemove.Add(info.Child.Routine);
                    }
                    info.State = CoroutineState.Completed;
                    OnCoroutineStateChanged(info);
                    _coroutines.Remove(info);
                }
            }
            _coroutinesToRemove.Clear();

            // Active coroutine update
            var completedRoutines = new List<CoroutineInfo>();
            var routinesToUpdate = _coroutines.ToList();

            // Process parent coroutine first
            foreach (var info in routinesToUpdate.Where(x => x.Parent == null))
            {
                if (!ProcessCoroutine(info, deltaTime))
                {
                    completedRoutines.Add(info);
                }
            }

            // Remove completed coroutines
            foreach (var completed in completedRoutines)
            {
                completed.State = CoroutineState.Completed;
                OnCoroutineStateChanged(completed);
                _coroutines.Remove(completed);
            }
        }

        public void Pause(IEnumerator routine)
        {
            _logger.LogDebug("Pausing coroutine");
            var info = _coroutines.Find(x => x.Routine == routine);
            if (info != null && info.State != CoroutineState.Paused)
            {
                info.PreviousState = info.State;
                info.State = CoroutineState.Paused;
                OnCoroutineStateChanged(info);

                // Pause child coroutine as well
                if (info.Child != null)
                {
                    Pause(info.Child.Routine);
                }
            }
        }

        public void Resume(IEnumerator routine)
        {
            _logger.LogDebug("Resuming coroutine");
            var info = _coroutines.Find(x => x.Routine == routine);
            if (info != null && info.State == CoroutineState.Paused)
            {
                info.State = info.PreviousState;
                OnCoroutineStateChanged(info);

                // If was waiting, mark for advance
                if (info.State == CoroutineState.Waiting)
                {
                    info.NeedsAdvance = true;
                }

                // Resume child coroutine as well
                if (info.Child != null)
                {
                    Resume(info.Child.Routine);
                }
            }
        }

        private bool ProcessCoroutine(CoroutineInfo info, float deltaTime)
        {
            try
            {
                // Skip if paused
                if (info.State == CoroutineState.Paused)
                {
                    return true;
                }

                // Process child coroutine first
                if (info.Child != null)
                {
                    if (!ProcessCoroutine(info.Child, deltaTime))
                    {
                        // When child completes, set parent to waiting
                        info.Child = null;
                        info.State = CoroutineState.Waiting;
                        OnCoroutineStateChanged(info);
                        info.NeedsAdvance = true;
                        return true;
                    }
                    return true;
                }

                // If waiting and needs advance
                if (info.State == CoroutineState.Waiting && info.NeedsAdvance)
                {
                    info.State = CoroutineState.Running;
                    OnCoroutineStateChanged(info);
                    info.NeedsAdvance = false;
                    return info.Routine.MoveNext();
                }

                var current = info.Routine.Current;

                // Handle yield instruction
                if (current is IYieldInstruction yieldInstruction)
                {
                    if (info.State != CoroutineState.Waiting)
                    {
                        info.State = CoroutineState.Waiting;
                        OnCoroutineStateChanged(info);
                    }

                    if (yieldInstruction.Update(deltaTime))
                    {
                        info.State = CoroutineState.Running;
                        OnCoroutineStateChanged(info);
                        return info.Routine.MoveNext();
                    }
                    return true;
                }

                // Otherwise proceed to next step
                if (info.State != CoroutineState.Running)
                {
                    info.State = CoroutineState.Running;
                    OnCoroutineStateChanged(info);
                }
                return info.Routine.MoveNext();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing coroutine");
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