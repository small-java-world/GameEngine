namespace MyEngine.Abstractions
{
    public interface IAudioPlayer
    {
        void PlayBgm(object bgm, bool loop = true);
        void StopBgm();
        void PlaySe(object se);
        void StopAll();
        float BgmVolume { get; set; }
        float SeVolume { get; set; }
    }
} 