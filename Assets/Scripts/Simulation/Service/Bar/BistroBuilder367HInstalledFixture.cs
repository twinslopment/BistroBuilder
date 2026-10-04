using UnityEngine;

/// <summary>
/// Marca fixtures provisionales creados por el instalador 367H.
/// Permite reparar la escena de forma idempotente sin depender del nombre
/// visible del GameObject.
/// </summary>
[DefaultExecutionOrder(-32000)]
[DisallowMultipleComponent]
[AddComponentMenu("")]
public sealed class BistroBuilder367HInstalledFixture : MonoBehaviour
{
    [SerializeField]
    private string fixtureId = string.Empty;

    // Retired prototype content. Identity-based compatibility for old scenes;
    // published player-authored bars have their own placeable lifecycle.
    public const string RetiredBarFixtureId = "fixture_367h_bar";
    public bool IsRetired => FixtureId == RetiredBarFixtureId;

    private void Awake()
    {
        if (!Application.isPlaying || !IsRetired) return;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    public string FixtureId =>
        BistroBuilderOrderIdUtility.Normalize(fixtureId);

#if UNITY_EDITOR
    public bool EditorAssignFixtureId(string value)
    {
        string normalized = BistroBuilderOrderIdUtility.Normalize(value);

        if (!BistroBuilderOrderIdUtility.IsValid(normalized))
        {
            return false;
        }

        fixtureId = normalized;
        return true;
    }
#endif
}
