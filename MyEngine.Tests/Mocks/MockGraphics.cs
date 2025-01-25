using System.Drawing;
using System.Collections.Generic;
using MyEngine.Abstractions;

namespace MyEngine.Tests.Mocks
{
    public class MockGraphics : IGraphics
    {
        public List<string> DrawCalls { get; } = new();

        public void Clear(Color color)
        {
            DrawCalls.Add($"Clear({color})");
        }

        public void DrawTexture(object texture, float x, float y)
        {
            DrawCalls.Add($"DrawTexture({texture}, {x}, {y})");
        }

        public void DrawTexture(object texture, float x, float y, float width, float height)
        {
            DrawCalls.Add($"DrawTexture({texture}, {x}, {y}, {width}, {height})");
        }

        public void DrawTexture(object texture, RectangleF sourceRect, RectangleF destRect)
        {
            DrawCalls.Add($"DrawTexture({texture}, src:{sourceRect}, dest:{destRect})");
        }

        public void Present()
        {
            DrawCalls.Add("Present");
        }

        public void ClearDrawCalls()
        {
            DrawCalls.Clear();
        }
    }
} 