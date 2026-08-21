using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Editor
{
    /// <summary>
    /// Adds a runtime Time.timeScale selector alongside Unity's Play controls.
    /// </summary>
    internal static class PlayModeTimeScaleToolbar
    {
        private const string PlayModeControlsPath = "Play Mode Controls";
        private const string GameSceneButtonName = "FitTheShape";
        private const string BlackHoleSceneButtonName = "BlockHole";
        private const string StickerdomSceneButtonName = "Stickerdom";
        private const string BucaSceneButtonName = "Buca";
        private const string TimeScaleMenuName = "game-time-scale-menu";
        private const string FitTheShapeScenePath = "Assets/Case1_FitTheShape/Scenes/FitTheShape.unity";
        private const string BlackHoleScenePath = "Assets/Case2_BlockHole/Scenes/BlockHole.unity";
        private const string StickerdomScenePath = "Assets/Case3_Stickerdom/Scenes/Stickerdom.unity";
        private const string BucaScenePath = "Assets/Case4_Buca/Scenes/Buca.unity";

        private static readonly float[] TimeScaleOptions =
        {
            0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f,
            1f, 2f, 3f, 4f, 5f
        };

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            var playModeButtonsType = Type.GetType(
                "UnityEditor.Toolbars.PlayModeButtons, UnityEditor.EditorToolbarModule");
            var buttonsCreatedEvent = playModeButtonsType?.GetEvent(
                "onPlayModeButtonsCreated",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            var addHandler = buttonsCreatedEvent?.GetAddMethod(true);
            addHandler?.Invoke(null, new object[] { (Action<VisualElement>)AddToolbarControls });

            EditorApplication.playModeStateChanged += _ => RefreshPlayModeControls();
            EditorApplication.delayCall += RefreshPlayModeControls;
        }

        private static void AddToolbarControls(VisualElement playModeButtons)
        {
            var gameSceneButton = playModeButtons.Q<Button>(GameSceneButtonName);
            if (gameSceneButton == null)
            {
                gameSceneButton = new Button(OpenGameScene)
                {
                    name = GameSceneButtonName,
                    text = "FitTheShape",
                    tooltip = "Open Assets/Case1_FitTheShape/Scenes/FitTheShape.unity"
                };

                gameSceneButton.style.marginLeft = 4f;
                playModeButtons.Insert(0, gameSceneButton);
            }

            gameSceneButton.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);

            var blackHoleSceneButton = playModeButtons.Q<Button>(BlackHoleSceneButtonName);
            if (blackHoleSceneButton == null)
            {
                blackHoleSceneButton = new Button(OpenBlackHoleScene)
                {
                    name = BlackHoleSceneButtonName,
                    text = "BlockHole",
                    tooltip = "Open Assets/Case2_BlockHole/Scenes/BlockHole.unity"
                };

                playModeButtons.Insert(playModeButtons.IndexOf(gameSceneButton) + 1, blackHoleSceneButton);
            }

            blackHoleSceneButton.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            blackHoleSceneButton.style.marginLeft = 2f;

            var stickerdomSceneButton = playModeButtons.Q<Button>(StickerdomSceneButtonName);
            if (stickerdomSceneButton == null)
            {
                stickerdomSceneButton = new Button(OpenStickerdomScene)
                {
                    name = StickerdomSceneButtonName,
                    text = "Stickerdom",
                    tooltip = "Open Assets/Case3_Stickerdom/Scenes/Stickerdom.unity"
                };

                playModeButtons.Insert(playModeButtons.IndexOf(blackHoleSceneButton) + 1, stickerdomSceneButton);
            }

            stickerdomSceneButton.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            stickerdomSceneButton.style.marginLeft = 2f;

            var bucaSceneButton = playModeButtons.Q<Button>(BucaSceneButtonName);
            if (bucaSceneButton == null)
            {
                bucaSceneButton = new Button(OpenBucaScene)
                {
                    name = BucaSceneButtonName,
                    text = "Buca",
                    tooltip = "Open Assets/Case4_Buca/Scenes/Buca.unity"
                };

                playModeButtons.Insert(playModeButtons.IndexOf(stickerdomSceneButton) + 1, bucaSceneButton);
            }

            bucaSceneButton.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            bucaSceneButton.style.marginLeft = 2f;

            var timeScaleMenu = playModeButtons.Q<Button>(TimeScaleMenuName);
            if (timeScaleMenu == null)
            {
                timeScaleMenu = new Button(ShowTimeScaleMenu)
                {
                    name = TimeScaleMenuName,
                    tooltip = "Select the runtime Time.timeScale value."
                };

                playModeButtons.Insert(playModeButtons.IndexOf(bucaSceneButton) + 1, timeScaleMenu);
            }

            timeScaleMenu.text = $"Time: {FormatScale(Time.timeScale)} ▾";
            timeScaleMenu.SetEnabled(EditorApplication.isPlaying);
            timeScaleMenu.style.marginLeft = 2f;
        }

        private static void OpenGameScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (SceneManager.GetActiveScene().path == FitTheShapeScenePath)
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(FitTheShapeScenePath, OpenSceneMode.Single);
        }

        private static void OpenBlackHoleScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (SceneManager.GetActiveScene().path == BlackHoleScenePath)
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(BlackHoleScenePath, OpenSceneMode.Single);
        }

        private static void OpenStickerdomScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (SceneManager.GetActiveScene().path == StickerdomScenePath)
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(StickerdomScenePath, OpenSceneMode.Single);
        }

        private static void OpenBucaScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (SceneManager.GetActiveScene().path == BucaScenePath)
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(BucaScenePath, OpenSceneMode.Single);
        }

        private static void ShowTimeScaleMenu()
        {
            var menu = new GenericMenu();
            foreach (var timeScale in TimeScaleOptions)
            {
                var option = timeScale;
                menu.AddItem(
                    new GUIContent(FormatScale(option)),
                    Mathf.Approximately(Time.timeScale, option),
                    () => SetTimeScale(option));
            }

            menu.ShowAsContext();
        }

        private static void RefreshPlayModeControls()
        {
            MainToolbar.Refresh(PlayModeControlsPath);
        }

        private static void SetTimeScale(float timeScale)
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            Time.timeScale = timeScale;
            RefreshPlayModeControls();
        }

        private static string FormatScale(float timeScale)
        {
            return timeScale.ToString("0.0#") + "x";
        }
    }
}
