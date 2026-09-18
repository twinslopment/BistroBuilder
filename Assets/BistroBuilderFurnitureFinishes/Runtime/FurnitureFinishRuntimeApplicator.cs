using UnityEngine;

namespace BistroBuilder.FurnitureFinishes
{
    public static class FurnitureFinishRuntimeApplicator
    {
        public static bool Apply(
            GameObject instance,
            FurnitureFinishPublishedSet publishedSet,
            string variantId)
        {
            if (instance == null || publishedSet == null)
                return false;

            FurnitureFinishPublishedSet.PublishedVariant variant;
            if (!publishedSet.TryGetVariant(variantId, out variant))
                variant = publishedSet.GetDefaultVariant();
            if (variant == null)
                return false;

            foreach (var binding in variant.Bindings)
            {
                if (binding == null)
                    continue;

                var transform = string.IsNullOrWhiteSpace(binding.RendererPath)
                    ? instance.transform
                    : instance.transform.Find(binding.RendererPath);
                if (transform == null)
                    return false;

                var renderer = transform.GetComponent<Renderer>();
                if (renderer == null)
                    return false;

                var materials = renderer.sharedMaterials;
                if (binding.MaterialIndex < 0 || binding.MaterialIndex >= materials.Length)
                    return false;

                materials[binding.MaterialIndex] = binding.Material;
                renderer.sharedMaterials = materials;
            }

            return true;
        }
    }
}