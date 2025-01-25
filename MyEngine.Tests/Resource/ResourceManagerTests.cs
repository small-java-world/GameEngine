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
            // 1回目の読み込み
            var texture1 = _resourceManager.LoadTexture<MockTexture>("test.png");
            Assert.Equal("test.png", texture1.Path);
            Assert.Single(_resourceManager.LoadCalls);
            Assert.Equal("LoadTexture(test.png)", _resourceManager.LoadCalls[0]);

            // 2回目の読み込み（キャッシュから取得されるはず）
            var texture2 = _resourceManager.LoadTexture<MockTexture>("test.png");
            Assert.Same(texture1, texture2);
            Assert.Single(_resourceManager.LoadCalls); // 追加の読み込み呼び出しがないことを確認
        }

        [Fact]
        public void LoadSound_ShouldCacheAndReuseSound()
        {
            // 1回目の読み込み
            var sound1 = _resourceManager.LoadSound<MockSound>("bgm.wav");
            Assert.Equal("bgm.wav", sound1.Path);
            Assert.Single(_resourceManager.LoadCalls);
            Assert.Equal("LoadSound(bgm.wav)", _resourceManager.LoadCalls[0]);

            // 2回目の読み込み（キャッシュから取得されるはず）
            var sound2 = _resourceManager.LoadSound<MockSound>("bgm.wav");
            Assert.Same(sound1, sound2);
            Assert.Single(_resourceManager.LoadCalls); // 追加の読み込み呼び出しがないことを確認
        }

        [Fact]
        public void UnloadAndReload_ShouldLoadAgain()
        {
            // 1回目の読み込み
            var texture1 = _resourceManager.LoadTexture<MockTexture>("test.png");
            
            // アンロード
            _resourceManager.UnloadTexture("test.png");
            
            // 2回目の読み込み（新しく読み込まれるはず）
            var texture2 = _resourceManager.LoadTexture<MockTexture>("test.png");
            
            Assert.NotSame(texture1, texture2);
            Assert.Equal(2, _resourceManager.LoadCalls.Count); // 2回読み込まれたことを確認
        }

        [Fact]
        public void ClearAll_ShouldUnloadAllResources()
        {
            // テクスチャとサウンドを読み込む
            _resourceManager.LoadTexture<MockTexture>("test1.png");
            _resourceManager.LoadTexture<MockTexture>("test2.png");
            _resourceManager.LoadSound<MockSound>("bgm1.wav");
            _resourceManager.LoadSound<MockSound>("bgm2.wav");
            Assert.Equal(4, _resourceManager.LoadCalls.Count);

            // 全てクリア
            _resourceManager.ClearAll();

            // 再度読み込むと新しく読み込まれるはず
            _resourceManager.LoadTexture<MockTexture>("test1.png");
            _resourceManager.LoadSound<MockSound>("bgm1.wav");
            Assert.Equal(6, _resourceManager.LoadCalls.Count); // 追加で2回読み込まれたことを確認
        }
    }
} 