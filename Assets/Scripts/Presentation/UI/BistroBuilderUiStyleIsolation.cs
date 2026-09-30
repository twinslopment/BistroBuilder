using UnityEngine;

/// <summary>
/// Marks a UI subtree whose visual contract is fully authored by its owner.
/// The global BistroBuilderUiDesignSystem must not restyle descendants.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderUiStyleIsolation : MonoBehaviour
{
}
