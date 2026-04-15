using UnityEngine;
using UnityEngine.UI;

namespace Race.UI
{
    public static class SpeedometerTextStyler
    {
        public static void Apply(Text target, SpeedometerTheme theme, float fontSizeMultiplier, TextAnchor alignment)
        {
            if (target == null || theme == null)
            {
                return;
            }

            target.font = theme.SpeedFont;
            target.color = theme.NumberColor;
            target.fontSize = Mathf.Max(1, Mathf.RoundToInt(theme.NumberFontSize * Mathf.Max(0.1f, fontSizeMultiplier)));
            target.alignment = alignment;
            target.horizontalOverflow = HorizontalWrapMode.Overflow;
            target.verticalOverflow = VerticalWrapMode.Overflow;
            target.resizeTextForBestFit = false;
            target.raycastTarget = false;

            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = theme.NumberOutlineColor;
            float outlineSize = Mathf.Lerp(1f, 8f, theme.NumberOutlineWidth);
            outline.effectDistance = new Vector2(outlineSize, -outlineSize);
        }
    }
}
