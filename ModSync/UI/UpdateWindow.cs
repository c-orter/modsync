using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ModSync.UI
{
    public class UpdateWindow(string title, string message, string continueText = "CONTINUE", string cancelText = "CANCEL")
    {
        private readonly UpdateBox alertBox = new(title, message, continueText, cancelText);
        public bool Active { get; private set; }

        public void Show() => Active = true;

        public void Hide() => Active = false;

        public void Draw(string updatesText, Action onAccept, Action onDecline)
        {
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            var windowWidth = 800f;
            var windowHeight = 640f;

            GUILayout.BeginArea(new Rect((screenWidth - windowWidth) / 2f, (screenHeight - windowHeight) / 2f, windowWidth, windowHeight));
            alertBox.Draw(new(800f, 640f), updatesText, onAccept, onDecline);
            GUILayout.EndArea();
        }
    }

    internal class UpdateBox(string title, string message, string continueText = "CONTINUE", string cancelText = "CANCEL") : Bordered
    {
        private readonly UpdateButton acceptButton = new(continueText, Colors.Primary, Colors.PrimaryLight, Colors.Grey, Colors.PrimaryDark);
        private readonly UpdateButton declineButton = new(cancelText, Colors.Secondary, Colors.SecondaryLight, Colors.Grey, Colors.SecondaryDark);

        private readonly int borderThickness = 2;
        private Vector2 scrollPosition = Vector2.zero;

        public void Draw(Vector2 size, string updatesText, Action onAccept, Action onDecline)
        {
            var borderRect = GUILayoutUtility.GetRect(size.x, size.y);
            DrawBorder(borderRect, borderThickness, Colors.Grey);

            Rect alertRect =
                new(
                    borderRect.x + borderThickness,
                    borderRect.y + borderThickness,
                    borderRect.width - 2 * borderThickness,
                    borderRect.height - 2 * borderThickness
                );

            GUI.DrawTexture(alertRect, Utility.GetTexture(Colors.Dark.SetAlpha(0.5f)), ScaleMode.StretchToFill, true, 0);

            Rect infoRect = new(alertRect.x, alertRect.y, alertRect.width, 96f);
            Rect scrollRect = new(alertRect.x, alertRect.y + 96f, alertRect.width, alertRect.height - 96f - 48f);
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

            GUIStyle scrollStyle =
                new()
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 16,
                    normal = { textColor = Colors.White }
                };

            Rect titleRect = new(infoRect.x, infoRect.y, infoRect.width, infoRect.height / 2);
            GUI.Label(titleRect, title, titleStyle);

            Rect messageRect = new(infoRect.x, infoRect.y + infoRect.height / 2f, infoRect.width, infoRect.height / 2);
            GUI.Label(messageRect, message, messageStyle);

            GUIStyle scrollbarStyle =
                new(GUI.skin.verticalScrollbar)
                {
                    normal = { background = Utility.GetTexture(Colors.Grey.SetAlpha(0.2f)) },
                    active = { background = Utility.GetTexture(Colors.Grey.SetAlpha(0.2f)) },
                    hover = { background = Utility.GetTexture(Colors.Grey.SetAlpha(0.2f)) },
                    focused = { background = Utility.GetTexture(Colors.Grey.SetAlpha(0.2f)) },
                };
            GUIStyle scrollbarThumbStyle =
                new(GUI.skin.verticalScrollbarThumb)
                {
                    normal = { background = Utility.GetTexture(Colors.Primary.SetAlpha(0.66f)) },
                    active = { background = Utility.GetTexture(Colors.Primary.SetAlpha(0.5f)) },
                    hover = { background = Utility.GetTexture(Colors.Primary.SetAlpha(0.66f)) },
                    focused = { background = Utility.GetTexture(Colors.Primary.SetAlpha(0.5f)) },
                };

            var scrollHeight = scrollStyle.CalcHeight(new GUIContent(updatesText), alertRect.width - 40f);
            GUI.DrawTexture(scrollRect, Utility.GetTexture(Color.black.SetAlpha(0.5f)), ScaleMode.StretchToFill, true, 0);

            var oldSkin = GUI.skin;
            GUI.skin.verticalScrollbarThumb = scrollbarThumbStyle;
            GUI.skin.label.wordWrap = true;
            scrollPosition = GUI.BeginScrollView(
                scrollRect,
                scrollPosition,
                new(0f, 0f, alertRect.width, scrollHeight + 32f),
                false,
                true,
                GUIStyle.none,
                scrollbarStyle
            );
            GUI.skin = oldSkin;
            GUI.Label(new(16f, 16f, alertRect.width - 56f, scrollHeight), updatesText, scrollStyle);
            GUI.EndScrollView();

            if (onDecline != null && declineButton.Draw(new(actionsRect.x, actionsRect.y, actionsRect.width / 2, actionsRect.height)))
                onDecline();
            if (
                onAccept != null
                && acceptButton.Draw(
                    new(
                        actionsRect.x + (onDecline == null ? 0 : actionsRect.width / 2),
                        actionsRect.y,
                        (onDecline == null ? actionsRect.width : actionsRect.width / 2),
                        actionsRect.height
                    )
                )
            )
                onAccept();
        }
    }

    internal class UpdateButton(string text, Color normalColor, Color hoverColor, Color activeColor, Color borderColor) : Bordered
    {
        private const int borderThickness = 2;
        private bool active;

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
