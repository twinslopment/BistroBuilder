using System;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    /// <summary>
    /// Converts imported model geometry into SAVIC metric space.
    /// The source root translation and any external parent transform are ignored,
    /// while the source root rotation/scale applied by Unity's importer is preserved.
    /// This prevents importer unit conversion from being cancelled during analysis.
    /// </summary>
    internal static class SavicMetricSpace
    {
        internal const string Version = "1.0.0";

        internal static Matrix4x4 LocalToMetric(
            Transform root,
            Transform owner)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            Matrix4x4 ownerToRootLocal =
                root.worldToLocalMatrix *
                owner.localToWorldMatrix;

            Matrix4x4 rootImporterTransform =
                Matrix4x4.TRS(
                    Vector3.zero,
                    root.localRotation,
                    root.localScale);

            return
                rootImporterTransform *
                ownerToRootLocal;
        }
    }
}
