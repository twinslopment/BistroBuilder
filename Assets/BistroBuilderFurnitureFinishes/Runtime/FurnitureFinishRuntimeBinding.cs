using UnityEngine;

namespace BistroBuilder.FurnitureFinishes
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Bistro Builder/Furniture Finishes/Runtime Binding")]
    public sealed class FurnitureFinishRuntimeBinding : MonoBehaviour
    {
        [SerializeField] private FurnitureFinishPublishedSet publishedSet;
        [SerializeField] private string variantId = string.Empty;
        [SerializeField] private bool applyOnAwake = true;

        public FurnitureFinishPublishedSet PublishedSet => publishedSet;
        public string VariantId => variantId;
        public bool ApplyOnAwake => applyOnAwake;

        private void Awake()
        {
            if (applyOnAwake)
                ApplyCurrent();
        }

        public bool ApplyCurrent()
        {
            return FurnitureFinishRuntimeApplicator.Apply(
                gameObject,
                publishedSet,
                variantId);
        }

        public bool ApplyVariant(string newVariantId)
        {
            if (publishedSet == null)
                return false;

            if (!string.IsNullOrWhiteSpace(newVariantId)
                && !publishedSet.TryGetVariant(newVariantId, out _))
                return false;

            variantId = newVariantId ?? string.Empty;
            return ApplyCurrent();
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            FurnitureFinishPublishedSet set,
            string selectedVariantId,
            bool shouldApplyOnAwake)
        {
            publishedSet = set;
            variantId = selectedVariantId ?? string.Empty;
            applyOnAwake = shouldApplyOnAwake;
        }
#endif
    }
}