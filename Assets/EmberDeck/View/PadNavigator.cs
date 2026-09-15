using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// Keyboard and gamepad play: focus that moves between whatever can be clicked on the screen in front,
    /// a ring around it, its tooltip, and a bar naming the buttons.
    ///
    /// Unity's own navigation is switched off (EventSystem.sendNavigationEvents) and replaced, for three
    /// reasons it could not meet. It navigates every Selectable in the scene, so focus wandered onto the
    /// hand behind the pause menu. Its automatic neighbours are computed once from layout, and a hand
    /// that re-fans on every play invalidates them. And it drops focus when the focused card is played,
    /// leaving a pad player with nothing selected and no way to see where they are. Here focus is always
    /// on the top screen, is found by position each time, and when its object disappears it moves to
    /// the nearest one left.
    ///
    /// The mouse always wins: moving it hides the ring and clears focus, so a mouse player never sees any
    /// of this. The first key or button press after the mouse only shows where focus is.
    ///
    /// Built on the legacy input manager like the rest of the project: sticks and the D-pad are axes
    /// added by ProjectSetup.ApplyInput, buttons are KeyCode.JoystickButton0 and up. Mappings follow the
    /// XInput layout Windows and Steam Input present; macOS is best effort (docs/controller.md).
    /// </summary>
    public sealed class PadNavigator : MonoBehaviour
    {
        public enum Device { Mouse, Keyboard, Gamepad }

        const float RepeatDelay = 0.38f;
        const float RepeatRate = 0.12f;
        const float StickThreshold = 0.5f;
        const float TooltipDelay = 0.2f;
        const float RingPadding = 7f;
        const float RingThickness = 3f;

        // Axis names added to the input manager by ProjectSetup.ApplyInput.
        public const string StickX = "Pad Stick X";
        public const string StickY = "Pad Stick Y";
        public const string DPadX = "Pad DPad X";
        public const string DPadY = "Pad DPad Y";

        static PadNavigator _instance;

        /// <summary>Capture-harness use: read no hardware at all, so a mouse on the desk cannot interfere.</summary>
        public static bool IgnoreHardware;

        public static PadNavigator Instance => _instance;

        /// <summary>True while a keyboard or pad is in charge and focus is being shown.</summary>
        public static bool Active => _instance != null && _instance._device != Device.Mouse;

        public static string FocusedName =>
            EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                ? EventSystem.current.currentSelectedGameObject.name
                : "none";

        /// <summary>The screen in front, whose buttons focus may move between.</summary>
        public Func<Transform> Scope;

        /// <summary>Whether End Turn applies now, for the button and its hint.</summary>
        public Func<bool> InCombat;

        /// <summary>Back pressed where the screen has no Back button of its own.</summary>
        public event Action Cancelled;
        public event Action PauseRequested;
        public event Action EndTurnRequested;

        Device _device = Device.Mouse;
        Canvas _canvas;
        readonly Image[] _edges = new Image[4];
        RectTransform _hintBar;
        Text _hints;
        string _hintText;
        Vector3 _lastMouse;
        Vector2 _heldDirection;
        float _nextRepeat;
        Transform _lastScope;
        Vector2 _lastFocusPoint;
        bool _hasFocusPoint;
        GameObject _lastSelected;
        float _selectedAt;
        TooltipTrigger _openTooltip;
        readonly HashSet<string> _missingAxes = new();

        public static PadNavigator Create()
        {
            if (_instance != null) return _instance;

            var host = new GameObject("[PadNavigator]", typeof(Canvas), typeof(CanvasScaler));
            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the tips (400), below the tooltip (500): the ring can circle a tip's button, and a
            // tooltip still reads on top of the ring it belongs to.
            canvas.sortingOrder = 450;

            var scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _instance = host.AddComponent<PadNavigator>();
            _instance._canvas = canvas;
            _instance.Build(host.transform);

            var events = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
            if (events != null) events.sendNavigationEvents = false;
            return _instance;
        }

        void Build(Transform host)
        {
            for (int i = 0; i < _edges.Length; i++)
            {
                var edge = UiFactory.Panel(host, $"FocusEdge{i}", Palette.Ink);
                edge.pivot = Vector2.zero;
                _edges[i] = edge.GetComponent<Image>();
                _edges[i].raycastTarget = false;
                edge.gameObject.SetActive(false);
            }

            _hintBar = UiFactory.Panel(host, "PadHints", new Color(0.06f, 0.06f, 0.08f, 0.9f));
            UiFactory.Frame(_hintBar.GetComponent<Image>(), "frame_panel", 4f);
            _hintBar.GetComponent<Image>().raycastTarget = false;
            UiFactory.Place(_hintBar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(760f, 32f));
            _hints = UiFactory.Label(_hintBar, "Text", "", 18, Palette.Ink);
            _hints.horizontalOverflow = HorizontalWrapMode.Overflow;   // one line; the bar is sized to it
            UiFactory.Stretch(_hints.rectTransform);
            _hintBar.gameObject.SetActive(false);
        }

        void Start() => _lastMouse = Input.mousePosition;

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ── Commands, from hardware or the capture harness ───────────────────────────

        public void SimulateMove(Vector2 direction) => Navigate(direction, Device.Gamepad);
        public void SimulateSubmit() => Submit(Device.Gamepad);
        public void SimulateCancel() => Cancel(Device.Gamepad);
        public void SetMouseMode() => SetDevice(Device.Mouse);

        /// <summary>Moves focus to an object, when focus is being shown. A mouse player's selection is left alone.</summary>
        public static void Focus(GameObject target)
        {
            if (!Active || target == null) return;
            var selectable = target.GetComponent<Selectable>();
            if (selectable != null && selectable.IsInteractable()) _instance.Select(selectable);
        }

        void Navigate(Vector2 direction, Device device)
        {
            if (!Activate(device)) return;
            var scope = CurrentScope();
            var current = SelectedIn(scope);
            if (current == null)
            {
                SelectDefault(scope);
                return;
            }

            // Left and right on a slider change its value, as they would on any settings screen.
            if (current is Slider slider && direction.y == 0f)
            {
                float facing = slider.transform.lossyScale.x < 0f ? -1f : 1f;   // a mirrored layout fills right to left
                slider.value += direction.x * facing * (slider.maxValue - slider.minValue) * 0.05f;
                return;
            }

            var next = FindInDirection(Candidates(scope), current, direction);
            if (next != null)
            {
                Select(next);
                AudioDirector.Play(Sfx.Click, 0.35f, 1.25f);
            }
        }

        void Submit(Device device)
        {
            if (!Activate(device)) return;
            var scope = CurrentScope();
            var current = SelectedIn(scope);
            if (current == null)
            {
                SelectDefault(scope);
                return;
            }
            ExecuteEvents.Execute(current.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }

        /// <summary>The screen's own Back button when it has one; otherwise whatever the game does with Back.</summary>
        void Cancel(Device? device)
        {
            if (device.HasValue) SetDevice(device.Value);
            var scope = CurrentScope();
            if (scope != null)
                foreach (var hint in scope.GetComponentsInChildren<NavHint>(false))
                {
                    if (!hint.Cancel) continue;
                    var button = hint.GetComponent<Button>();
                    if (button == null || !button.IsInteractable()) continue;
                    ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    return;
                }
            Cancelled?.Invoke();
        }

        /// <summary>Switches to a keyboard or pad. Returns false when this press only woke focus up.</summary>
        bool Activate(Device device)
        {
            bool wasMouse = _device == Device.Mouse;
            SetDevice(device);
            if (!wasMouse) return true;

            var scope = CurrentScope();
            if (SelectedIn(scope) == null) SelectDefault(scope);
            return false;
        }

        void SetDevice(Device device)
        {
            if (device == _device) return;
            _device = device;
            if (device != Device.Mouse) return;

            HideTooltip();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            SetRing(false);
            _hintBar.gameObject.SetActive(false);
        }

        // ── Hardware ─────────────────────────────────────────────────────────────────

        void Update()
        {
            if (IgnoreHardware) return;

            var mouse = Input.mousePosition;
            if ((mouse - _lastMouse).sqrMagnitude > 9f || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                SetDevice(Device.Mouse);
            _lastMouse = mouse;

            var keys = Quantize(KeyboardDirection());
            var pad = Quantize(PadDirection());
            var direction = keys != Vector2.zero ? keys : pad;
            var source = keys != Vector2.zero ? Device.Keyboard : Device.Gamepad;

            if (direction == Vector2.zero)
            {
                _heldDirection = Vector2.zero;
            }
            else if (direction != _heldDirection)
            {
                _heldDirection = direction;
                _nextRepeat = Time.unscaledTime + RepeatDelay;
                Navigate(direction, source);
            }
            else if (Time.unscaledTime >= _nextRepeat)
            {
                _nextRepeat = Time.unscaledTime + RepeatRate;
                Navigate(direction, source);
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                Submit(Device.Keyboard);
            if (PadDown(0, 16)) Submit(Device.Gamepad);

            // Escape backs out without taking over from the mouse; B on a pad does both.
            if (Input.GetKeyDown(KeyCode.Escape)) Cancel(null);
            if (PadDown(1, 17)) Cancel(Device.Gamepad);

            if (Input.GetKeyDown(KeyCode.E)) RequestEndTurn(Device.Keyboard);
            if (PadDown(3, 19)) RequestEndTurn(Device.Gamepad);

            if (PadDown(7, 9) || PadDown(6, 10))
            {
                SetDevice(Device.Gamepad);
                PauseRequested?.Invoke();
            }
        }

        void RequestEndTurn(Device device)
        {
            if (InCombat == null || !InCombat()) return;
            SetDevice(device);
            EndTurnRequested?.Invoke();
        }

        static Vector2 KeyboardDirection()
        {
            var v = Vector2.zero;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) v.x -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) v.x += 1f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) v.y += 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) v.y -= 1f;
            return v;
        }

        Vector2 PadDirection()
        {
            var stick = new Vector2(Axis(StickX), Axis(StickY));
            var dpad = new Vector2(Axis(DPadX), Axis(DPadY));
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // The macOS Xbox driver reports the D-pad as buttons 5 to 8 rather than as axes.
            if (Input.GetKey(KeyCode.JoystickButton5)) dpad.y = 1f;
            if (Input.GetKey(KeyCode.JoystickButton6)) dpad.y = -1f;
            if (Input.GetKey(KeyCode.JoystickButton7)) dpad.x = -1f;
            if (Input.GetKey(KeyCode.JoystickButton8)) dpad.x = 1f;
#endif
            return dpad.sqrMagnitude > stick.sqrMagnitude ? dpad : stick;
        }

        static bool PadDown(int windows, int mac)
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return Input.GetKeyDown(KeyCode.JoystickButton0 + windows) || Input.GetKeyDown(KeyCode.JoystickButton0 + mac);
#else
            return Input.GetKeyDown(KeyCode.JoystickButton0 + windows);
