using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MyEngine.Resource
{
    public class ResourceManager
    {
        private readonly ILogger<ResourceManager> _logger;
        private readonly Dictionary<string, object> _textureCache = new();
        private readonly Dictionary<string, object> _soundCache = new();

        public ResourceManager(ILogger<ResourceManager> logger)
        {
            _logger = logger;
        }

        public T LoadTexture<T>(string path) where T : class
        {
            if (_textureCache.TryGetValue(path, out var cached))
            {
                _logger.LogDebug($"テクスチャをキャッシュから取得: {path}");
                return (T)cached;
            }

            _logger.LogInformation($"テクスチャを新規読み込み: {path}");
            var texture = LoadTextureFromFile<T>(path);
            _textureCache[path] = texture;
            return texture;
        }

        public T LoadSound<T>(string path) where T : class
        {
            if (_soundCache.TryGetValue(path, out var cached))
            {
                _logger.LogDebug($"サウンドをキャッシュから取得: {path}");
                return (T)cached;
            }

            _logger.LogInformation($"サウンドを新規読み込み: {path}");
            var sound = LoadSoundFromFile<T>(path);
            _soundCache[path] = sound;
            return sound;
        }

        public void UnloadTexture(string path)
        {
            if (_textureCache.Remove(path))
            {
                _logger.LogInformation($"テクスチャをアンロード: {path}");
            }
        }

        public void UnloadSound(string path)
        {
            if (_soundCache.Remove(path))
            {
                _logger.LogInformation($"サウンドをアンロード: {path}");
            }
        }

        public void ClearAll()
        {
            _logger.LogInformation("全リソースをクリア");
            _textureCache.Clear();
            _soundCache.Clear();
        }

        protected virtual T LoadTextureFromFile<T>(string path) where T : class
        {
            // 実際の読み込み処理は継承先で
            throw new NotImplementedException();
        }

        protected virtual T LoadSoundFromFile<T>(string path) where T : class
        {
            // 実際の読み込み処理は継承先で
            throw new NotImplementedException();
        }
    }
}
