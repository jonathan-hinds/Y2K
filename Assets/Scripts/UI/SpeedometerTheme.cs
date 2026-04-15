using UnityEngine;

namespace Race.UI
{
    [CreateAssetMenu(fileName = "SpeedometerTheme", menuName = "Race/UI/Speedometer Theme")]
    public sealed class SpeedometerTheme : ScriptableObject
    {
        [field: Header("Typography")]
        [field: SerializeField] public Font SpeedFont { get; private set; }
        [field: SerializeField] public Color NumberColor { get; private set; } = Color.white;
        [field: SerializeField] public Color NumberOutlineColor { get; private set; } = Color.black;
        [field: SerializeField, Range(0f, 1f)] public float NumberOutlineWidth { get; private set; } = 0.25f;
        [field: SerializeField, Min(1f)] public float NumberFontSize { get; private set; } = 68f;
        [field: SerializeField] public Vector2 NumberOffset { get; private set; } = new(0f, -6f);

        [field: Header("Meter")]
        [field: SerializeField] public Vector2Int TextureSize { get; private set; } = new(256, 176);
        [field: SerializeField] public Color EmptyColor { get; private set; } = new(0.18f, 0.03f, 0.03f, 0.92f);
        [field: SerializeField] public Color LowSpeedColor { get; private set; } = new(0.19f, 0.89f, 0.33f, 1f);
        [field: SerializeField] public Color MidSpeedColor { get; private set; } = new(0.96f, 0.89f, 0.22f, 1f);
        [field: SerializeField] public Color HighSpeedColor { get; private set; } = new(0.92f, 0.18f, 0.12f, 1f);
        [field: SerializeField] public Color MaxSpeedColor { get; private set; } = new(0.03f, 0.03f, 0.03f, 1f);
        [field: SerializeField, Range(0f, 1f)] public float YellowThreshold { get; private set; } = 0.45f;
        [field: SerializeField, Range(0f, 1f)] public float RedThreshold { get; private set; } = 0.78f;
        [field: SerializeField, Min(0f)] public float ResponseSharpness { get; private set; } = 10f;
    }
}
