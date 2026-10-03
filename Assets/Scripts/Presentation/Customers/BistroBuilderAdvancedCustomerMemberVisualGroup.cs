using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Materializa un proxy visual ligero por miembro lógico de CustomerGroup.
/// No crea nuevos clientes de simulación: solo hace visibles/clicables los ya existentes.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderAdvancedCustomerMemberVisualGroup : MonoBehaviour
{
    private const string VisualPrefix = "BB_CustomerMemberVisual_";
    [SerializeField] private CustomerGroup customerGroup;
    [SerializeField] private MeshRenderer groupPlaceholderRenderer;
    [SerializeField, Min(0.15f)] private float memberSpacing = 0.42f;
    [SerializeField] private Vector3 memberScale = new Vector3(0.32f, 0.55f, 0.32f);
    [SerializeField] private BistroBuilderCustomerHumanoidProfile humanoidProfile;

    private readonly List<GameObject> visuals = new List<GameObject>(8);
    private int builtForSize;

    public int VisualCount => visuals.Count;
    public CustomerGroup CustomerGroup => customerGroup;
    public BistroBuilderCustomerHumanoidProfile HumanoidProfile => humanoidProfile;

#if UNITY_EDITOR
    public void ConfigureHumanoidForEditor(BistroBuilderCustomerHumanoidProfile profile)
    { humanoidProfile = profile; builtForSize = 0; }
#endif

    private void Awake() => CacheReferences();

    private void Start()
    {
        if (Application.isPlaying) EnsureVisuals();
    }

    private void LateUpdate()
    {
        if (Application.isPlaying && customerGroup != null &&
            builtForSize != customerGroup.GroupSize)
            EnsureVisuals();
    }
    public bool ValidateConfiguration(out string error)
    {
        CacheReferences();
        if (customerGroup == null)
        {
            error = "10G necesita CustomerGroup en el prefab de clientes.";
            return false;
        }
        if (groupPlaceholderRenderer == null)
        {
            error = "10G necesita el renderer visual del CustomerGroup.";
            return false;
        }
        if (humanoidProfile != null && !humanoidProfile.ValidateConfiguration(out error)) return false;
        error = string.Empty;
        return true;
    }

    public bool EnsureVisuals()
    {
        CacheReferences();
        if (customerGroup == null || customerGroup.GroupSize < 1) return false;
        if (humanoidProfile != null && !humanoidProfile.ValidateConfiguration(out _)) return false;
        if (builtForSize == customerGroup.GroupSize && visuals.Count == builtForSize)
            return true;

        ClearVisuals();
        Material sharedMaterial = groupPlaceholderRenderer != null
            ? groupPlaceholderRenderer.sharedMaterial : null;
        for (int index = 1; index <= customerGroup.GroupSize; index++)
            visuals.Add(CreateMemberVisual(index, customerGroup.GroupSize, sharedMaterial));
        if (visuals.Exists(visual => visual == null)) { ClearVisuals(); return false; }
        builtForSize = customerGroup.GroupSize;
        if (groupPlaceholderRenderer != null) groupPlaceholderRenderer.enabled = false;
        return visuals.Count == customerGroup.GroupSize;
    }

    public BistroBuilderAdvancedCustomerMemberHitTarget GetHitTarget(int memberIndex)
    {
        if (memberIndex < 1 || memberIndex > visuals.Count) return null;
        GameObject visual = visuals[memberIndex - 1];
        return visual != null
            ? visual.GetComponent<BistroBuilderAdvancedCustomerMemberHitTarget>() : null;
    }
    private GameObject CreateMemberVisual(
        int memberIndex,
        int groupSize,
        Material sharedMaterial)
    {
        if (humanoidProfile != null && humanoidProfile.ValidateConfiguration(out _))
            return CreateHumanoidMember(memberIndex, groupSize);
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = VisualPrefix + memberIndex.ToString("D2");
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = CalculateLocalPosition(memberIndex, groupSize);
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = memberScale;
        visual.layer = gameObject.layer;

        MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
        if (renderer != null && sharedMaterial != null)
            renderer.sharedMaterial = sharedMaterial;
        Collider collider = visual.GetComponent<Collider>();
        if (collider != null) collider.isTrigger = true;
        var target = visual.AddComponent<BistroBuilderAdvancedCustomerMemberHitTarget>();
        target.Bind(customerGroup, memberIndex);
        return visual;
    }

    private GameObject CreateHumanoidMember(int memberIndex, int groupSize)
    {
        GameObject visual = new GameObject(VisualPrefix + memberIndex.ToString("D2"));
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = CalculateLocalPosition(memberIndex, groupSize);
        visual.layer = gameObject.layer;
        GameObject model = Instantiate(humanoidProfile.ModelPrefab, visual.transform, false);
        model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * humanoidProfile.ModelScale;
        foreach (Collider sourceCollider in model.GetComponentsInChildren<Collider>(true)) sourceCollider.enabled = false;
        var hitCollider = visual.AddComponent<CapsuleCollider>();
        hitCollider.isTrigger = true; hitCollider.height = 1.8f; hitCollider.radius = 0.25f;
        hitCollider.center = Vector3.up * 0.9f;
        var target = visual.AddComponent<BistroBuilderAdvancedCustomerMemberHitTarget>();
        target.Bind(customerGroup, memberIndex);
        if (!visual.AddComponent<BistroBuilderCustomerBarSeatPresenter>().ConfigureRuntime(customerGroup, memberIndex, humanoidProfile))
        { Destroy(visual); return null; }
        return visual;
    }

    private Vector3 CalculateLocalPosition(int memberIndex, int groupSize)
    {
        if (groupSize <= 1) return Vector3.zero;
        float angle = (memberIndex - 1) * Mathf.PI * 2f / groupSize;
        float radius = groupSize <= 3 ? memberSpacing * 0.65f : memberSpacing;
        return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    private void ClearVisuals()
    {
        for (int i = visuals.Count - 1; i >= 0; i--)
            if (visuals[i] != null) Destroy(visuals[i]);
        visuals.Clear();
        builtForSize = 0;
    }
    private void OnDisable()
    {
        if (groupPlaceholderRenderer != null && !Application.isPlaying)
            groupPlaceholderRenderer.enabled = true;
    }

    private void CacheReferences()
    {
        if (customerGroup == null) customerGroup = GetComponent<CustomerGroup>();
        if (groupPlaceholderRenderer == null)
            groupPlaceholderRenderer = GetComponent<MeshRenderer>();
    }

#if UNITY_EDITOR
    private void Reset() => CacheReferences();
    private void OnValidate()
    {
        CacheReferences();
        memberSpacing = Mathf.Max(0.15f, memberSpacing);
        memberScale.x = Mathf.Max(0.1f, memberScale.x);
        memberScale.y = Mathf.Max(0.1f, memberScale.y);
        memberScale.z = Mathf.Max(0.1f, memberScale.z);
    }
#endif
}
