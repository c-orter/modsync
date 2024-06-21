using UnityEngine;

namespace ModSync.UI
{
    internal class InfoBox(string title, string message, int borderThickness = 2, bool transparent = false) : Bordered
    {
        private readonly string title = title;
        private readonly string message = message;
        private readonly int borderThickness = borderThickness;
        private readonly bool transparent = transparent;

        public void Draw(Vector2 size)
        {
            Rect borderRect = GUILayoutUtility.GetRect(size.x, size.y);
            DrawBorder(borderRect, borderThickness, Colors.Grey);

            Rect infoRect =
                new(
                    borderRect.x + borderThickness,
                    borderRect.y + borderThickness,
                    borderRect.width - 2 * borderThickness,
                    borderRect.height - 2 * borderThickness
                );

            if (!transparent)
                GUI.DrawTexture(infoRect, Utility.GetTexture(Colors.Dark.SetAlpha(0.5f)), ScaleMode.StretchToFill, true, 0);

            GUIStyle titleStyle =
                new()
                {
                    alignment = TextAnchor.LowerCenter,
                    fontSize = 28,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Colors.White }
                };

            GUIStyle messageStyle =
                new()
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Colors.White }
                };

            Rect titleRect = new(infoRect.x, infoRect.y, infoRect.width, infoRect.height / 2.75f);
            GUI.Label(titleRect, title, titleStyle);

            Rect messageRect = new(infoRect.x, infoRect.y + infoRect.height / 2.5f, infoRect.width, infoRect.height / 2);
            GUI.Label(messageRect, message, messageStyle);
        }
    }
}
