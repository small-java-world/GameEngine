using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using MyEngine.Resource;

namespace MyEngine.Tests.Mocks
{
    public class MockResourceManager : ResourceManager
    {
        public List<string> LoadCalls { get; } = new();

        public MockResourceManager(ILogger<ResourceManager> logger) : base(logger)
        {
        }

        protected override T LoadTextureFromFile<T>(string path)
        {
            LoadCalls.Add($"LoadTexture({path})");
            if (typeof(T) == typeof(MockTexture))
            {
                return (T)(object)new MockTexture(path);
            }
            throw new InvalidOperationException($"Unsupported texture type: {typeof(T)}");
        }

        protected override T LoadSoundFromFile<T>(string path)
        {
            LoadCalls.Add($"LoadSound({path})");
            if (typeof(T) == typeof(MockSound))
            {
                return (T)(object)new MockSound(path);
            }
            throw new InvalidOperationException($"Unsupported sound type: {typeof(T)}");
        }

        public void ClearLoadCalls()
        {
            LoadCalls.Clear();
        }
    }

    public class MockTexture
    {
        public string Path { get; }
        public MockTexture(string path)
        {
            Path = path;
        }
        public override string ToString() => $"MockTexture({Path})";
    }

    public class MockSound
    {
        public string Path { get; }
        public MockSound(string path)
        {
            Path = path;
        }
        public override string ToString() => $"MockSound({Path})";
    }
}
