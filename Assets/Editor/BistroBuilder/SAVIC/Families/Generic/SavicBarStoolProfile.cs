using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [CreateAssetMenu(menuName = "Bistro Builder/SAVIC/Bar Stool Authoring Profile")]
    public sealed class SavicBarStoolProfile : ScriptableObject
    {
        public string profileVersion = "1.0.0";
        [Header("Authored physical dimensions; source units are not inferred")]
        public float seatHeightMeters = 0.75f;
        public float counterHeightMeters = 1.05f;
        public float minimumSeatSpanMeters = 0.32f;
        public float maximumSeatSpanMeters = 0.65f;
        public float maximumTotalHeightMeters = 1.5f;
        public float maximumBodySpanMeters = 0.8f;
        [Header("Floor approach and native association")]
        public float customerApproachRadiusMeters = 0.32f;
        public float customerApproachMarginMeters = 0.1f;
        public float spotPositionToleranceMeters = 0.08f;
        public float maximumFacingAngleDegrees = 10f;
        internal bool IsValid => !string.IsNullOrWhiteSpace(profileVersion) &&
            Within(seatHeightMeters, 0.65f, 0.85f) && Within(counterHeightMeters, 0.9f, 1.2f) &&
            Within(counterHeightMeters - seatHeightMeters, 0.2f, 0.4f) &&
            Within(minimumSeatSpanMeters, 0.3f, 0.5f) && Within(maximumSeatSpanMeters, minimumSeatSpanMeters, 0.75f) &&
            Within(maximumTotalHeightMeters, seatHeightMeters, 1.6f) && Within(maximumBodySpanMeters, maximumSeatSpanMeters, 1f) &&
            Within(customerApproachRadiusMeters, 0.28f, 0.4f) && Within(customerApproachMarginMeters, 0.05f, 0.2f) &&
            Within(spotPositionToleranceMeters, 0.01f, 0.15f) && Within(maximumFacingAngleDegrees, 1f, 20f);
        private static bool Within(float value, float min, float max) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
    }
}
