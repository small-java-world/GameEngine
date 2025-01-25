using System.Collections.Generic;
using MyEngine.Abstractions;

namespace MyEngine.Tests.Mocks
{
    public class MockAudioPlayer : IAudioPlayer
    {
        public List<string> AudioCalls { get; } = new();
        public float BgmVolume { get; set; } = 1.0f;
        public float SeVolume { get; set; } = 1.0f;

        public void PlayBgm(object bgm, bool loop = true)
        {
            AudioCalls.Add($"PlayBgm({bgm}, loop={loop})");
        }

        public void StopBgm()
        {
            AudioCalls.Add("StopBgm");
        }

        public void PlaySe(object se)
        {
            AudioCalls.Add($"PlaySe({se})");
        }

        public void StopAll()
        {
            AudioCalls.Add("StopAll");
        }

        public void ClearAudioCalls()
        {
            AudioCalls.Clear();
        }
    }
}
