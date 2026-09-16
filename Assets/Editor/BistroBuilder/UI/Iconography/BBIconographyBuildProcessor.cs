#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BistroBuilder.Editor.UI.Iconography
{
    /// <summary>
    /// Garantiza que 21B no dependa de ejecutar un menú manual antes de generar build.
    /// Los SVG forman parte del repositorio; aquí solo se reconstruye/valida el catálogo.
    /// </summary>
    [InitializeOnLoad]
    public static class BBIconographyEditorBootstrap
    {
        static BBIconographyEditorBootstrap()
        {
            EditorApplication.delayCall += EnsureReadyAfterDomainReload;
        }

        private static void EnsureReadyAfterDomainReload()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            try
            {
                if (!BBIconographyInstaller.IsCatalogReady())
                    BBIconographyInstaller.RebuildCatalogFromLocalOrThrow();
            }
            catch (Exception ex)
            {
                Debug.LogError("[BB Iconography 21B] Preparación automática pendiente: " + ex.Message);
            }
        }
    }

    public sealed class BBIconographyBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -250;

        public void OnPreprocessBuild(BuildReport report)
        {
            BBIconographyInstaller.RebuildCatalogFromLocalOrThrow();
            if (!BBIconographyInstaller.IsCatalogReady())
            {
                throw new BuildFailedException(
                    "BB Iconography 21B no está lista: catálogo o sprites incompletos.");
            }

            Debug.Log(
                "[BB Iconography 21B] BUILD GATE PASS — catálogo completo y sprites resueltos.");
        }
    }
}
#endif
