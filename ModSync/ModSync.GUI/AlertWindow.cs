using System;
using Comfort.Common;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace ModSync.UI
{
    public class AlertWindow(string title, string message)
    {
        private readonly AlertBox alertBox = new(title, message);

        public void Draw(Action onAccept, Action onDecline)
        {
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            var windowWidth = 640f;
            var windowHeight = 640f;

            GUILayout.BeginArea(new Rect((screenWidth - windowWidth) / 2f, (screenHeight - windowHeight) / 2f, windowWidth, windowHeight));
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            alertBox.Draw(new(640f, 240f), onAccept, onDecline);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }

    internal class AlertBox(string title, string message) : Bordered
    {
        private readonly AlertButton declineButton = new("CANCEL", Colors.Blue, Colors.BlueLighten, Colors.Grey, Colors.BlueDarken);
        private readonly AlertButton acceptButton = new("CONTINUE", Colors.Red, Colors.RedLighten, Colors.Grey, Colors.RedDarken);

        private readonly int borderThickness = 2;

        public void Draw(Vector2 size, Action onAccept, Action onDecline)
        {
            Rect borderRect = GUILayoutUtility.GetRect(size.x, size.y);
            DrawBorder(borderRect, borderThickness, Colors.Grey);

            Rect alertRect =
                new(
                    borderRect.x + borderThickness,
                    borderRect.y + borderThickness,
                    borderRect.width - 2 * borderThickness,
                    borderRect.height - 2 * borderThickness
                );
            
            GUI.DrawTexture(alertRect, Utility.GetTexture(Colors.Dark.SetAlpha(0.5f)), ScaleMode.StretchToFill, true, 0);

            Rect infoRect = new(alertRect.x, alertRect.y, alertRect.width, alertRect.height - 48f);
            Rect actionsRect = new(alertRect.x, alertRect.y + alertRect.height - 48f, alertRect.width, 48f);

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

            Rect titleRect = new(infoRect.x, infoRect.y, infoRect.width, infoRect.height / 2.5f);
            GUI.Label(titleRect, title, titleStyle);

            Rect messageRect = new(infoRect.x, infoRect.y + infoRect.height / 2.5f, infoRect.width, infoRect.height / 2);
            GUI.Label(messageRect, message, messageStyle);

            if (declineButton.Draw(new(actionsRect.x, actionsRect.y, actionsRect.width / 2, actionsRect.height)))
                onDecline();
            if (acceptButton.Draw(new(actionsRect.x + actionsRect.width / 2, actionsRect.y, actionsRect.width / 2, actionsRect.height)))
                onAccept();
        }
    }

    internal class AlertButton(string text, Color normalColor, Color hoverColor, Color activeColor, Color borderColor) : Bordered
    {
        private readonly string text = text;
        private readonly int borderThickness = 2;
        private bool active = false;

        public bool Draw(Rect borderRect)
        {
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
                ? activeColor
                : hovered
                    ? hoverColor
                    : normalColor;
            var textColor = active ? Colors.Dark : Colors.White;

            DrawBorder(borderRect, borderThickness, borderColor);
            GUI.DrawTexture(buttonRect, Utility.GetTexture(buttonColor), ScaleMode.StretchToFill);

            return GUI.Button(
                buttonRect,
                new GUIContent(text),
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
