using System;
using System.Collections.Generic;
using System.Reflection;
using Diz.Utils;
using EFT.InputSystem;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace ModSync
{
    internal static class UIUtils
    {
        public static GClass3085 ShowMismatchedModScreen(
            this PreloaderUI preloaderUI,
            string header,
            string message,
            string acceptMessage,
            string cancelMessage,
            float waitingTime,
            Action acceptCallback,
            Action endTimeCallback
        )
        {
            Traverse preloaderUiTraverse = Traverse.Create(preloaderUI);

            PreloaderUI.Class2561 messageHandler =
                new()
                {
                    preloaderUI_0 = preloaderUI,
                    acceptCallback = acceptCallback,
                    endTimeCallback = endTimeCallback
                };

            if (!AsyncWorker.CheckIsMainThread())
            {
                return new GClass3085();
            }

            ErrorScreen errorScreenTemplate = preloaderUiTraverse.Field("_criticalErrorScreenTemplate").GetValue<ErrorScreen>();
            EmptyInputNode errorScreenContainer = preloaderUiTraverse.Field("_criticalErrorScreenContainer").GetValue<EmptyInputNode>();

            messageHandler.errorScreen = UnityEngine.Object.Instantiate(errorScreenTemplate, errorScreenContainer.transform, false);
            errorScreenContainer.AddChildNode(messageHandler.errorScreen);
            return messageHandler.errorScreen.ShowMismatchedModScreen(
                header,
                message,
                acceptMessage,
                cancelMessage,
                new Action(messageHandler.method_1),
                waitingTime,
                new Action(messageHandler.method_2)
            );
        }

        public static GClass3087 ShowMismatchedModScreen(
            this ErrorScreen errorScreen,
            string title,
            string message,
            string acceptMessage,
            string cancelMessage,
            Action closeManuallyCallback = null,
            float waitingTime = 0f,
            Action timeOutCallback = null,
            ErrorScreen.EButtonType buttonType = ErrorScreen.EButtonType.OkButton,
            bool removeHtml = true
        )
        {
            Traverse errorScreenTraverse = Traverse.Create(errorScreen);

            ErrorScreen.Class2352 errorScreenHandler = new() { errorScreen_0 = errorScreen };
            if (!MonoBehaviourSingleton<PreloaderUI>.Instance.CanShowErrorScreen)
            {
                return new GClass3087();
            }
            if (removeHtml)
            {
                message = ErrorScreen.smethod_0(message);
            }
            ItemUiContext.Instance.CloseAllWindows();

            errorScreenTraverse.Field("action_1").SetValue(closeManuallyCallback);
            MethodBase baseShow = typeof(ErrorScreen).BaseType.GetMethod("Show");

            errorScreenHandler.context = (GClass3087)baseShow.Invoke(errorScreen, [closeManuallyCallback]);
            errorScreenHandler.context.OnAccept += errorScreen.method_3;
            errorScreenHandler.context.OnDecline += errorScreen.method_4;
            errorScreenHandler.context.OnCloseSilent += errorScreen.method_4;

            var errorScreenContextTraverse = Traverse.Create(errorScreenHandler.context);

            GClass767 ui = Traverse.Create(errorScreen).Field("UI").GetValue<GClass767>();

            ui.AddDisposable(new Action(errorScreenHandler.method_0));

            string text = "IGNORE UPDATES";

            DefaultUIButton exitButton = errorScreenTraverse.Field("_exitButton").GetValue<DefaultUIButton>();

            exitButton.SetHeaderText(text, exitButton.HeaderSize);
            errorScreen.RectTransform.anchoredPosition = Vector2.zero;

            string string_1 = message.SubstringIfNecessary(500);
            errorScreenTraverse.Field("string_1").SetValue(string_1);

            TextMeshProUGUI errorDescription = Traverse.Create(errorScreen).Field("_errorDescription").GetValue<TextMeshProUGUI>();
            errorDescription.text = string_1;

            Coroutine coroutine_0 = errorScreenTraverse.Field("coroutine_0").GetValue<Coroutine>();
            if (coroutine_0 != null)
            {
                errorScreen.StopCoroutine(coroutine_0);
            }
            if (waitingTime > 0f)
            {
                errorScreenTraverse
                    .Field("coroutine_0")
                    .SetValue(
                        errorScreen.StartCoroutine(
                            errorScreen.UpdateMismatchedButton(
                                GClass1296.Now.AddSeconds((double)waitingTime),
                                title,
                                acceptMessage,
                                cancelMessage,
                                timeOutCallback
                            )
                        )
                    );
            }
            return errorScreenHandler.context;
        }

        public static IEnumerator<object> UpdateMismatchedButton(
            this ErrorScreen errorScreen,
            DateTime endTime,
            string title,
            string acceptMessage,
            string cancelMessage,
            Action timeOutCallback
        )
        {
            Traverse errorScreenTraverse = Traverse.Create(errorScreen);
            var errorDescription = errorScreenTraverse.Field("_errorDescription").GetValue<TextMeshProUGUI>();
            var string_1 = errorScreenTraverse.Field("string_1").GetValue<string>();

            while (GClass1296.Now < endTime)
            {
                errorDescription.text = string.Format(
                    $"{title}\n\n{string_1}\n\n{{1}}",
                    Math.Max(0, (int)(endTime - GClass1296.Now).TotalSeconds),
                    cancelMessage
                );
                yield return null;
            }

            errorDescription.text = string.Format($"{title}\n\n{{0}}", acceptMessage);
            DefaultUIButton exitButton = errorScreenTraverse.Field("_exitButton").GetValue<DefaultUIButton>();
            exitButton.SetHeaderText("START UPDATE", exitButton.HeaderSize);
            errorScreenTraverse.Field("CloseAction").SetValue(timeOutCallback);
        }

        public static GClass3085 ShowProgressScreen(
            this PreloaderUI preloaderUI,
            string message,
            int totalDownloads,
            Func<int> getDownloadedCount,
            Action acceptCallback,
            Action endTimeCallback
        )
        {
            Traverse preloaderUiTraverse = Traverse.Create(preloaderUI);

            PreloaderUI.Class2561 messageHandler =
                new()
                {
                    preloaderUI_0 = preloaderUI,
                    acceptCallback = acceptCallback,
                    endTimeCallback = endTimeCallback
                };

            if (!AsyncWorker.CheckIsMainThread())
            {
                return new GClass3085();
            }

            ErrorScreen errorScreenTemplate = preloaderUiTraverse.Field("_criticalErrorScreenTemplate").GetValue<ErrorScreen>();
            EmptyInputNode errorScreenContainer = preloaderUiTraverse.Field("_criticalErrorScreenContainer").GetValue<EmptyInputNode>();

            messageHandler.errorScreen = UnityEngine.Object.Instantiate(errorScreenTemplate, errorScreenContainer.transform, false);
            errorScreenContainer.AddChildNode(messageHandler.errorScreen);
            return messageHandler.errorScreen.ShowProgressScreen(
                message,
                totalDownloads,
                getDownloadedCount,
                new Action(messageHandler.method_1),
                new Action(messageHandler.method_2)
            );
        }

        public static GClass3087 ShowProgressScreen(
            this ErrorScreen errorScreen,
            string message,
            int totalDownloads,
            Func<int> getDownloadedCount,
            Action closeManuallyCallback = null,
            Action timeOutCallback = null
        )
        {
            Traverse errorScreenTraverse = Traverse.Create(errorScreen);

            ErrorScreen.Class2352 errorScreenHandler = new() { errorScreen_0 = errorScreen };
            if (!MonoBehaviourSingleton<PreloaderUI>.Instance.CanShowErrorScreen)
            {
                return new GClass3087();
            }
            ItemUiContext.Instance.CloseAllWindows();

            Action action_1 = timeOutCallback ?? closeManuallyCallback;
            errorScreenTraverse.Field("action_1").SetValue(action_1);
            MethodBase baseShow = typeof(ErrorScreen).BaseType.GetMethod("Show");

            errorScreenHandler.context = (GClass3087)baseShow.Invoke(errorScreen, [closeManuallyCallback]);
            errorScreenHandler.context.OnAccept += errorScreen.method_3;
            errorScreenHandler.context.OnDecline += errorScreen.method_4;
            errorScreenHandler.context.OnCloseSilent += errorScreen.method_4;

            GClass767 ui = Traverse.Create(errorScreen).Field("UI").GetValue<GClass767>();

            ui.AddDisposable(new Action(errorScreenHandler.method_0));
            string text = "CANCEL UPDATE";

            DefaultUIButton exitButton = errorScreenTraverse.Field("_exitButton").GetValue<DefaultUIButton>();

            exitButton.SetHeaderText(text, exitButton.HeaderSize);
            errorScreen.RectTransform.anchoredPosition = Vector2.zero;

            string string_1 = message.SubstringIfNecessary(500);
            errorScreenTraverse.Field("string_1").SetValue(string_1);

            TextMeshProUGUI errorDescription = Traverse.Create(errorScreen).Field("_errorDescription").GetValue<TextMeshProUGUI>();
            errorDescription.text = string_1;

            Coroutine coroutine_0 = errorScreenTraverse.Field("coroutine_0").GetValue<Coroutine>();
            if (coroutine_0 != null)
            {
                errorScreen.StopCoroutine(coroutine_0);
            }
            errorScreenTraverse
                .Field("coroutine_0")
                .SetValue(errorScreen.StartCoroutine(errorScreen.UpdateProgressText(totalDownloads, getDownloadedCount, timeOutCallback)));
            return errorScreenHandler.context;
        }

        public static IEnumerator<object> UpdateProgressText(
            this ErrorScreen errorScreen,
            int totalDownloads,
            Func<int> getDownloadedCount,
            Action timeOutCallback
        )
        {
            Traverse errorScreenTraverse = Traverse.Create(errorScreen);

            var errorDescription = errorScreenTraverse.Field("_errorDescription").GetValue<TextMeshProUGUI>();

            var downloaded = 0;
            while (downloaded < totalDownloads)
            {
                downloaded = getDownloadedCount();
                errorDescription.text = string.Format(
                    "{0}\n\n{2}/{3} ({1:P1})\n\nGame must be restarted after completion.",
                    errorScreenTraverse.Field("string_1").GetValue(),
                    (float)downloaded / totalDownloads,
                    downloaded,
                    totalDownloads
                );
                yield return null;
            }

            errorDescription.text = $"Downloaded client mods.\n\n{totalDownloads}/{totalDownloads} ({1:P1})\n\nGame must be restarted.";
            DefaultUIButton exitButton = errorScreenTraverse.Field("_exitButton").GetValue<DefaultUIButton>();
            exitButton.SetHeaderText("EXIT GAME", exitButton.HeaderSize);
            errorScreenTraverse.Field("CloseAction").SetValue(timeOutCallback);
        }
    }
}
