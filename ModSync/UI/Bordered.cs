using UnityEngine;

namespace ModSync.UI;
public class Bordered
{
    protected static void DrawBorder(Rect rect, int thickness, Color color)
    {
        var borderTexture = Utility.GetTexture(color);
        GUI.DrawTexture(new(rect.xMin, rect.yMin, rect.width, thickness), borderTexture); // Top
        GUI.DrawTexture(new(rect.xMin, rect.yMax - thickness, rect.width, thickness), borderTexture); // Bottom
        GUI.DrawTexture(new(rect.xMin, rect.yMin, thickness, rect.height), borderTexture); // Left
        GUI.DrawTexture(new(rect.xMax - thickness, rect.yMin, thickness, rect.height), borderTexture); // Right
    }
}