#endif
        }

        /// <summary>An axis's value, or 0 when the input manager does not define it (a build made before ApplyInput).</summary>
        float Axis(string name)
        {
            if (_missingAxes.Contains(name)) return 0f;
            try
            {
                return Input.GetAxisRaw(name);
            }
            catch (ArgumentException)
            {
                _missingAxes.Add(name);
                Debug.LogWarning($"[EmberDeck] Input axis '{name}' is not set up; that part of the gamepad is ignored.");
                return 0f;
            }
        }

        static Vector2 Quantize(Vector2 v)
        {
            if (v.magnitude < StickThreshold) return Vector2.zero;
            return Mathf.Abs(v.x) > Mathf.Abs(v.y) ? new Vector2(Mathf.Sign(v.x), 0f) : new Vector2(0f, Mathf.Sign(v.y));
        }

        // ── Focus ────────────────────────────────────────────────────────────────────

        void LateUpdate()
        {
            if (_device == Device.Mouse) return;

            var scope = CurrentScope();
            var current = SelectedIn(scope);
            if (current == null && scope != null)
            {
                // The focused object went away — a card was played, a screen closed. On the same screen,
                // the nearest thing left is where the player's eye already is.
                var candidates = Candidates(scope);
                Selectable pick = scope == _lastScope && _hasFocusPoint ? Nearest(candidates, _lastFocusPoint) : null;
                pick ??= Default(candidates);
                if (pick != null) Select(pick);
                current = pick;
            }
            _lastScope = scope;

            if (current != null)
            {
                _lastFocusPoint = Center(current);
                _hasFocusPoint = true;
            }

            UpdateRing(current);
            UpdateTooltip(current);
            UpdateHints();
        }

        void Select(Selectable target)
        {
            if (EventSystem.current == null || target == null) return;
            if (EventSystem.current.currentSelectedGameObject == target.gameObject) return;
            HideTooltip();
            EventSystem.current.SetSelectedGameObject(target.gameObject);
            _selectedAt = Time.unscaledTime;
            _lastFocusPoint = Center(target);
            _hasFocusPoint = true;
        }

        void SelectDefault(Transform scope)
        {
            if (scope == null) return;
            var pick = Default(Candidates(scope));
            if (pick != null) Select(pick);
        }

        /// <summary>The top screen, narrowed to an open tip or picker on it.</summary>
        Transform CurrentScope()
        {
            var scope = Scope?.Invoke();
            // A tip on screen takes focus first: one press of A reads it and moves on.
            var tip = Coach.NavScope;
            if (tip != null) scope = tip;
            if (scope == null) return null;

            NavHint modal = null;
            foreach (var hint in scope.GetComponentsInChildren<NavHint>(false))
                if (hint.Modal) modal = hint;
            return modal != null ? modal.transform : scope;
        }

        static Selectable SelectedIn(Transform scope)
        {
            if (scope == null || EventSystem.current == null) return null;
            var go = EventSystem.current.currentSelectedGameObject;
            if (go == null || !go.activeInHierarchy || !go.transform.IsChildOf(scope)) return null;
            var selectable = go.GetComponent<Selectable>();
            return Usable(selectable) ? selectable : null;
        }

        static bool Usable(Selectable selectable)
        {
            if (selectable == null || !selectable.isActiveAndEnabled || !selectable.IsInteractable()) return false;
            var hint = selectable.GetComponent<NavHint>();
            if (hint != null && hint.Skip) return false;
            var card = selectable.GetComponent<CardView>();
            return card == null || !card.IsLeaving;
        }

        static List<Selectable> Candidates(Transform scope)
        {
            var list = new List<Selectable>();
            if (scope == null) return list;
            foreach (var selectable in scope.GetComponentsInChildren<Selectable>(false))
                if (Usable(selectable)) list.Add(selectable);
            return list;
        }

        static Selectable Default(List<Selectable> candidates)
        {
            Selectable best = null;
            int bestPriority = int.MinValue;
            Vector2 bestCenter = default;
            foreach (var candidate in candidates)
            {
                var hint = candidate.GetComponent<NavHint>();
                int priority = hint != null ? hint.Priority : 0;
                var center = Center(candidate);
                bool better = best == null
                              || priority > bestPriority
                              || (priority == bestPriority && (center.y > bestCenter.y + 4f
                                                              || (Mathf.Abs(center.y - bestCenter.y) <= 4f && center.x < bestCenter.x)));
                if (!better) continue;
                best = candidate;
                bestPriority = priority;
                bestCenter = center;
            }
            return best;
        }

        static Selectable Nearest(List<Selectable> candidates, Vector2 point)
        {
            Selectable best = null;
            float bestDistance = float.MaxValue;
            foreach (var candidate in candidates)
            {
                float distance = (Center(candidate) - point).sqrMagnitude;
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>
        /// The closest object in a direction, weighing sideways distance more than forward distance, so Up
        /// from a card reaches the enemy above it rather than one far to the side that is slightly nearer.
        /// </summary>
        static Selectable FindInDirection(List<Selectable> candidates, Selectable from, Vector2 direction)
        {
            var origin = Center(from);
            Selectable best = null;
            float bestScore = float.MaxValue;
            foreach (var candidate in candidates)
            {
                if (candidate == from) continue;
                var offset = Center(candidate) - origin;
                float along = Vector2.Dot(offset, direction);
                if (along < 8f) continue;
                float across = Mathf.Abs(direction.x != 0f ? offset.y : offset.x);
                float score = along + across * 2.5f;
                if (score >= bestScore) continue;
                best = candidate;
                bestScore = score;
            }
            return best;
        }

        static Vector2 Center(Component target)
        {
            var rect = target.transform as RectTransform;
            if (rect == null) return target.transform.position;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }

        // ── What the player sees ─────────────────────────────────────────────────────

        void UpdateRing(Selectable current)
        {
            if (current == null)
            {
                SetRing(false);
                return;
            }

            var rect = (RectTransform)current.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (var corner in corners)
            {
                xMin = Mathf.Min(xMin, corner.x);
                yMin = Mathf.Min(yMin, corner.y);
                xMax = Mathf.Max(xMax, corner.x);
                yMax = Mathf.Max(yMax, corner.y);
            }

            float scale = _canvas.scaleFactor;
            float pad = RingPadding * scale, t = RingThickness * scale;
            xMin -= pad; yMin -= pad; xMax += pad; yMax += pad;
            float width = xMax - xMin, height = yMax - yMin;
            SetEdge(0, xMin, yMin - t, width, t, scale);
            SetEdge(1, xMin, yMax, width, t, scale);
            SetEdge(2, xMin - t, yMin - t, t, height + t * 2f, scale);
            SetEdge(3, xMax, yMin - t, t, height + t * 2f, scale);
            SetRing(true);

            float pulse = 0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
            foreach (var edge in _edges)
                edge.color = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, pulse);
        }

        void SetEdge(int index, float x, float y, float width, float height, float scale)
        {
            var rect = _edges[index].rectTransform;
            rect.position = new Vector3(x, y, 0f);
            rect.sizeDelta = new Vector2(width / scale, height / scale);
        }

        void SetRing(bool on)
        {
            foreach (var edge in _edges)
                if (edge.gameObject.activeSelf != on) edge.gameObject.SetActive(on);
        }

        /// <summary>A focused object explains itself as a hovered one does, after a short pause so a quick pass does not flash.</summary>
        void UpdateTooltip(Selectable current)
        {
            var go = current != null ? current.gameObject : null;
            if (go != _lastSelected)
            {
                HideTooltip();
                _lastSelected = go;
                _selectedAt = Time.unscaledTime;
            }
            if (go == null || _openTooltip != null || Time.unscaledTime - _selectedAt < TooltipDelay) return;

            var trigger = go.GetComponent<TooltipTrigger>();
            if (trigger == null) return;
            trigger.ShowNow();
            _openTooltip = trigger;
        }

        void HideTooltip()
        {
            if (_openTooltip != null) Tooltip.Hide(_openTooltip);
            _openTooltip = null;
        }

        void UpdateHints()
        {
            bool combat = InCombat != null && InCombat();
            string text;
            if (_device == Device.Gamepad)
            {
                text = $"{Glyph("A", "6CC24A")}  Select        {Glyph("B", "E4572E")}  Back"
                       + (combat ? $"        {Glyph("Y", "F2C14E")}  End Turn" : "")
                       + $"        {Glyph("Start", "C8C8D0")}  Menu";
            }
            else
            {
                text = $"{Glyph("Arrows", "C8C8D0")}  Move        {Glyph("Enter", "C8C8D0")}  Select        {Glyph("Esc", "C8C8D0")}  Back"
                       + (combat ? $"        {Glyph("E", "C8C8D0")}  End Turn" : "");
            }

            if (text != _hintText)
            {
                _hintText = text;
                _hints.text = text;
                // Sized to its words: a fixed-width bar ran into the map's bottom row with half of it empty.
                _hintBar.sizeDelta = new Vector2(Mathf.Max(260f, _hints.preferredWidth + 56f), _hintBar.sizeDelta.y);
            }
            if (!_hintBar.gameObject.activeSelf) _hintBar.gameObject.SetActive(true);
        }

        static string Glyph(string label, string colour) => $"<b><color=#{colour}>{label}</color></b>";
    }
}
