using System;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [CreateAssetMenu(menuName = "Bistro Builder/SAVIC/Floor Lamp Authoring Profile")]
    public sealed class SavicFloorLampProfile : ScriptableObject
    {
        public string profileVersion = "1.0.0";
        public float minimumHeight = 0.9f;
        public float maximumHeight = 2.5f;
        public float minimumWidth = 0.1f;
        public float maximumWidth = 0.9f;
        public float baseTopRatio = 0.08f;
        public float stemBottomRatio = 0.25f;
        public float stemTopRatio = 0.65f;
        public float shadeBottomRatio = 0.75f;
        public float maximumStemSpanRatio = 0.35f;
        public float minimumBaseSpanRatio = 0.45f;
        public float minimumShadeSpanRatio = 0.65f;
        [Header("Authored lighting, not inferred from source")]
        public float intensity = 1.5f;
        public float rangeMeters = 3f;
        public float colorTemperatureKelvin = 3000f;

        internal bool IsValid => !string.IsNullOrWhiteSpace(profileVersion) &&
            InRange(minimumHeight, 0.5f, 2.5f) && InRange(maximumHeight, minimumHeight, 3f) &&
            InRange(minimumWidth, 0.05f, 0.9f) && InRange(maximumWidth, minimumWidth, 1.2f) &&
            InRange(baseTopRatio, 0.02f, 0.15f) &&
            InRange(stemBottomRatio, baseTopRatio + 0.05f, 0.4f) &&
            InRange(stemTopRatio, stemBottomRatio + 0.15f, 0.7f) &&
            InRange(shadeBottomRatio, stemTopRatio + 0.05f, 0.9f) &&
            InRange(maximumStemSpanRatio, 0.05f, 0.4f) &&
            InRange(minimumBaseSpanRatio, 0.3f, 1f) && InRange(minimumShadeSpanRatio, 0.4f, 1f) &&
            InRange(intensity, 0.1f, 5f) && InRange(rangeMeters, 1f, 5f) &&
            InRange(colorTemperatureKelvin, 2000f, 6500f);

        private static bool InRange(float value, float minimum, float maximum) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
    }
}
