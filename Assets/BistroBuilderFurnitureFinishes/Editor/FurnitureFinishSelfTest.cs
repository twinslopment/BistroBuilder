using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes.Editor
{
    internal static class FurnitureFinishSelfTest
    {
        private const string TestRoot = "Assets/__BBFFVAS_SelfTest";
        private const string PublishedTestRoot =
            "Assets/Generated/FurnitureFinishes/Published/bbffvas_selftest";
        private const string DraftTestRoot =
            "Assets/Generated/FurnitureFinishes/GeneratedDrafts/bbffvas_draft_selftest";
        private const string AutoDraftTestRoot =
            "Assets/Generated/FurnitureFinishes/AutoDrafts/bbffvas_draft_selftest";

        [MenuItem("Tools/Bistro Builder/Acabados y Variantes/Run Self-Test")]
        public static void RunFromMenu()
        {
            try
            {
                RunFromCommandLine();
                EditorUtility.DisplayDialog(
                    "BBFFVAS Self-Test",
                    "PASS · missing → propuesta → variante → publicación → aplicación runtime.",
                    "Cerrar");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "BBFFVAS Self-Test",
                    "FAIL · " + exception.Message,
                    "Cerrar");
            }
        }

        public static void RunFromCommandLine()
        {
            Cleanup();
            FurnitureFinishAssetUtility.EnsureFolder(TestRoot);

            try
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard");
                Assert(shader != null, "No hay shader Lit disponible para el self-test.");

                var donorMaterial = new Material(shader)
                {
                    name = "BBFFVAS_SelfTest_Wood"
                };
                AssetDatabase.CreateAsset(
                    donorMaterial,
                    TestRoot + "/DonorWood.mat");

                var finish = ScriptableObject.CreateInstance<FurnitureFinishDefinition>();
                finish.EditorConfigure(
                    "wood_test",
                    "Madera Test",
                    FurnitureSurfaceFamily.Wood,
                    donorMaterial,
                    FurnitureFinishChannel.None,
                    new[] { "selftest" });
                AssetDatabase.CreateAsset(
                    finish,
                    TestRoot + "/WoodFinish.asset");

                var library = ScriptableObject.CreateInstance<FurnitureFinishLibrary>();
                library.EditorConfigure(new[] { finish });
                AssetDatabase.CreateAsset(
                    library,
                    TestRoot + "/Library.asset");

                var sourceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sourceObject.name = "BBFFVAS_SelfTest_Source";
                foreach (var collider in sourceObject.GetComponentsInChildren<Collider>(true))
                    UnityEngine.Object.DestroyImmediate(collider);

                var sourceRenderer = sourceObject.GetComponent<Renderer>();
                sourceRenderer.sharedMaterials = new Material[] { null };
                var sourcePrefab = PrefabUtility.SaveAsPrefabAsset(
                    sourceObject,
                    TestRoot + "/Source.prefab");
                UnityEngine.Object.DestroyImmediate(sourceObject);
                Assert(sourcePrefab != null, "No se pudo crear el prefab de prueba.");

                var profile = ScriptableObject.CreateInstance<FurnitureFinishProfile>();
                var zone = new FurnitureFinishProfile.ZoneDefinition(
                    "surface",
                    "Superficie",
                    FurnitureSurfaceFamily.Wood,
                    true,
                    FurnitureFinishChannel.None,
                    new[]
                    {
                        new FurnitureFinishProfile.SlotBinding(string.Empty, 0)
                    });
                profile.EditorConfigure(
                    "bbffvas_selftest",
                    "BBFFVAS Self-Test",
                    sourcePrefab,
                    new[] { zone },
                    Array.Empty<FurnitureFinishProfile.VariantDefinition>(),
                    string.Empty);
                AssetDatabase.CreateAsset(
                    profile,
                    TestRoot + "/Profile.asset");
                AssetDatabase.SaveAssets();

                var issues = FurnitureFinishAnalyzer.Analyze(profile);
                Assert(
                    ContainsIssue(issues, FurnitureFinishIssueKind.MissingMaterial),
                    "El analizador no detectó el material missing.");

                var proposals = FurnitureFinishAutoResolver.Propose(
                    profile,
                    library,
                    issues);
                Assert(proposals.Count == 1, "El resolver no produjo exactamente una propuesta.");
                Assert(
                    proposals[0].Mode == FurnitureFinishProposalMode.ReplaceMissingMaterial,
                    "La propuesta no es de reemplazo de zona missing.");
                Assert(
                    proposals[0].SuggestedFinish == finish,
                    "La propuesta no reutilizó el acabado de biblioteca.");

                var variant = FurnitureFinishAutoResolver.ApplyToVariant(
                    profile,
                    proposals,
                    "auto_finish",
                    "Auto Finish");
                Assert(variant.FindFinish("surface") == finish, "La variante no recibió el acabado.");

                var validation = FurnitureFinishValidator.ValidateForPublish(profile);
                Assert(
                    !FurnitureFinishValidator.HasErrors(validation),
                    "La variante válida quedó bloqueada por el validador.");

                var published = FurnitureFinishPublisher.Publish(profile);
                Assert(published != null, "No se creó el conjunto publicado.");
                Assert(published.Variants.Count == 1, "El conjunto publicado no contiene una variante.");
                Assert(
                    published.Variants[0].Bindings.Count == 1,
                    "La variante publicada no contiene el mapa completo de slots.");

                var sourceAfterPublish = sourcePrefab.GetComponent<Renderer>();
                Assert(
                    sourceAfterPublish.sharedMaterials[0] == null,
                    "La publicación modificó el asset fuente.");

                var runtimeInstance = UnityEngine.Object.Instantiate(sourcePrefab);
                try
                {
                    var applied = FurnitureFinishRuntimeApplicator.Apply(
                        runtimeInstance,
                        published,
                        "auto_finish");
                    Assert(applied, "El aplicador runtime devolvió false.");
                    Assert(
                        runtimeInstance.GetComponent<Renderer>().sharedMaterials[0] == donorMaterial,
                        "El aplicador runtime no asignó el acabado publicado.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(runtimeInstance);
                }

                RunDraftFallbackTest(shader);
                RunMultiFamilyTest(shader);
                RunRuntimeBindingTest(published, sourcePrefab, donorMaterial);

                Debug.Log("[BBFFVAS] SELF-TEST PASS · core + draft fallback + multi-family + runtime binding");
            }
            finally
            {
                Cleanup();
            }
        }

        private static void RunDraftFallbackTest(Shader shader)
        {
            var sourceMaterial = new Material(shader)
            {
                name = "BBFFVAS_Draft_Source"
            };
            AssetDatabase.CreateAsset(
                sourceMaterial,
                TestRoot + "/DraftSource.mat");

            var sourceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sourceObject.name = "BBFFVAS_Draft_Source";
            foreach (var collider in sourceObject.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            sourceObject.GetComponent<Renderer>().sharedMaterial = sourceMaterial;

            var sourcePrefab = PrefabUtility.SaveAsPrefabAsset(
                sourceObject,
                TestRoot + "/DraftSource.prefab");
            UnityEngine.Object.DestroyImmediate(sourceObject);

            var library = ScriptableObject.CreateInstance<FurnitureFinishLibrary>();
            library.EditorConfigure(Array.Empty<FurnitureFinishDefinition>());
            AssetDatabase.CreateAsset(
                library,
                TestRoot + "/DraftLibrary.asset");

            var profile = ScriptableObject.CreateInstance<FurnitureFinishProfile>();
            var zone = new FurnitureFinishProfile.ZoneDefinition(
                "upholstery",
                "Tapizado",
                FurnitureSurfaceFamily.Fabric,
                true,
                FurnitureFinishChannel.NormalMap,
                new[]
                {
                    new FurnitureFinishProfile.SlotBinding(string.Empty, 0)
                });
            profile.EditorConfigure(
                "bbffvas_draft_selftest",
                "Draft fallback self-test",
                sourcePrefab,
                new[] { zone },
                Array.Empty<FurnitureFinishProfile.VariantDefinition>(),
                string.Empty);
            AssetDatabase.CreateAsset(
                profile,
                TestRoot + "/DraftProfile.asset");
            AssetDatabase.SaveAssets();

            var issues = FurnitureFinishAnalyzer.Analyze(profile);
            Assert(
                ContainsIssue(issues, FurnitureFinishIssueKind.MissingChannel),
                "El fallback no detectó el Normal missing.");

            var initial = FurnitureFinishAutoResolver.Propose(
                profile,
                library,
                issues);
            Assert(
                initial.Count == 0,
                "El resolver encontró un acabado reutilizable en una biblioteca vacía.");

            var generated = FurnitureFinishDraftGenerator.EnsureCandidatesForUnresolved(
                profile,
                library,
                issues,
                initial);
            Assert(generated == 1, "No se generó exactamente un borrador técnico.");

            var proposals = FurnitureFinishAutoResolver.Propose(
                profile,
                library,
                issues);
            Assert(proposals.Count == 1, "El borrador técnico no se convirtió en propuesta.");
            Assert(
                proposals[0].Mode == FurnitureFinishProposalMode.CompleteMissingChannels,
                "El fallback no preserva el material existente.");
            Assert(
                proposals[0].SuggestedFinish.Family == FurnitureSurfaceFamily.Fabric,
                "El fallback generó una familia incorrecta.");

            var variant = FurnitureFinishAutoResolver.ApplyToVariant(
                profile,
                proposals,
                "draft_auto",
                "Draft Auto");
            var composed = variant.FindFinish("upholstery");
            Assert(composed != null && composed.Material != null, "No se creó material compuesto.");
            Assert(
                FurnitureFinishAssetUtility.GetTexture(
                    composed.Material,
                    FurnitureFinishChannel.NormalMap) != null,
                "El material compuesto sigue sin Normal.");
            Assert(
                FurnitureFinishAssetUtility.GetTexture(
                    sourceMaterial,
                    FurnitureFinishChannel.NormalMap) == null,
                "Acabado Automático modificó el material fuente.");

            var validation = FurnitureFinishValidator.ValidateForPublish(profile);
            Assert(
                !FurnitureFinishValidator.HasErrors(validation),
                "El fallback técnico válido no supera publicación.");
        }

        private static void RunMultiFamilyTest(Shader shader)
        {
            var material = new Material(shader)
            {
                name = "BBFFVAS_MultiFamily_Paint"
            };
            var finish = ScriptableObject.CreateInstance<FurnitureFinishDefinition>();
            finish.EditorConfigure(
                "paint_test",
                "Pintura Test",
                FurnitureSurfaceFamily.Paint,
                material,
                FurnitureFinishChannel.None,
                new[] { "selftest" });

            var sourceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sourceObject.name = "BBFFVAS_MultiFamily_Source";
            foreach (var collider in sourceObject.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            var sourcePrefab = PrefabUtility.SaveAsPrefabAsset(
                sourceObject,
                TestRoot + "/MultiFamilySource.prefab");
            UnityEngine.Object.DestroyImmediate(sourceObject);

            var zone = new FurnitureFinishProfile.ZoneDefinition(
                "surface",
                "Superficie",
                FurnitureSurfaceFamily.Wood,
                new[]
                {
                    FurnitureSurfaceFamily.Wood,
                    FurnitureSurfaceFamily.Paint
                },
                true,
                FurnitureFinishChannel.None,
                new[]
                {
                    new FurnitureFinishProfile.SlotBinding(string.Empty, 0)
                });
            Assert(zone.Allows(FurnitureSurfaceFamily.Paint), "Zona multi-familia rechaza Paint.");
            Assert(!zone.Allows(FurnitureSurfaceFamily.Glass), "Zona multi-familia acepta Glass.");

            var variant = new FurnitureFinishProfile.VariantDefinition(
                "painted",
                "Pintado",
                new[]
                {
                    new FurnitureFinishProfile.ZoneFinishBinding("surface", finish)
                });
            var profile = ScriptableObject.CreateInstance<FurnitureFinishProfile>();
            profile.EditorConfigure(
                "bbffvas_multifamily_selftest",
                "Multi-family self-test",
                sourcePrefab,
                new[] { zone },
                new[] { variant },
                "painted");

            var validation = FurnitureFinishValidator.ValidateForPublish(profile);
            Assert(
                !FurnitureFinishValidator.HasErrors(validation),
                "El validador rechazó una familia compatible secundaria.");

            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(finish);
            UnityEngine.Object.DestroyImmediate(material);
        }

        private static void RunRuntimeBindingTest(
            FurnitureFinishPublishedSet published,
            GameObject sourcePrefab,
            Material expectedMaterial)
        {
            var instance = UnityEngine.Object.Instantiate(sourcePrefab);
            try
            {
                var binding = instance.AddComponent<FurnitureFinishRuntimeBinding>();
                binding.EditorConfigure(published, "auto_finish", false);
                Assert(binding.ApplyCurrent(), "Runtime Binding no pudo aplicar la variante.");
                Assert(
                    instance.GetComponent<Renderer>().sharedMaterials[0] == expectedMaterial,
                    "Runtime Binding aplicó un material incorrecto.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static bool ContainsIssue(
            System.Collections.Generic.IReadOnlyList<FurnitureFinishIssue> issues,
            FurnitureFinishIssueKind kind)
        {
            foreach (var issue in issues)
            {
                if (issue.Kind == kind)
                    return true;
            }
            return false;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void Cleanup()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.DeleteAsset(PublishedTestRoot);
            AssetDatabase.DeleteAsset(DraftTestRoot);
            AssetDatabase.DeleteAsset(AutoDraftTestRoot);
            AssetDatabase.Refresh();
        }
    }
}