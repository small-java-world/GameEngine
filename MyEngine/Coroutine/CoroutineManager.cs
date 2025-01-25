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
        public List<CoroutineInfo> Children { get; } = new();
        public bool NeedsAdvance { get; set; }
        public CoroutineState PreviousState { get; set; }

        public CoroutineInfo(IEnumerator routine)
        {
            Routine = routine;
            State = CoroutineState.Initializing;
            NeedsAdvance = false;
            PreviousState = CoroutineState.Initializing;
        }

        public void AddChild(CoroutineInfo child)
        {
            Children.Add(child);
            child.Parent = this;
        }

        public void RemoveChild(CoroutineInfo child)
        {
            Children.Remove(child);
            child.Parent = null;
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
            var result = info.Routine.MoveNext();
            if (!result)
            {
                info.State = CoroutineState.Completed;
                OnCoroutineStateChanged(info);
                _coroutines.Remove(info);
                return;
            }

            // Handle nested coroutine
            if (info.Routine.Current is IEnumerator nestedRoutine)
            {
                var childInfo = new CoroutineInfo(nestedRoutine)
                {
                    State = CoroutineState.Initializing
                };
                info.AddChild(childInfo);
                OnCoroutineStateChanged(childInfo);
                
                childInfo.State = CoroutineState.Running;
                OnCoroutineStateChanged(childInfo);
                result = childInfo.Routine.MoveNext();
                if (!result)
                {
                    info.RemoveChild(childInfo);
                    return;
                }
                
                info.State = CoroutineState.Waiting;
                OnCoroutineStateChanged(info);
            }
        }

        public void Stop(IEnumerator routine)
        {
            var info = _coroutines.Find(x => x.Routine == routine);
            if (info != null)
            {
                foreach (var child in info.Children.ToList())
                {
                    Stop(child.Routine);
                }
                info.State = CoroutineState.Completed;
                OnCoroutineStateChanged(info);
                _coroutines.Remove(info);
            }
        }

        public void StopAll()
        {
            foreach (var info in _coroutines.ToList())
            {
                Stop(info.Routine);
            }
            _coroutinesToAdd.Clear();
        }

        public void Update(float deltaTime)
        {
            // Process active coroutines
            var completedRoutines = new List<CoroutineInfo>();
            var routinesToUpdate = _coroutines.Where(x => x.Parent == null).ToList();

            // Process root coroutines and their children
            foreach (var info in routinesToUpdate)
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
                // Pause all child coroutines first
                foreach (var child in info.Children.ToList())
                {
                    if (child.State != CoroutineState.Paused)
                    {
                        child.PreviousState = child.State;
                        child.State = CoroutineState.Paused;
                        OnCoroutineStateChanged(child);
                    }
                }

                // Then pause the parent
                info.PreviousState = info.State;
                info.State = CoroutineState.Paused;
                OnCoroutineStateChanged(info);
            }
        }

        public void Resume(IEnumerator routine)
        {
            _logger.LogDebug("Resuming coroutine");
            var info = _coroutines.Find(x => x.Routine == routine);
            if (info != null && info.State == CoroutineState.Paused)
            {
                // Resume the parent first
                info.State = info.PreviousState;
                OnCoroutineStateChanged(info);

                // Then resume all child coroutines
                foreach (var child in info.Children.ToList())
                {
                    if (child.State == CoroutineState.Paused)
                    {
                        child.State = child.PreviousState;
                        OnCoroutineStateChanged(child);
                    }
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

                // Process all child coroutines first
                var completedChildren = new List<CoroutineInfo>();
                var hasRunningChildren = false;
                foreach (var child in info.Children.ToList())
                {
                    if (!ProcessCoroutine(child, deltaTime))
                    {
                        completedChildren.Add(child);
                    }
                    else if (child.State == CoroutineState.Running || child.State == CoroutineState.Waiting)
                    {
                        hasRunningChildren = true;
                    }
                }

                // Remove completed children
                foreach (var child in completedChildren)
                {
                    info.RemoveChild(child);
                }

                // Handle parent coroutine state
                if (info.State == CoroutineState.Waiting)
                {
                    if (!hasRunningChildren)
                    {
                        info.State = CoroutineState.Running;
                        OnCoroutineStateChanged(info);
                        var result = info.Routine.MoveNext();
                        if (!result)
                        {
                            return false;
                        }

                        var current = info.Routine.Current;
                        if (current is IEnumerator nestedRoutine)
                        {
                            var childInfo = new CoroutineInfo(nestedRoutine)
                            {
                                State = CoroutineState.Initializing
                            };
                            info.AddChild(childInfo);
                            OnCoroutineStateChanged(childInfo);

                            childInfo.State = CoroutineState.Running;
                            OnCoroutineStateChanged(childInfo);
                            result = childInfo.Routine.MoveNext();
                            if (!result)
                            {
                                info.RemoveChild(childInfo);
                                return true;
                            }

                            info.State = CoroutineState.Waiting;
                            OnCoroutineStateChanged(info);
                        }
                    }
                    return true;
                }
                else if (info.State == CoroutineState.Running)
                {
                    var result = info.Routine.MoveNext();
                    if (!result)
                    {
                        return false;
                    }

                    var current = info.Routine.Current;
                    if (current is IEnumerator nestedRoutine)
                    {
                        var childInfo = new CoroutineInfo(nestedRoutine)
                        {
                            State = CoroutineState.Initializing
                        };
                        info.AddChild(childInfo);
                        OnCoroutineStateChanged(childInfo);

                        childInfo.State = CoroutineState.Running;
                        OnCoroutineStateChanged(childInfo);
                        result = childInfo.Routine.MoveNext();
                        if (!result)
                        {
                            info.RemoveChild(childInfo);
                            return true;
                        }

                        info.State = CoroutineState.Waiting;
                        OnCoroutineStateChanged(info);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing coroutine");
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