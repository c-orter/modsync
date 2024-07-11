using System;
using UnityEngine;

namespace ModSync.UI
{
    public class ProgressWindow(string title, string message)
    {
        private readonly InfoBox infoBox = new(title, message);
        private readonly ProgressBar progressBar = new();
        private readonly CancelButton cancelButton = new();
        public bool Active { get; private set; }

        public void Show() => Active = true;

        public void Hide() => Active = false;

        public void Draw(int progressValue, int progressMax, Action cancelAction)
        {
            if (!Active)
                return;

            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            const float windowWidth = 640f;
            const float windowHeight = 640f;

            GUILayout.BeginArea(new Rect((screenWidth - windowWidth) / 2f, (screenHeight - windowHeight) / 2f, windowWidth, windowHeight));
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            infoBox.Draw(new Vector2(480f, 240f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(64f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            progressBar.Draw(new Vector2(windowWidth, 32f), progressValue, progressMax);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(64f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (cancelButton.Draw(new Vector2(196f, 48f)))
                cancelAction();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private class ProgressBar : Bordered
        {
            private const int borderThickness = 2;

            public void Draw(Vector2 size, int currentValue, int maxValue)
            {
                var borderRect = GUILayoutUtility.GetRect(size.x, size.y);
                DrawBorder(borderRect, borderThickness, Colors.Grey);

                Rect progressRect =
                    new(
                        borderRect.x + borderThickness,
                        borderRect.y + borderThickness,
                        borderRect.width - 2 * borderThickness,
                        borderRect.height - 2 * borderThickness
                    );
                GUI.Box(progressRect, "");

                var ratio = (float)currentValue / maxValue;
                Rect fillRect = new(progressRect.x, progressRect.y, progressRect.width * ratio, progressRect.height);
                GUI.DrawTexture(fillRect, Utility.GetTexture(Colors.Primary));

                GUIStyle style = new() { alignment = TextAnchor.MiddleCenter, normal = { textColor = Colors.White } };
                GUI.Label(progressRect, $"{currentValue} / {maxValue} ({(float)currentValue / maxValue:P1})", style);
            }
        }
    }

    internal class CancelButton : Bordered
    {
        private const int borderThickness = 2;

        private bool active;

        public bool Draw(Vector2 size)
        {
            var borderRect = GUILayoutUtility.GetRect(size.x, size.y);

            Rect buttonRect =
                new(
                    borderRect.x + borderThickness,
                    borderRect.y + borderThickness,
                    borderRect.width - 2 * borderThickness,
                    borderRect.height - 2 * borderThickness
                );

            var hovered = buttonRect.Contains(Event.current.mousePosition);

            if (hovered && Event.current.type == EventType.MouseDown)
                active = true;
            if (active && Event.current.type == EventType.MouseUp)
                active = false;

            var buttonColor = active
                ? Colors.Grey
                : hovered
                    ? Colors.PrimaryLight
                    : Colors.Primary;
            var textColor = active ? Colors.Dark : Colors.White;

            DrawBorder(borderRect, borderThickness, Colors.PrimaryDark);
            GUI.DrawTexture(buttonRect, Utility.GetTexture(buttonColor), ScaleMode.StretchToFill);
            return GUI.Button(
                buttonRect,
                new GUIContent("CANCEL UPDATE"),
                new GUIStyle()
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = textColor }
                }
            );
        }
    }
}
