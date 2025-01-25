using Microsoft.Extensions.Logging;
using MyEngine.Core;
using MyEngine.Abstractions;

namespace MyEngine.Tests.Core
{
    public class TitleScene : Scene
    {
        public override void OnStart()
        {
            Logger.LogInformation("TitleScene started");
        }

        public override void OnUpdate(float deltaTime)
        {
            if (Engine.InputProvider.IsKeyDown(KeyCode.Space))
            {
                Logger.LogInformation("Space key pressed, changing to OfficeScene");
                ChangeScene<OfficeScene>();
            }
        }

        public override void OnDraw()
        {
            // タイトル画面の描画をシミュレート
            Engine.Graphics.DrawTexture("title_bg", 0, 0);
            Engine.Graphics.DrawTexture("press_space", 320, 240);
        }

        public override void OnStop()
        {
            Logger.LogInformation("TitleScene stopped");
        }
    }

    public class OfficeScene : Scene
    {
        public override void OnStart()
        {
            Logger.LogInformation("OfficeScene started");
            Engine.AudioPlayer.PlayBgm("office_bgm");
        }

        public override void OnStop()
        {
            Logger.LogInformation("OfficeScene stopped");
            Engine.AudioPlayer.StopBgm();
        }
    }
} 