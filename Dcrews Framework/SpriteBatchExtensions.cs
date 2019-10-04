using System;

namespace Microsoft.Xna.Framework.Graphics
{
    public enum RectStyle { Inline = 0, Centered = 1, Outline = 2 }

    public static class SpriteBatchExtensions
    {
        public static Texture2D Pixel { get; private set; }

        public static readonly Vector2 PixelOrigin = new Vector2(.5f);

        static readonly Vector2[] _lineOrigin = { new Vector2(0, 0), new Vector2(0, .5f), new Vector2(0, 1) };

        public static void Initialize(GraphicsDevice graphicsDevice)
        {
            Pixel = new Texture2D(graphicsDevice, 1, 1);
            Pixel.SetData(new Color[] { Color.White });
        }

        public static void DrawPixel(this SpriteBatch spriteBatch, Vector2 position, Color color, float rotation = 0, float scale = 1, float layerDepth = 0) => spriteBatch.Draw(Pixel, position, null, color, rotation, PixelOrigin, scale, 0, layerDepth);

        public static void DrawRectangleOutline(this SpriteBatch spriteBatch, Rectangle destinationRectangle, Color color, float thickness = 1, float layerDepth = 0)
        {
            DrawLine(spriteBatch, (destinationRectangle.Location.ToVector2(), new Vector2(destinationRectangle.Right, destinationRectangle.Top)), color, thickness, RectStyle.Outline, layerDepth);
            DrawLine(spriteBatch, (new Vector2(destinationRectangle.Right, destinationRectangle.Top), new Vector2(destinationRectangle.Right, destinationRectangle.Bottom)), color, thickness, RectStyle.Outline, layerDepth);
            DrawLine(spriteBatch, (new Vector2(destinationRectangle.Right, destinationRectangle.Bottom), new Vector2(destinationRectangle.Left, destinationRectangle.Bottom)), color, thickness, RectStyle.Outline, layerDepth);
            DrawLine(spriteBatch, (new Vector2(destinationRectangle.Left, destinationRectangle.Bottom), destinationRectangle.Location.ToVector2()), color, thickness, RectStyle.Outline, layerDepth);
        }

        public static void DrawRectangleInline(this SpriteBatch spriteBatch, Rectangle destinationRectangle, Color color, float thickness = 1, float layerDepth = 0)
        {
            DrawLine(spriteBatch, (destinationRectangle.Location.ToVector2(), new Vector2(destinationRectangle.Right, destinationRectangle.Top)), color, thickness, RectStyle.Inline, layerDepth);
            DrawLine(spriteBatch, (new Vector2(destinationRectangle.Right, destinationRectangle.Top), new Vector2(destinationRectangle.Right, destinationRectangle.Bottom)), color, thickness, RectStyle.Inline, layerDepth);
            DrawLine(spriteBatch, (new Vector2(destinationRectangle.Right, destinationRectangle.Bottom), new Vector2(destinationRectangle.Left, destinationRectangle.Bottom)), color, thickness, RectStyle.Inline, layerDepth);
            DrawLine(spriteBatch, (new Vector2(destinationRectangle.Left, destinationRectangle.Bottom), destinationRectangle.Location.ToVector2()), color, thickness, RectStyle.Inline, layerDepth);
        }

        public static void DrawRectangle(this SpriteBatch spriteBatch, Vector2 position, Vector2 scale, Color color, float rotation = 0, float layerDepth = 0) => spriteBatch.Draw(Pixel, position, null, color, rotation, PixelOrigin, scale, 0, layerDepth);
        public static void DrawRectangle(this SpriteBatch spriteBatch, Vector2 position, float scale, Color color, float rotation = 0, float layerDepth = 0) => spriteBatch.Draw(Pixel, position, null, color, rotation, PixelOrigin, scale, 0, layerDepth);

        public static void DrawLine(this SpriteBatch spriteBatch, (Vector2 A, Vector2 B) position, Color color, float thickness = 1, RectStyle rectStyle = RectStyle.Centered, float layerDepth = 0) => spriteBatch.Draw(Pixel, position.A, null, color, (float)Math.Atan2(position.B.Y - position.A.Y, position.B.X - position.A.X), _lineOrigin[(int)rectStyle], new Vector2(Vector2.Distance(position.A, position.B), thickness), 0, layerDepth);
    }
}