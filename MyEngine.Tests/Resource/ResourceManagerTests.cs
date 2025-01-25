using Xunit;
using Microsoft.Extensions.Logging;
using MyEngine.Tests.Mocks;

namespace MyEngine.Tests.Resource
{
    public class ResourceManagerTests
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly MockResourceManager _resourceManager;

        public ResourceManagerTests()
        {
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddConsole();
            });

            _resourceManager = new MockResourceManager(
                _loggerFactory.CreateLogger<MyEngine.Resource.ResourceManager>()
            );
        }

        [Fact]
        public void LoadTexture_ShouldCacheAndReuseTexture()
        {
            var texture1 = _resourceManager.LoadTexture<MockTexture>("test.png");
            Assert.Equal("test.png", texture1.Path);
            Assert.Single(_resourceManager.LoadCalls);
            Assert.Equal("LoadTexture(test.png)", _resourceManager.LoadCalls[0]);

            var texture2 = _resourceManager.LoadTexture<MockTexture>("test.png");
            Assert.Same(texture1, texture2);
            Assert.Single(_resourceManager.LoadCalls);
        }

        [Fact]
        public void LoadSound_ShouldCacheAndReuseSound()
        {
            var sound1 = _resourceManager.LoadSound<MockSound>("bgm.wav");
            Assert.Equal("bgm.wav", sound1.Path);
            Assert.Single(_resourceManager.LoadCalls);
            Assert.Equal("LoadSound(bgm.wav)", _resourceManager.LoadCalls[0]);

            var sound2 = _resourceManager.LoadSound<MockSound>("bgm.wav");
            Assert.Same(sound1, sound2);
            Assert.Single(_resourceManager.LoadCalls);
        }

        [Fact]
        public void UnloadAndReload_ShouldLoadAgain()
        {
            var texture1 = _resourceManager.LoadTexture<MockTexture>("test.png");

            _resourceManager.UnloadTexture("test.png");

            var texture2 = _resourceManager.LoadTexture<MockTexture>("test.png");
            Assert.NotSame(texture1, texture2);
            Assert.Equal(2, _resourceManager.LoadCalls.Count);
        }

        [Fact]
        public void ClearAll_ShouldUnloadAllResources()
        {
            _resourceManager.LoadTexture<MockTexture>("test1.png");
            _resourceManager.LoadTexture<MockTexture>("test2.png");
            _resourceManager.LoadSound<MockSound>("bgm1.wav");
            _resourceManager.LoadSound<MockSound>("bgm2.wav");
            Assert.Equal(4, _resourceManager.LoadCalls.Count);

            _resourceManager.ClearAll();

            _resourceManager.LoadTexture<MockTexture>("test1.png");
            _resourceManager.LoadSound<MockSound>("bgm1.wav");
            Assert.Equal(6, _resourceManager.LoadCalls.Count);
        }
    }
}
