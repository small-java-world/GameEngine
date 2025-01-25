using Microsoft.Extensions.Logging;
using MyEngine.Abstractions;

namespace MyEngine.Core
{
    public class Engine
    {
        private readonly ILogger<Engine> _logger;
        public IInputProvider InputProvider { get; }
        public IGraphics Graphics { get; }
        public IAudioPlayer AudioPlayer { get; }
        public SceneManager SceneManager { get; }

        private bool _isRunning;
        private DateTime _prevTime;

        public Engine(
            IInputProvider input,
            IGraphics graphics,
            IAudioPlayer audio,
            ILoggerFactory loggerFactory)
        {
            InputProvider = input;
            Graphics = graphics;
            AudioPlayer = audio;
            _logger = loggerFactory.CreateLogger<Engine>();

            SceneManager = new SceneManager(this, loggerFactory.CreateLogger<SceneManager>());
        }

        public void Initialize()
        {
            _logger.LogInformation("Engine initializing...");
            _prevTime = DateTime.Now;
        }

        public void Run(int maxFrame = 0)
        {
            _logger.LogInformation($"Engine run started. maxFrame={maxFrame}");
            _isRunning = true;
            int frameCount = 0;

            while (_isRunning)
            {
                if (maxFrame > 0 && frameCount++ >= maxFrame)
                {
                    _logger.LogInformation("Max frame count reached, stopping engine");
                    break;
                }

                var now = DateTime.Now;
                float deltaTime = (float)(now - _prevTime).TotalSeconds;
                _prevTime = now;

                // 入力更新
                InputProvider.Update();

                // シーン更新
                SceneManager.Update(deltaTime);

                // 画面描画
                Render();
            }

            Shutdown();
        }

        public void Exit()
        {
            _logger.LogInformation("Engine exit requested");
            _isRunning = false;
        }

        private void Render()
        {
            Graphics.Clear(System.Drawing.Color.Black);
            SceneManager.Draw();
            Graphics.Present();
        }

        private void Shutdown()
        {
            _logger.LogInformation("Engine shutting down...");
            AudioPlayer.StopAll();
        }
    }
} 