using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BistroBuilder.AssetStudioBB;

namespace BistroBuilder.AssetStudioBB.Editor
{
    /// <summary>
    /// Contrato público y deliberadamente pequeño entre Asset Studio BB
    /// y las herramientas de orquestación de Bistro Builder.
    ///
    /// Asset Studio conserva internamente su manifiesto y sus validadores;
    /// el Taller de Objetos 3D solo necesita una vista segura del estado y
    /// una operación para generar las variantes visuales.
    /// </summary>
    public static class AssetStudioBBFacade
    {
        public enum PublicSeverity
        {
            Information = 0,
            Warning = 1,
            Error = 2
        }

        [Serializable]
        public sealed class PublicValidationMessage
        {
            public PublicSeverity Severity;
            public string Text = string.Empty;
        }

        [Serializable]
        public sealed class Inspection
        {
            public string ManifestPath = string.Empty;
            public string AssetId = string.Empty;
            public string DisplayName = string.Empty;
            public string Description = string.Empty;
            public string Category = string.Empty;
            public string Family = string.Empty;
            public string Subtype = string.Empty;
            public string Quality = string.Empty;
            public Vector3 DimensionsMeters = Vector3.zero;
            public float SeatHeightMeters;
            public float WorktopHeightMeters;
            public int SourceMeshCount;
            public int SourceTriangleCount;
            public int VariantCount;
            public bool HasErrors;
            public readonly List<PublicValidationMessage> Validation =
                new List<PublicValidationMessage>();
        }

        /// <summary>
        /// Comprueba si la selección pertenece a Asset Studio BB y devuelve
        /// únicamente datos estables aptos para otras herramientas de Editor.
        /// </summary>
        public static bool TryInspect(
            UnityEngine.Object selectedObject,
            out Inspection inspection)
        {
            inspection = null;

            if (selectedObject == null)
            {
                return false;
            }

            string selectedPath =
                AssetDatabase.GetAssetPath(selectedObject);

            string manifestPath =
                AssetStudioBBPaths.FindManifestFromSelection(selectedPath);

            if (!AssetStudioBBManifest.TryLoad(
                    manifestPath,
                    out AssetStudioBBManifest manifest
                ))
            {
                return false;
            }

            IReadOnlyList<AssetStudioBBValidationMessage> validation =
                AssetStudioBBValidator.Validate(
                    manifestPath,
                    manifest
                );

            Inspection result =
                new Inspection
                {
                    ManifestPath = manifestPath,
                    AssetId = manifest.assetId ?? string.Empty,
                    DisplayName = manifest.displayName ?? string.Empty,
                    Description = manifest.description ?? string.Empty,
                    Category = manifest.category ?? string.Empty,
                    Family = manifest.family ?? string.Empty,
                    Subtype = manifest.subtype ?? string.Empty,
                    Quality = manifest.quality ?? string.Empty,
                    DimensionsMeters = new Vector3(
                        manifest.dimensions.targetWidthM,
                        manifest.dimensions.targetHeightM,
                        manifest.dimensions.targetDepthM
                    ),
                    SeatHeightMeters = manifest.dimensions.seatHeightM,
                    WorktopHeightMeters = manifest.dimensions.worktopHeightM,
                    SourceMeshCount = manifest.geometry.meshCount,
                    SourceTriangleCount = manifest.geometry.triangleCountSource,
                    VariantCount = manifest.variants != null
                        ? manifest.variants.Length
                        : 0,
                    HasErrors = AssetStudioBBValidator.HasErrors(validation)
                };

            for (int index = 0;
                 index < validation.Count;
                 index++)
            {
                AssetStudioBBValidationMessage source = validation[index];

                result.Validation.Add(
                    new PublicValidationMessage
                    {
                        Severity = ConvertSeverity(source.Severity),
                        Text = source.Text ?? string.Empty
                    }
                );
            }

            inspection = result;
            return true;
        }

        /// <summary>
        /// Genera o actualiza las variantes visuales del asset seleccionado.
        /// Lanza una excepción descriptiva si el manifiesto no es válido.
        /// </summary>
        public static AssetStudioBBVariantSet GenerateVariants(
            UnityEngine.Object selectedObject
        )
        {
            if (selectedObject == null)
            {
                throw new InvalidOperationException(
                    "No hay ningún asset de Asset Studio BB seleccionado."
                );
            }

            string selectedPath =
                AssetDatabase.GetAssetPath(selectedObject);

            string manifestPath =
                AssetStudioBBPaths.FindManifestFromSelection(selectedPath);

            if (!AssetStudioBBManifest.TryLoad(
                    manifestPath,
                    out AssetStudioBBManifest manifest
                ))
            {
                throw new InvalidOperationException(
                    "La selección no contiene un manifiesto válido de " +
                    "Asset Studio BB 3.0."
                );
            }

            IReadOnlyList<AssetStudioBBValidationMessage> validation =
                AssetStudioBBValidator.Validate(
                    manifestPath,
                    manifest
                );

            if (AssetStudioBBValidator.HasErrors(validation))
            {
                throw new InvalidOperationException(
                    "Asset Studio BB ha detectado errores. Corrígelos " +
                    "antes de generar las variantes visuales."
                );
            }

            return AssetStudioBBVariantGenerator.Generate(
                manifestPath,
                manifest
            );
        }

        private static PublicSeverity ConvertSeverity(
            AssetStudioBBSeverity severity
        )
        {
            switch (severity)
            {
                case AssetStudioBBSeverity.Error:
                    return PublicSeverity.Error;

                case AssetStudioBBSeverity.Warning:
                    return PublicSeverity.Warning;

                default:
                    return PublicSeverity.Information;
            }
        }
    }
}
