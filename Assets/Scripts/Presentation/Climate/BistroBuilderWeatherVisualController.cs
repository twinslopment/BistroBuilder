using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Climate/Weather Visual Controller")]
public sealed class BistroBuilderWeatherVisualController : MonoBehaviour
{
    [SerializeField] private BistroBuilderClimateService climateService;

    [Header("Raíces visuales opcionales")]
    [SerializeField] private GameObject rainVisualRoot;
    [SerializeField] private GameObject snowVisualRoot;
    [SerializeField] private GameObject windVisualRoot;
    [SerializeField] private Light sunLight;

    [Header("Iluminación")]
    [SerializeField, Min(0f)] private float clearSunIntensity = 1f;
    [SerializeField, Min(0f)] private float cloudySunIntensity = 0.72f;
    [SerializeField, Min(0f)] private float precipitationSunIntensity = 0.58f;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();

        if (climateService == null)
            return;

        climateService.WeatherChanged -= HandleWeatherChanged;
        climateService.WeatherChanged += HandleWeatherChanged;

        if (climateService.IsInitialized)
            Apply(climateService.CurrentWeather);
    }

    private void OnDisable()
    {
        if (climateService != null)
            climateService.WeatherChanged -= HandleWeatherChanged;
    }

    private void HandleWeatherChanged(BistroBuilderWeatherState state)
    {
        Apply(state);
    }

    public void Apply(BistroBuilderWeatherState state)
    {
        if (state == null)
            return;

        SetActiveSafe(rainVisualRoot, state.isRaining);
        SetActiveSafe(snowVisualRoot, state.isSnowing);
        SetActiveSafe(windVisualRoot, state.isWindy);

        if (sunLight == null)
            return;

        if (state.isRaining || state.isSnowing)
            sunLight.intensity = precipitationSunIntensity;
        else if (state.cloudState == BistroBuilderCloudState.Cloudy)
            sunLight.intensity = cloudySunIntensity;
        else
            sunLight.intensity = clearSunIntensity;
    }

    private void CacheDependencies()
    {
        if (climateService == null)
        {
            climateService =
                UnityEngine.Object.FindFirstObjectByType<BistroBuilderClimateService>();
        }
    }

    private static void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependencies();
    }
#endif
}
