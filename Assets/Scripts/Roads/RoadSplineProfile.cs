using UnityEngine;
using UnityEngine.Splines;

namespace Race.Roads
{
    [CreateAssetMenu(fileName = "RoadSplineProfile", menuName = "Race/Road Spline Profile")]
    public sealed class RoadSplineProfile : ScriptableObject
    {
        public event System.Action Changed;

        [field: Header("Segment")]
        [field: SerializeField] public GameObject SegmentPrefab { get; private set; }

        [field: Min(0.01f)]
        [field: SerializeField] public float SegmentSpacing { get; private set; } = 19.336f;

        [field: Header("Alignment")]
        [field: SerializeField] public SplineComponent.AlignAxis ForwardAxis { get; private set; } =
            SplineComponent.AlignAxis.YAxis;

        [field: SerializeField] public SplineComponent.AlignAxis UpAxis { get; private set; } =
            SplineComponent.AlignAxis.ZAxis;

        [field: SerializeField] public float CrossSectionRollDegrees { get; private set; } = 90f;

        [field: Header("Instantiation")]
        [field: SerializeField] public SplineInstantiate.Space CoordinateSpace { get; private set; } =
            SplineInstantiate.Space.Local;

        private void OnValidate()
        {
            Changed?.Invoke();
        }
    }
}
