using UnityEngine;

namespace Race.Tagging
{
    [DisallowMultipleComponent]
    public sealed class GraffitiPaintTargetSettings : MonoBehaviour
    {
        [SerializeField] [Min(1)] private int sourceTextureWidthMultiplier = 1;
        [SerializeField] [Min(1)] private int sourceTextureHeightMultiplier = 1;

        public int SourceTextureWidthMultiplier => Mathf.Max(1, sourceTextureWidthMultiplier);

        public int SourceTextureHeightMultiplier => Mathf.Max(1, sourceTextureHeightMultiplier);

        public bool HasTextureTilingOverrides => SourceTextureWidthMultiplier > 1 || SourceTextureHeightMultiplier > 1;

        public void ConfigureTextureTiling(int widthMultiplier, int heightMultiplier)
        {
            sourceTextureWidthMultiplier = Mathf.Max(1, widthMultiplier);
            sourceTextureHeightMultiplier = Mathf.Max(1, heightMultiplier);
        }
    }
}
