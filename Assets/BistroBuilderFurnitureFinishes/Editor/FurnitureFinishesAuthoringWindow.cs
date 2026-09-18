using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes.Editor
{
    internal sealed class FurnitureFinishesAuthoringWindow : EditorWindow
    {
        private FurnitureFinishProfile profile;
        private FurnitureFinishLibrary library;
        private IReadOnlyList<FurnitureFinishIssue> issues = Array.Empty<FurnitureFinishIssue>();
        private IReadOnlyList<FurnitureFinishProposal> proposals = Array.Empty<FurnitureFinishProposal>();
        private IReadOnlyList<FurnitureFinishValidationMessage> validation =
            Array.Empty<FurnitureFinishValidationMessage>();

        private readonly FurnitureFinishPreviewRenderer preview =
            new FurnitureFinishPreviewRenderer();

        private Vector2 scroll;
        private string previewVariantId = string.Empty;
        private string autoVariantId = "auto_finish";
        private string autoVariantName = "Acabado automático";
        private Material newFinishMaterial;
        private string newFinishId = string.Empty;
        private string newFinishName = string.Empty;
        private FurnitureSurfaceFamily newFinishFamily = FurnitureSurfaceFamily.Wood;
        private string manualVariantId = "variant_01";
        private string manualVariantName = "Nueva variante";
        private int quickZoneIndex;
        private int quickFinishIndex;
        private bool showZoneEditor;
        private bool showAdvancedVariants;

        private static readonly FurnitureSurfaceFamily[] QuickFamilies =
        {
            FurnitureSurfaceFamily.Wood,
            FurnitureSurfaceFamily.Fabric,
            FurnitureSurfaceFamily.Leather,
            FurnitureSurfaceFamily.Metal,
            FurnitureSurfaceFamily.Stone,
            FurnitureSurfaceFamily.Glass,
            FurnitureSurfaceFamily.Paint,
            FurnitureSurfaceFamily.Plastic,
            FurnitureSurfaceFamily.Ceramic,
            FurnitureSurfaceFamily.Other
        };

        [MenuItem("Tools/Bistro Builder/Acabados y Variantes de Mobiliario")]
        private static void Open()
        {
            var window = GetWindow<FurnitureFinishesAuthoringWindow>();
            window.titleContent = new GUIContent("Acabados y Variantes");
            window.minSize = new Vector2(820f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            library = FurnitureFinishProfileFactory.LoadOrCreateDefaultLibrary();
            if (Selection.activeObject is FurnitureFinishProfile selectedProfile)
                SetProfile(selectedProfile);
        }

        private void OnDisable() => preview.Dispose();

        private void OnSelectionChange()
        {
            if (Selection.activeObject is FurnitureFinishProfile selectedProfile)
                SetProfile(selectedProfile);
            Repaint();
        }

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(6f);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSourceAndProfile();
            if (profile != null)
            {
                DrawPreview();
                DrawZoneConfiguration();
                DrawDiagnostics();
                DrawAutomaticFinish();
                DrawVariantsAndPublish();
            }
            DrawLibrary();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField(
                "Sistema de Acabados y Variantes de Mobiliario · Autoría",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Herramienta interna. Las automatizaciones solo completan zonas/canales missing; " +
                "no sustituyen trabajo válido sin una acción explícita.",
                MessageType.Info);
        }

        private void DrawSourceAndProfile()
        {
            EditorGUILayout.LabelField("Mueble", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var nextProfile = (FurnitureFinishProfile)EditorGUILayout.ObjectField(
                "Perfil",
                profile,
                typeof(FurnitureFinishProfile),
                false);
            if (EditorGUI.EndChangeCheck())
                SetProfile(nextProfile);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!(Selection.activeObject is GameObject)))
            {
                if (GUILayout.Button("Crear perfil desde selección", GUILayout.Height(28f)))
                {
                    try
                    {
                        Undo.IncrementCurrentGroup();
                        SetProfile(FurnitureFinishProfileFactory.CreateFromSelection(Selection.activeObject));
                        RefreshAnalysis();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        EditorUtility.DisplayDialog("Acabados y Variantes", exception.Message, "Cerrar");
                    }
                }
            }

            using (new EditorGUI.DisabledScope(
                !FurnitureFinishExistingPipelineImporter.CanImport(Selection.activeObject)))
            {
                if (GUILayout.Button("Importar Variant Set existente", GUILayout.Height(28f)))
                {
                    try
                    {
                        library = library ?? FurnitureFinishProfileFactory.LoadOrCreateDefaultLibrary();
                        SetProfile(FurnitureFinishExistingPipelineImporter.Import(
                            Selection.activeObject,
                            library));
                        RefreshAnalysis();
                        ShowNotification(new GUIContent("Variantes importadas"));
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        EditorUtility.DisplayDialog("Importar variantes", exception.Message, "Cerrar");
                    }
                }
            }

            using (new EditorGUI.DisabledScope(profile == null))
            {
                if (GUILayout.Button("Localizar perfil", GUILayout.Height(28f)))
                {
                    Selection.activeObject = profile;
                    EditorGUIUtility.PingObject(profile);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (profile != null)
            {
                EditorGUILayout.LabelField("ID", profile.FurnitureId);
                EditorGUILayout.LabelField("Fuente", profile.SourceAsset != null ? profile.SourceAsset.name : "Missing");
                EditorGUILayout.LabelField("Zonas", profile.Zones.Count.ToString());
                EditorGUILayout.LabelField("Variantes", profile.Variants.Count.ToString());
            }
        }

        private void DrawPreview()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Previsualización 3D", EditorStyles.boldLabel);

            var options = BuildVariantOptions(out var ids);
            var selected = Array.IndexOf(ids, previewVariantId);
            if (selected < 0)
                selected = 0;
            var next = EditorGUILayout.Popup("Vista", selected, options);
            previewVariantId = ids[Mathf.Clamp(next, 0, ids.Length - 1)];

            var rect = GUILayoutUtility.GetRect(
                100f,
                280f,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(280f));
            preview.Set(profile, previewVariantId);
            preview.Draw(rect);

            if (GUILayout.Button("Reset cámara", GUILayout.Width(110f)))
                preview.ResetCamera();
        }

        private void DrawZoneConfiguration()
        {
            EditorGUILayout.Space(8f);
            showZoneEditor = EditorGUILayout.Foldout(
                showZoneEditor,
                "Zonas semánticas y requisitos",
                true);
            if (!showZoneEditor)
                return;

            EditorGUILayout.HelpBox(
                "Las zonas desconocidas deben clasificarse antes de usar Acabado Automático. " +
                "Required Channels permite exigir mapas concretos sin asumirlos para todos los materiales.",
                MessageType.None);

            var serialized = new SerializedObject(profile);
            serialized.Update();
            EditorGUILayout.PropertyField(serialized.FindProperty("zones"), true);
            if (serialized.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(profile);
                RefreshAnalysis();
            }
        }

        private void DrawDiagnostics()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Diagnóstico de acabados", EditorStyles.boldLabel);
            if (GUILayout.Button("Analizar", GUILayout.Width(90f)))
                RefreshAnalysis();
            EditorGUILayout.EndHorizontal();

            if (DrawUnclassifiedSurfaceActions())
                return;

            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No se han detectado incidencias en las zonas configuradas.",
                    MessageType.Info);
                return;
            }

            foreach (var issue in issues)
            {
                EditorGUILayout.HelpBox(
                    issue.Message,
                    issue.Severity == FurnitureFinishIssueSeverity.Error
                        ? MessageType.Error
                        : issue.Severity == FurnitureFinishIssueSeverity.Warning
                            ? MessageType.Warning
                            : MessageType.Info);
            }
        }

        private bool DrawUnclassifiedSurfaceActions()
        {
            foreach (var zone in profile.Zones)
            {
                if (zone == null || (zone.ClassificationConfirmed && HasCompatibleFamily(zone)))
                    continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(
                    $"Clasificar · {zone.DisplayName}",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "El sistema no aplicará Acabado Automático hasta que confirmes el tipo de superficie.",
                    EditorStyles.wordWrappedMiniLabel);

                for (var start = 0; start < QuickFamilies.Length; start += 5)
                {
                    EditorGUILayout.BeginHorizontal();
                    var end = Mathf.Min(start + 5, QuickFamilies.Length);
                    for (var index = start; index < end; index++)
                    {
                        var family = QuickFamilies[index];
                        if (GUILayout.Button(FamilyLabel(family)))
                        {
                            Undo.RecordObject(profile, "Clasificar superficie");
                            zone.EditorClassify(family, new[] { family });
                            EditorUtility.SetDirty(profile);
                            AssetDatabase.SaveAssets();
                            RefreshAnalysis();
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.EndVertical();
                            return true;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }

            return false;
        }

        private static bool HasCompatibleFamily(
            FurnitureFinishProfile.ZoneDefinition zone)
        {
            foreach (var family in zone.CompatibleFamilies)
            {
                if (family != FurnitureSurfaceFamily.Unknown)
                    return true;
            }
            return zone.Family != FurnitureSurfaceFamily.Unknown;
        }

        private static string FamilyLabel(FurnitureSurfaceFamily family)
        {
            switch (family)
            {
                case FurnitureSurfaceFamily.Wood: return "Madera";
                case FurnitureSurfaceFamily.Fabric: return "Tela";
                case FurnitureSurfaceFamily.Leather: return "Cuero";
                case FurnitureSurfaceFamily.Metal: return "Metal";
                case FurnitureSurfaceFamily.Stone: return "Piedra";
                case FurnitureSurfaceFamily.Glass: return "Vidrio";
                case FurnitureSurfaceFamily.Paint: return "Pintura";
                case FurnitureSurfaceFamily.Plastic: return "Plástico";
                case FurnitureSurfaceFamily.Ceramic: return "Cerámica";
                default: return "Otro";
            }
        }

        private void DrawAutomaticFinish()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Acabado Automático", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            autoVariantId = EditorGUILayout.TextField("Variant ID", autoVariantId);
            autoVariantName = EditorGUILayout.TextField("Nombre", autoVariantName);
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(profile == null || library == null))
            {
                if (GUILayout.Button(
                        $"Acabado Automático · resolver solo missing ({CountActionableIssues()})",
                        GUILayout.Height(34f)))
                {
                    RefreshAnalysis();
                    proposals = FurnitureFinishAutoResolver.Propose(profile, library, issues);
                    var generated = FurnitureFinishDraftGenerator.EnsureCandidatesForUnresolved(
                        profile,
                        library,
                        issues,
                        proposals);
                    if (generated > 0)
                    {
                        proposals = FurnitureFinishAutoResolver.Propose(
                            profile,
                            library,
                            issues);
                        ShowNotification(new GUIContent(
                            $"Borradores técnicos generados: {generated}"));
                    }
                }
            }

            if (proposals.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Sin propuestas automáticas aplicables. Si existe una zona sin clasificar, " +
                    "indica primero su tipo de superficie.",
                    MessageType.None);
                return;
            }

            foreach (var proposal in proposals)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(
                    $"{proposal.ZoneId} → {proposal.SuggestedFinish.DisplayName}",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    proposal.Mode == FurnitureFinishProposalMode.ReplaceMissingMaterial
                        ? "Completar zona sin acabado"
                        : $"Completar únicamente: {proposal.MissingChannels}");
                EditorGUILayout.LabelField(proposal.Reason, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Aplicar propuestas a variante borrador", GUILayout.Height(32f)))
            {
                try
                {
                    Undo.RecordObject(profile, "Aplicar Acabado Automático");
                    var variant = FurnitureFinishAutoResolver.ApplyToVariant(
                        profile,
                        proposals,
                        autoVariantId,
                        autoVariantName);
                    previewVariantId = variant.Id;
                    proposals = Array.Empty<FurnitureFinishProposal>();
                    RefreshAnalysis();
                    preview.Set(profile, previewVariantId);
                    preview.Refresh();
                    ShowNotification(new GUIContent("Acabado automático aplicado al borrador"));
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog("Acabado Automático", exception.Message, "Cerrar");
                }
            }
        }

        private void DrawVariantsAndPublish()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Variantes y publicación", EditorStyles.boldLabel);

            manualVariantId = EditorGUILayout.TextField("Nuevo Variant ID", manualVariantId);
            manualVariantName = EditorGUILayout.TextField("Nombre variante", manualVariantName);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Nueva variante"))
                CreateManualVariant(false);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(previewVariantId)))
            {
                if (GUILayout.Button("Duplicar variante visible"))
                    CreateManualVariant(true);
            }
            EditorGUILayout.EndHorizontal();

            DrawQuickBindingEditor();

            showAdvancedVariants = EditorGUILayout.Foldout(
                showAdvancedVariants,
                "Edición avanzada de variantes",
                true);
            if (showAdvancedVariants)
            {
                var serialized = new SerializedObject(profile);
                serialized.Update();
                EditorGUILayout.PropertyField(serialized.FindProperty("defaultVariantId"));
                EditorGUILayout.PropertyField(serialized.FindProperty("variants"), true);
                if (serialized.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(profile);
                    RefreshAnalysis();
                    preview.Refresh();
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generar miniaturas"))
            {
                try
                {
                    var count = FurnitureFinishThumbnailGenerator.GenerateAll(profile);
                    ShowNotification(new GUIContent(
                        $"Miniaturas generadas: {count}"));
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog(
                        "Miniaturas",
                        exception.Message,
                        "Cerrar");
                }
            }

            if (GUILayout.Button("Validar publicación"))
                validation = FurnitureFinishValidator.ValidateForPublish(profile);
            EditorGUILayout.EndHorizontal();

            foreach (var message in validation)
            {
                var type = message.Severity == FurnitureFinishValidationSeverity.Error
                    ? MessageType.Error
                    : message.Severity == FurnitureFinishValidationSeverity.Warning
                        ? MessageType.Warning
                        : MessageType.Info;
                EditorGUILayout.HelpBox(message.Text, type);
            }

            var blocked = validation.Count == 0 || FurnitureFinishValidator.HasErrors(validation);
            using (new EditorGUI.DisabledScope(blocked))
            {
                if (GUILayout.Button("Publicar conjunto de variantes", GUILayout.Height(34f)))
                {
                    try
                    {
                        var published = FurnitureFinishPublisher.Publish(profile);
                        Selection.activeObject = published;
                        EditorGUIUtility.PingObject(published);
                        ShowNotification(new GUIContent("Variantes publicadas"));
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        EditorUtility.DisplayDialog("Publicación", exception.Message, "Cerrar");
                    }
                }
            }
        }

        private void CreateManualVariant(bool duplicateVisible)
        {
            var id = FurnitureFinishAssetUtility.StableId(manualVariantId, "variant");
            if (profile.FindVariant(id) != null)
            {
                EditorUtility.DisplayDialog(
                    "Variantes",
                    $"Ya existe la variante '{id}'.",
                    "Cerrar");
                return;
            }

            var bindings = new List<FurnitureFinishProfile.ZoneFinishBinding>();
            if (duplicateVisible)
            {
                var source = profile.FindVariant(previewVariantId);
                if (source != null)
                {
                    foreach (var binding in source.Bindings)
                    {
                        if (binding != null)
                        {
                            bindings.Add(new FurnitureFinishProfile.ZoneFinishBinding(
                                binding.ZoneId,
                                binding.Finish));
                        }
                    }
                }
            }

            var created = new FurnitureFinishProfile.VariantDefinition(
                id,
                string.IsNullOrWhiteSpace(manualVariantName) ? id : manualVariantName,
                bindings.ToArray());

            Undo.RecordObject(profile, duplicateVisible ? "Duplicar variante" : "Crear variante");
            var variants = new List<FurnitureFinishProfile.VariantDefinition>(profile.Variants);
            variants.Add(created);
            var defaultId = string.IsNullOrWhiteSpace(profile.DefaultVariantId)
                ? id
                : profile.DefaultVariantId;
            profile.EditorSetVariants(variants.ToArray(), defaultId);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            previewVariantId = id;
            manualVariantId = "variant_" + (profile.Variants.Count + 1).ToString("00");
            RefreshAnalysis();
            preview.Set(profile, previewVariantId);
        }

        private void DrawQuickBindingEditor()
        {
            if (library == null || profile.Variants.Count == 0 || profile.Zones.Count == 0)
                return;

            var variant = profile.FindVariant(previewVariantId);
            if (variant == null)
                variant = profile.FindVariant(profile.DefaultVariantId);
            if (variant == null)
                variant = profile.Variants[0];
            if (variant == null)
                return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Asignación rápida", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Variante activa", variant.DisplayName);

            quickZoneIndex = Mathf.Clamp(quickZoneIndex, 0, profile.Zones.Count - 1);
            var zoneLabels = new string[profile.Zones.Count];
            for (var index = 0; index < profile.Zones.Count; index++)
            {
                var zone = profile.Zones[index];
                zoneLabels[index] = zone != null
                    ? $"{zone.DisplayName} · {zone.Family}"
                    : "<zona nula>";
            }
            quickZoneIndex = EditorGUILayout.Popup("Zona", quickZoneIndex, zoneLabels);

            var selectedZone = profile.Zones[quickZoneIndex];
            if (selectedZone == null)
                return;

            var candidates = new List<FurnitureFinishDefinition>();
            foreach (var finish in library.Finishes)
            {
                if (finish != null
                    && finish.Material != null
                    && selectedZone.Allows(finish.Family))
                    candidates.Add(finish);
            }

            if (candidates.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    !selectedZone.ClassificationConfirmed
                        ? "Clasifica la superficie para filtrar acabados compatibles."
                        : "No hay acabados compatibles con esta zona en la biblioteca.",
                    MessageType.Warning);
                return;
            }

            quickFinishIndex = Mathf.Clamp(quickFinishIndex, 0, candidates.Count - 1);
            var finishLabels = new string[candidates.Count];
            for (var index = 0; index < candidates.Count; index++)
                finishLabels[index] = candidates[index].DisplayName;
            quickFinishIndex = EditorGUILayout.Popup("Acabado", quickFinishIndex, finishLabels);

            if (GUILayout.Button("Asignar acabado a esta zona"))
            {
                AssignFinishToVariant(variant, selectedZone.Id, candidates[quickFinishIndex]);
                previewVariantId = variant.Id;
                preview.Set(profile, previewVariantId);
            }
        }

        private void AssignFinishToVariant(
            FurnitureFinishProfile.VariantDefinition sourceVariant,
            string zoneId,
            FurnitureFinishDefinition finish)
        {
            var bindings = new List<FurnitureFinishProfile.ZoneFinishBinding>();
            var replaced = false;
            foreach (var binding in sourceVariant.Bindings)
            {
                if (binding == null)
                    continue;
                if (string.Equals(binding.ZoneId, zoneId, StringComparison.Ordinal))
                {
                    bindings.Add(new FurnitureFinishProfile.ZoneFinishBinding(zoneId, finish));
                    replaced = true;
                }
                else
                {
                    bindings.Add(new FurnitureFinishProfile.ZoneFinishBinding(
                        binding.ZoneId,
                        binding.Finish));
                }
            }
            if (!replaced)
                bindings.Add(new FurnitureFinishProfile.ZoneFinishBinding(zoneId, finish));

            var updated = new FurnitureFinishProfile.VariantDefinition(
                sourceVariant.Id,
                sourceVariant.DisplayName,
                bindings.ToArray());

            Undo.RecordObject(profile, "Asignar acabado a zona");
            var variants = new List<FurnitureFinishProfile.VariantDefinition>();
            foreach (var variant in profile.Variants)
            {
                variants.Add(
                    variant != null && string.Equals(variant.Id, sourceVariant.Id, StringComparison.Ordinal)
                        ? updated
                        : variant);
            }
            profile.EditorSetVariants(variants.ToArray(), profile.DefaultVariantId);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            RefreshAnalysis();
            preview.Refresh();
        }

        private void DrawLibrary()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Biblioteca de acabados", EditorStyles.boldLabel);
            library = (FurnitureFinishLibrary)EditorGUILayout.ObjectField(
                "Biblioteca",
                library,
                typeof(FurnitureFinishLibrary),
                false);
            if (library == null)
            {
                if (GUILayout.Button("Crear/cargar biblioteca predeterminada"))
                    library = FurnitureFinishProfileFactory.LoadOrCreateDefaultLibrary();
                return;
            }

            EditorGUILayout.LabelField("Acabados registrados", library.Finishes.Count.ToString());
            newFinishMaterial = (Material)EditorGUILayout.ObjectField(
                "Material",
                newFinishMaterial,
                typeof(Material),
                false);
            newFinishFamily = (FurnitureSurfaceFamily)EditorGUILayout.EnumPopup(
                "Familia",
                newFinishFamily);
            newFinishId = EditorGUILayout.TextField("Finish ID", newFinishId);
            newFinishName = EditorGUILayout.TextField("Nombre", newFinishName);

            using (new EditorGUI.DisabledScope(newFinishMaterial == null))
            {
                if (GUILayout.Button("Añadir material como acabado reutilizable"))
                {
                    try
                    {
                        Undo.RecordObject(library, "Añadir acabado a biblioteca");
                        var definition = FurnitureFinishProfileFactory.AddFinish(
                            library,
                            newFinishMaterial,
                            newFinishId,
                            newFinishName,
                            newFinishFamily);
                        Selection.activeObject = definition;
                        newFinishMaterial = null;
                        newFinishId = string.Empty;
                        newFinishName = string.Empty;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        EditorUtility.DisplayDialog("Biblioteca de acabados", exception.Message, "Cerrar");
                    }
                }
            }
        }

        private void SetProfile(FurnitureFinishProfile value)
        {
            profile = value;
            previewVariantId = profile != null ? profile.DefaultVariantId : string.Empty;
            issues = Array.Empty<FurnitureFinishIssue>();
            proposals = Array.Empty<FurnitureFinishProposal>();
            validation = Array.Empty<FurnitureFinishValidationMessage>();
            preview.Set(profile, previewVariantId);
            if (profile != null)
                RefreshAnalysis();
        }

        private void RefreshAnalysis()
        {
            issues = profile != null
                ? FurnitureFinishAnalyzer.Analyze(profile)
                : Array.Empty<FurnitureFinishIssue>();
            validation = profile != null
                ? FurnitureFinishValidator.ValidateForPublish(profile)
                : Array.Empty<FurnitureFinishValidationMessage>();
            Repaint();
        }

        private int CountActionableIssues()
        {
            var count = 0;
            foreach (var issue in issues)
            {
                if (issue.Kind == FurnitureFinishIssueKind.MissingMaterial
                    || issue.Kind == FurnitureFinishIssueKind.BrokenShader
                    || issue.Kind == FurnitureFinishIssueKind.PlaceholderMaterial
                    || issue.Kind == FurnitureFinishIssueKind.MissingChannel)
                    count++;
            }
            return count;
        }

        private string[] BuildVariantOptions(out string[] ids)
        {
            var labels = new List<string> { "Original" };
            var values = new List<string> { string.Empty };
            if (profile != null)
            {
                foreach (var variant in profile.Variants)
                {
                    if (variant == null)
                        continue;
                    labels.Add(string.IsNullOrWhiteSpace(variant.DisplayName) ? variant.Id : variant.DisplayName);
                    values.Add(variant.Id);
                }
            }
            ids = values.ToArray();
            return labels.ToArray();
        }
    }
}