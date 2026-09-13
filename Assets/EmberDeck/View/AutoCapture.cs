using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// Screenshots the running game from inside the player, driven by command-line flags.
    ///
    /// Capturing the game's own framebuffer is the only reliable way to see this UI: the
    /// canvas is ScreenSpaceOverlay, which bypasses cameras entirely and therefore cannot be
    /// rendered into a RenderTexture, and an OS-level screen grab needs screen-recording
    /// permission that a headless run will not have.
    ///
    /// Entirely inert without -emberdeck-capture, so it costs a shipped build nothing.
    /// </summary>
    public static class AutoCapture
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string directory = ReadArg("-emberdeck-capture");
            if (string.IsNullOrEmpty(directory)) return;

            // Unity halts the whole loop — Update and coroutines included — when the window
            // is not focused and runInBackground is off. A capture run launched from a shell
            // never gets focus, so without this the player loads the scene and then simply
            // stops, with nothing in the log to say why.
            Application.runInBackground = true;

            var host = new GameObject("[AutoCapture]");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>().Directory = directory;
        }

        static string ReadArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        sealed class Runner : MonoBehaviour
        {
            public string Directory;

            IEnumerator Start()
            {
                Directory ??= ".";
                System.IO.Directory.CreateDirectory(Directory);

                // Let layout settle. uGUI positions itself over a frame or two, so capturing
                // immediately shows a half-built board and proves nothing.
                yield return new WaitForSeconds(1.5f);
                yield return Capture("01-combat-start.png");

                // Drive one full turn cycle through the real button, so the shot exercises
                // enemy resolution and the redraw path rather than just the initial render.
                var endTurn = FindButton("EndTurn");
                if (endTurn != null)
                {
                    // Never let a fault in the game skip the quit below: an unattended
                    // capture run that hangs costs far more than a missing screenshot.
                    try { endTurn.onClick.Invoke(); }
                    catch (System.Exception e) { Debug.LogError($"[AutoCapture] end turn threw: {e}"); }
                    yield return new WaitForSeconds(1.0f);
                    yield return Capture("02-after-end-turn.png");
                }
                else
                {
                    Debug.LogError("[AutoCapture] End Turn button not found.");
                }

                yield return new WaitForSeconds(0.3f);
                Application.Quit();
            }

            IEnumerator Capture(string fileName)
            {
                yield return new WaitForEndOfFrame();

                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                var png = texture.EncodeToPNG();
                Destroy(texture);

                var path = Path.Combine(Directory, fileName);
                File.WriteAllBytes(path, png);
                Debug.Log($"[AutoCapture] wrote {path} ({png.Length} bytes, {Screen.width}x{Screen.height})");
            }

            static Button FindButton(string name)
            {
                foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
                    if (button.name == name) return button;
                return null;
            }
        }
    }
}
