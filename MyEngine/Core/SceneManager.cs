using Microsoft.Extensions.Logging;

namespace MyEngine.Core
{
    public class SceneManager
    {
        private readonly Engine _engine;
        private readonly ILogger<SceneManager> _logger;
        public Scene? CurrentScene { get; private set; }

        public SceneManager(Engine engine, ILogger<SceneManager> logger)
        {
            _engine = engine;
            _logger = logger;
        }

        public void ChangeScene<T>() where T : Scene, new()
        {
            _logger.LogInformation($"Changing scene to {typeof(T).Name}");
            
            // 旧シーンの終了
            CurrentScene?.OnStop();
            
            // 新シーン作成
            var newScene = new T();
            newScene.Initialize(_engine, _logger);
            
            // 新シーン開始
            newScene.OnStart();
            CurrentScene = newScene;
        }

        public void Update(float deltaTime)
        {
            CurrentScene?.OnUpdate(deltaTime);
        }

        public void Draw()
        {
            CurrentScene?.OnDraw();
        }
    }
} 