using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// The one text component the interface uses: legacy Text that translates what it is given, and draws
    /// Arabic correctly.
    ///
    /// Assigning text stores the English, translates it (Loc), and when the result is Arabic hands the base
    /// component a shaped, line-broken, right-to-left string (ArabicText) instead. Reading text returns what
    /// was assigned, so code that compares or rebuilds a label keeps working in English. The shaped string
    /// depends on the rectangle's width, so it is redone whenever the width, wrapping or font size changes.
    ///
    /// Under a mirrored layout (a right-to-left language flips the whole stage, see CombatView) the glyph
    /// quads are mirrored back inside the label's own rectangle, so text reads forwards while its position
    /// follows the mirror.
    /// </summary>
    public sealed class UiText : Text
    {
        string _source;
        string _translated;
        bool _drawing;
        float _laidOutWidth = -1f;
        HorizontalWrapMode _laidOutWrap;
        int _laidOutSize = -1;
        FontStyle _laidOutStyle;

        // Text.OnPopulateMesh reads this property to draw, so while drawing it must return the laid-out string;
        // everywhere else it returns what the code assigned. Returning the English here was why every label
        // that was not translated before assignment drew in English.
        public override string text
        {
            get => _drawing || _source == null ? m_Text : _source;
            set
            {
                value ??= "";
                if (_source == value) return;
                _source = value;
                _translated = Loc.Translate(value);
                if (ArabicText.HasArabic(_translated) && UiFactory.ArabicFont != null && font != UiFactory.ArabicFont)
                    font = UiFactory.ArabicFont;   // an Arabic word in an English interface, like the language setting
                Relayout();
                SetVerticesDirty();
                SetLayoutDirty();
            }
        }

        bool Shaped => _translated != null && ArabicText.HasArabic(_translated);

        bool Stale => Shaped && (Mathf.Abs(rectTransform.rect.width - _laidOutWidth) > 0.5f
                                 || _laidOutWrap != horizontalOverflow
                                 || _laidOutSize != fontSize
                                 || _laidOutStyle != fontStyle);

        void Relayout()
        {
            if (!Shaped)
            {
                m_Text = _translated ?? "";
                return;
            }

            float width = rectTransform.rect.width;
            bool wrap = horizontalOverflow == HorizontalWrapMode.Wrap && width > 1f;
            // A little short of the full width: Text re-wraps any line its own measurement finds too long, and its
            // rounding at the canvas scale differs slightly from these advances. Without the margin a tooltip grew a
            // third line its layout had not made room for.
            m_Text = ArabicText.Layout(_translated, Measure, width * 0.94f, wrap);
            _laidOutWidth = width;
            _laidOutWrap = horizontalOverflow;
            _laidOutSize = fontSize;
            _laidOutStyle = fontStyle;
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (!Stale) return;
            Relayout();
            SetVerticesDirty();
        }

        public override float preferredWidth
        {
            get
            {
                if (Stale) Relayout();
                return base.preferredWidth;
            }
        }

        public override float preferredHeight
        {
            get
            {
                if (Stale) Relayout();
                return base.preferredHeight;
            }
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            if (Stale) Relayout();
            _drawing = true;
            try
            {
                base.OnPopulateMesh(toFill);
            }
            finally
            {
                _drawing = false;
            }
            if (transform.lossyScale.x >= 0f) return;

            float axis = rectTransform.rect.center.x * 2f;
            var vertex = new UIVertex();
            for (int i = 0; i < toFill.currentVertCount; i++)
            {
                toFill.PopulateUIVertex(ref vertex, i);
                vertex.position.x = axis - vertex.position.x;
                toFill.SetUIVertex(vertex, i);
            }
        }

        /// <summary>Advance widths at this label's size, in the same units as its rectangle.</summary>
        float Measure(string value)
        {
            if (font == null || string.IsNullOrEmpty(value)) return 0f;
            font.RequestCharactersInTexture(value, fontSize, fontStyle);
            float width = 0f;
            foreach (char c in value)
                width += font.GetCharacterInfo(c, out var info, fontSize, fontStyle) ? info.advance : fontSize * 0.55f;
            return width;
        }
    }
}
