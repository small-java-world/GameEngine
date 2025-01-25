using Xunit;
using Microsoft.Extensions.Logging;
using MyEngine.Core;
using MyEngine.Abstractions;
using MyEngine.Tests.Mocks;

namespace MyEngine.Tests.Core
{
    public class SceneTransitionTests
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly MockInputProvider _input;
        private readonly MockGraphics _graphics;
        private readonly MockAudioPlayer _audio;
        private readonly Engine _engine;

        public SceneTransitionTests()
        {
            _loggerFactory = LoggerFactory.Create(builder => 
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddConsole();
            });

            _input = new MockInputProvider();
            _graphics = new MockGraphics();
            _audio = new MockAudioPlayer();

            _engine = new Engine(
                _input,
                _graphics,
                _audio,
                _loggerFactory
            );
        }

        [Fact]
        public void TitleToOfficeScene_WhenSpaceKeyPressed_TransitionShouldOccur()
        {
            // 1) タイトルシーンを開始
            _engine.Initialize();
            _engine.SceneManager.ChangeScene<TitleScene>();
            Assert.IsType<TitleScene>(_engine.SceneManager.CurrentScene);

            // 2) 10フレーム目でスペースキーを押す
            _input.SetKeyState(KeyCode.Space, true);

            // 3) エンジンを20フレーム実行
            _engine.Run(maxFrame: 20);

            // 4) オフィスシーンに遷移していることを確認
            Assert.IsType<OfficeScene>(_engine.SceneManager.CurrentScene);

            // 5) 描画とオーディオの呼び出しを確認
            Assert.Contains(_graphics.DrawCalls, call => call.StartsWith("DrawTexture(title_bg"));
            Assert.Contains(_graphics.DrawCalls, call => call.StartsWith("DrawTexture(press_space"));
            Assert.Contains(_audio.AudioCalls, call => call.StartsWith("PlayBgm(office_bgm"));
        }
    }
} 