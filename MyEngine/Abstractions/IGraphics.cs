using System.Drawing;

namespace MyEngine.Abstractions
{
    public interface IGraphics
    {
        void Clear(Color color);
        void DrawTexture(object texture, float x, float y);
        void DrawTexture(object texture, float x, float y, float width, float height);
        void DrawTexture(object texture, RectangleF sourceRect, RectangleF destRect);
        void Present();
    }
} 