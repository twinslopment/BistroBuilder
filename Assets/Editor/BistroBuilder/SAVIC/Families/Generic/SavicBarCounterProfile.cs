using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [CreateAssetMenu(menuName = "Bistro Builder/SAVIC/Bar Counter Authoring Profile")]
    public sealed class SavicBarCounterProfile : ScriptableObject
    {
        public string profileVersion = "1.0.0";
        [Header("Authored dimensions, not inferred source units")]
        public float counterHeightMeters = 1.05f;
        public float minimumWidthMeters = 1.5f;
        public float maximumWidthMeters = 8f;
        public float minimumDepthMeters = 1f;
        public float maximumDepthMeters = 5f;
        public float maximumTotalHeightMeters = 1.8f;
        public float minimumUpperSurfaceCoverage = 0.08f;
        public float minimumWaiterClearanceMeters = 0.35f;
        public float customerOffsetMeters = 0.45f;

        internal bool IsValid => !string.IsNullOrWhiteSpace(profileVersion) &&
            InRange(counterHeightMeters, 0.9f, 1.2f) &&
            InRange(minimumWidthMeters, 1f, 8f) && InRange(maximumWidthMeters, minimumWidthMeters, 10f) &&
            InRange(minimumDepthMeters, 0.8f, 5f) && InRange(maximumDepthMeters, minimumDepthMeters, 6f) &&
            InRange(maximumTotalHeightMeters, counterHeightMeters, 2f) &&
            InRange(minimumUpperSurfaceCoverage, 0.08f, 0.8f) &&
            InRange(minimumWaiterClearanceMeters, 0.35f, 0.8f) && InRange(customerOffsetMeters, 0.35f, 0.8f);

        private static bool InRange(float value, float min, float max) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
    }
}
