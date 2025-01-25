using Microsoft.Extensions.Logging;

namespace MyEngine.Core
{
    public abstract class Scene
    {
        private Engine? _engine;
        private ILogger? _logger;

        public Engine Engine 
        { 
            get => _engine ?? throw new InvalidOperationException("Scene is not initialized");
            internal set => _engine = value;
        }

        protected ILogger Logger
        {
            get => _logger ?? throw new InvalidOperationException("Scene is not initialized");
            private set => _logger = value;
        }

        public virtual void OnStart() {}
        public virtual void OnUpdate(float deltaTime) {}
        public virtual void OnDraw() {}
        public virtual void OnStop() {}

        internal void Initialize(Engine engine, ILogger logger)
        {
            Engine = engine;
            Logger = logger;
        }

        protected void ChangeScene<T>() where T : Scene, new()
        {
            Engine.SceneManager.ChangeScene<T>();
        }
    }
} 