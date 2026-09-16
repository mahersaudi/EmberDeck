using System;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// The fade between screens: darken, swap, lift.
    ///
    /// A cut from a fight straight to the map is disorienting — the whole screen changes in one
    /// frame and the eye has to find everything again. Two tenths of a second of darkness gives
    /// the swap a beginning and an end, and hides the frame in which a screen is half built.
    ///
    /// It also swallows input while it is down, so the tap that chose a map node cannot land a
    /// second time on whatever appears underneath it.
    /// </summary>
    public sealed class Curtain : MonoBehaviour
    {
        const float FadeOut = 0.14f;
        const float FadeIn = 0.22f;

        static Curtain _instance;
        static bool _busy;

        /// <summary>
        /// Capture-harness use: screens swap with no fade at all. A screenshot taken during a fade
        /// is a picture of a dark rectangle, and the harness photographs on a timer.
        /// </summary>
        public static bool Instant;

        CanvasGroup _group;

        /// <summary>
        /// Runs <paramref name="swap"/> behind a fade. The swap is not immediate: everything that has
        /// to happen with it belongs inside it, not after the call.
        /// </summary>
        public static void Wipe(Action swap)
        {
            if (swap == null) return;

            // Already mid-transition, or asked to be instant: just do the work. Fades cannot nest —
            // a second curtain over the first would lift in the wrong order.
            if (Instant || _busy)
            {
                swap();
                return;
            }

            var curtain = Instance;
            _busy = true;
            curtain._group.blocksRaycasts = true;
            curtain.gameObject.SetActive(true);

            Motion.Run(curtain, "wipe", FadeOut, t => curtain._group.alpha = t, Motion.OutCubic, 0f, () =>
            {
                try
                {
                    swap();
                }
                catch (Exception e)
                {
                    // The curtain must come up even if the screen it was hiding threw.
                    Debug.LogException(e);
                }

                Motion.Run(curtain, "wipe", FadeIn, t => curtain._group.alpha = 1f - t, Motion.OutCubic, 0f, () =>
                {
                    _busy = false;
                    curtain._group.alpha = 0f;
                    curtain._group.blocksRaycasts = false;
                    curtain.gameObject.SetActive(false);
                });
            });
        }

        static Curtain Instance
        {
            get
            {
                if (_instance != null) return _instance;

                // Above every other canvas in the game, including the tip bubble and the pad's hint bar:
                // a transition covers the screen or it does nothing.
                var host = new GameObject("[Curtain]", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                                          typeof(CanvasGroup));
                DontDestroyOnLoad(host);

                var canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 600;
                UiFactory.ConfigureScaler(host.GetComponent<CanvasScaler>());

                _instance = host.AddComponent<Curtain>();
                _instance._group = host.GetComponent<CanvasGroup>();
                _instance._group.alpha = 0f;

                UiFactory.Stretch(UiFactory.Panel(host.transform, "Sheet", new Color(0.02f, 0.02f, 0.03f, 1f)));

                host.SetActive(false);
                return _instance;
            }
        }
    }
}
