using UnityEngine;

/// <summary>
/// Marca fixtures provisionales creados por el instalador 367H.
/// Permite reparar la escena de forma idempotente sin depender del nombre
/// visible del GameObject.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("")]
public sealed class BistroBuilder367HInstalledFixture : MonoBehaviour
{
    [SerializeField]
    private string fixtureId = string.Empty;

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
