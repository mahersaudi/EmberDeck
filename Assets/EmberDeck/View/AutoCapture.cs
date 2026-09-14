// Development builds and the editor only. The harness drives CombatView's Debug* hooks, which are
// compiled out of release builds — so the harness must be too. Before this guard, no release
// build could compile; only development builds had ever been made, so nothing had shown it.
#if DEVELOPMENT_BUILD || UNITY_EDITOR
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
                // The real mouse still drives the event system, and the window opens wherever the
                // cursor happens to be — so a card under it lifted and showed its tooltip in shots
                // nobody asked for. The harness clicks through onClick and opens tooltips directly,
                // so it needs no pointer at all.
                foreach (var module in FindObjectsByType<UnityEngine.EventSystems.BaseInputModule>(FindObjectsSortMode.None))
                    module.enabled = false;

                yield return new WaitForSeconds(1.5f);
                yield return Capture("00-main-menu.png");

                // Settings and back, then a new run — all through the menu's own buttons.
                var settingsButton = FindButton("Settings");
                if (settingsButton != null)
                {
                    Click(settingsButton);
                    yield return new WaitForSeconds(0.6f);
                    yield return Capture("00b-settings.png");
                    Click(FindButton("SettingsBack"));
                    yield return new WaitForSeconds(0.3f);
                }

                // New Run asks for a second click when a saved run would be abandoned.
                for (int attempt = 0; attempt < 2 && FindButton("NewRun") != null; attempt++)
                {
                    Click(FindButton("NewRun"));
                    yield return new WaitForSeconds(0.4f);
                }
                yield return new WaitForSeconds(0.8f);
                yield return Capture("01-map.png");

                var pauseHost = FindFirstObjectByType<CombatView>();
                if (pauseHost != null)
                {
                    pauseHost.TogglePause();
                    yield return new WaitForSeconds(0.4f);
                    yield return Capture("01b-pause.png");
                    pauseHost.TogglePause();
                    yield return new WaitForSeconds(0.2f);
                }

                // A run opens on the map, so the harness has to walk it: take the first
                // available node, which is always a fight on the opening row.
                var map = FindFirstObjectByType<MapView>();
                if (map != null && map.gameObject.activeInHierarchy)
                {
                    var node = FindFirstNodeButton();
                    if (node != null) { Click(node); yield return new WaitForSeconds(1.2f); }
                }
                yield return Capture("02-combat-start.png");

                // Tooltips, opened through the same trigger the pointer uses: the hand card with the
                // most keywords, then an enemy's intent and statuses.
                CardView richest = null;
                int most = -1;
                foreach (var candidate in FindObjectsByType<CardView>(FindObjectsSortMode.None))
                {
                    int count = Keywords.ForCard(candidate.Card.Data).Count;
                    if (count > most) { most = count; richest = candidate; }
                }
                var cardTip = richest != null ? richest.GetComponent<TooltipTrigger>() : null;
                if (cardTip != null)
                {
                    cardTip.ShowNow();
                    yield return new WaitForSeconds(0.3f);
                    yield return Capture("02a-card-tooltip.png");
                    Tooltip.Hide();
                }

                var firstEnemy = FindFirstObjectByType<EnemyView>();
                var enemyTip = firstEnemy != null ? firstEnemy.GetComponent<TooltipTrigger>() : null;
                if (enemyTip != null)
                {
                    enemyTip.ShowNow();
                    yield return new WaitForSeconds(0.3f);
                    yield return Capture("02b-enemy-tooltip.png");
                    Tooltip.Hide();
                }

                // Drive one full turn cycle through the real button, so the shot exercises
                // enemy resolution and the redraw path rather than just the initial render.
                var endTurn = FindButton("EndTurn");
                if (endTurn != null)
                {
                    // Never let a fault in the game skip the quit below: an unattended
                    // capture run that hangs costs far more than a missing screenshot.
                    try { endTurn.onClick.Invoke(); }
                    catch (System.Exception e) { Debug.LogError($"[AutoCapture] end turn threw: {e}"); }
                    // Two frames mid-replay: the enemy turn resolves in one call and the view
                    // replays it staggered, so a shot taken after everything settles cannot show
                    // whether anything moved. These catch the numbers and shakes in flight.
                    yield return new WaitForSeconds(0.12f);
                    yield return Capture("03a-motion-0.12s.png");
                    yield return new WaitForSeconds(0.23f);
                    yield return Capture("03b-motion-0.35s.png");
                    yield return new WaitForSeconds(0.65f);
                    yield return Capture("03-after-end-turn.png");
                }
                else
                {
                    Debug.LogError("[AutoCapture] End Turn button not found.");
                }

                // Drive the fight to a finish so the reward screen can be captured. The
                // player's own End Turn button is used rather than reaching into the model,
                // so this exercises the real path.
                // One turn. The harness plays badly by design, and the enemies deal about
                // 31 a turn against a 63 HP player — three turns killed it before the win
                // could be forced, which is how the first attempts captured DEFEAT.
                for (int turn = 0; turn < 1; turn++)
                {
                    if (FindButton("Skip") != null) break;   // reward screen is up

                    // Play whatever is in hand. Clicking a card then an enemy is exactly what
                    // a player does; a card that needs no target plays on the first click and
                    // the enemy click is then a no-op.
                    for (int play = 0; play < 8; play++)
                    {
                        var cards = FindObjectsByType<CardView>(FindObjectsSortMode.None);
                        if (cards.Length == 0) break;
                        Click(cards[0].GetComponent<Button>());
                        yield return null;

                        var enemies = FindObjectsByType<EnemyView>(FindObjectsSortMode.None);
                        foreach (var enemy in enemies)
                        {
                            if (!enemy.Enemy.IsAlive) continue;
                            Click(enemy.GetComponent<Button>());
                            break;
                        }
                        yield return null;

                        // Nothing was consumed, so no card in hand is affordable.
                        if (FindObjectsByType<CardView>(FindObjectsSortMode.None).Length == cards.Length) break;
                    }

                    // After the harness has played its hand and before the turn ends: the only
                    // moment in the run where statuses, Block and Heat are all on the board.
                    yield return new WaitForSeconds(1.0f);
                    yield return Capture("03c-after-plays.png");

                    var endOfTurn = FindButton("EndTurn");
                    if (endOfTurn == null || !endOfTurn.interactable) break;
                    Click(endOfTurn);
                    yield return new WaitForSeconds(0.1f);
                }

                // Force the win so the reward screen can be captured. See
                // CombatView.DebugWinFight for why the harness does not try to win honestly.
                if (FindButton("Skip") == null)
                {
                    var view = FindFirstObjectByType<CombatView>();
                    if (view != null) view.DebugWinFight();
                    yield return new WaitForSeconds(0.8f);
                }

                Debug.Log($"[AutoCapture] ended on {(FindButton("Skip") != null ? "rewards" : "something else")}");
                yield return Capture("04-rewards.png");

                // Take a reward and prove the loop closes: deck grows, fight number advances,
                // health carries over. A screenshot of the reward screen alone does not show
                // that any of that actually happens.
                var offers = FindObjectsByType<CardView>(FindObjectsSortMode.None);
                if (offers.Length > 0)
                {
                    Click(offers[0].GetComponent<Button>());
                    yield return new WaitForSeconds(1.0f);
                    yield return Capture("05-map-after.png");
                }

                // The rest site sits several rows up the map, beyond anything the harness plays
                // to, so it is opened directly: heal-or-smith choice, picker, then the result.
                var restHost = FindFirstObjectByType<CombatView>();
                if (restHost != null)
                {
                    restHost.DebugOpenRest();
                    yield return new WaitForSeconds(0.8f);
                    yield return Capture("06-rest.png");

                    Click(FindButton("Smith"));
                    yield return new WaitForSeconds(0.8f);
                    yield return Capture("07-upgrade-picker.png");

                    var restScreen = FindFirstObjectByType<RestView>();
                    var choices = restScreen != null ? restScreen.GetComponentsInChildren<CardView>() : new CardView[0];
                    if (choices.Length > 0)
                    {
                        Click(choices[0].GetComponent<Button>());
                        yield return new WaitForSeconds(1.0f);
                    }
                    yield return Capture("08-map-after-upgrade.png");
                }

                // The shop with 160 gold — some shelves affordable, some not — then its removal picker.
                var shopHost = FindFirstObjectByType<CombatView>();
                if (shopHost != null)
                {
                    shopHost.DebugOpenShop();
                    yield return new WaitForSeconds(0.8f);
                    yield return Capture("08b-shop.png");
                    Click(FindButton("ShopRemove"));
                    yield return new WaitForSeconds(0.6f);
                    yield return Capture("08c-shop-remove.png");
                    Click(FindButton("ShopRemoveCancel"));
                    yield return new WaitForSeconds(0.2f);
                    Click(FindButton("ShopLeave"));
                    yield return new WaitForSeconds(0.4f);
                }

                // An event, then the outcome of its second choice (+40 gold).
                var eventHost = FindFirstObjectByType<CombatView>();
                if (eventHost != null)
                {
                    eventHost.DebugOpenEvent("abandoned_forge");
                    yield return new WaitForSeconds(0.6f);
                    yield return Capture("08d-event.png");
                    Click(FindButton("EventChoice1"));
                    yield return new WaitForSeconds(0.5f);
                    yield return Capture("08e-event-outcome.png");
                    Click(FindButton("EventContinue"));
                    yield return new WaitForSeconds(0.4f);
                }

                // One screenshot per encounter, so every portrait and every group's layout — three
                // Ash Mites is the widest — is checked in the real player rather than assumed.
                var encounterHost = FindFirstObjectByType<CombatView>();
                if (encounterHost != null)
                    foreach (var id in encounterHost.DebugEncounterIds())
                    {
                        encounterHost.DebugFightEncounter(id);
                        yield return new WaitForSeconds(0.8f);
                        yield return Capture($"enc-{id}.png");
                    }

                // The end-of-run screen both ways, over the run the harness has been playing.
                var endHost = FindFirstObjectByType<CombatView>();
                if (endHost != null)
                {
                    endHost.DebugShowEndOfRun(won: false);
                    yield return new WaitForSeconds(1.3f);
                    yield return Capture("09-end-defeat.png");
                    endHost.DebugShowEndOfRun(won: true);
                    yield return new WaitForSeconds(1.3f);
                    yield return Capture("10-end-victory.png");
                }

                // Report the state the save should restore to, so a second launch can be
                // checked against it rather than eyeballed.
                var runInfo = FindFirstObjectByType<CombatView>();
                Debug.Log($"[AutoCapture] STATE {(runInfo != null ? runInfo.DebugRunSummary() : "none")}");

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

            static void Click(Button button)
            {
                if (button == null) return;
                try { button.onClick.Invoke(); }
                catch (System.Exception e) { Debug.LogError($"[AutoCapture] click threw: {e}"); }
            }

            /// <summary>Any clickable map node — only reachable ones carry a Button.</summary>
            static Button FindFirstNodeButton()
            {
                foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
                    if (button.name.StartsWith("Node_")) return button;
                return null;
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
#endif
