using UnityEngine;

using EFT.UI;

namespace ModSync.UI
{
    public static class Utility
    {
        public static Texture2D GetTexture(Color color)
        {
            Texture2D texture = new(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
