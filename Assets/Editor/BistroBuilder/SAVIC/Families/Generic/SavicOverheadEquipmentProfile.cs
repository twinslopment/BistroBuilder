using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [CreateAssetMenu(menuName = "Bistro Builder/SAVIC/Passive Overhead Equipment Profile")]
    public sealed class SavicOverheadEquipmentProfile : ScriptableObject
    {
        public string profileVersion = "1.0.0";
        [Header("Authored dimensions; source units and ceiling height are not inferred")]
        public float widthMeters = 2f;
        public float installationBottomMeters = 2.2f;
        public float maximumBodyHeightMeters = 0.8f;
        public float minimumDepthMeters = 0.3f;
        public float maximumDepthMeters = 1.5f;
        internal bool IsValid => !string.IsNullOrWhiteSpace(profileVersion) &&
            Range(widthMeters, 0.8f, 4f) && Range(installationBottomMeters, 2.2f, 3.5f) &&
            Range(maximumBodyHeightMeters, 0.1f, 1.5f) && Range(minimumDepthMeters, 0.1f, 1f) &&
            Range(maximumDepthMeters, minimumDepthMeters, 2f);
        private static bool Range(float value, float min, float max) => !float.IsNaN(value) &&
            !float.IsInfinity(value) && value >= min && value <= max;
    }
}
