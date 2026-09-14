#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using BistroBuilder.UI.Iconography;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.UI.Iconography
{
    /// <summary>
    /// Instalador idempotente de la iconografía 21B.
    /// Descarga las fuentes SVG oficiales fijadas a Lucide 1.45.0, importa los sprites
    /// y reconstruye el catálogo canónico usado en runtime.
    /// </summary>
    public static class BBIconographyInstaller
    {
        private const string LucideVersion = "1.45.0";
        private const string RawBaseUrl = "https://raw.githubusercontent.com/lucide-icons/lucide/" + LucideVersion + "/icons/";
        private const string IconRoot = "Assets/BistroBuilder/UI/Iconography/Icons";
        private const string ResourceRoot = "Assets/Resources/BistroBuilder/UI";
        private const string CatalogPath = ResourceRoot + "/BBIconCatalog.asset";

        private readonly struct Spec
        {
            public readonly BBIconId Id;
            public readonly string Source;
            public readonly BBIconSemanticRole Role;

            public Spec(BBIconId id, string source, BBIconSemanticRole role = BBIconSemanticRole.Neutral)
            {
                Id = id;
                Source = source;
                Role = role;
            }
        }

        private static readonly Spec[] Specs =
        {
            // 01. Navegación principal
            new Spec(BBIconId.NavActivity, "layout-grid"),
            new Spec(BBIconId.NavStaff, "users"),
            new Spec(BBIconId.NavMenu, "notebook-tabs"),
            new Spec(BBIconId.NavInventory, "package-open"),
            new Spec(BBIconId.NavSuppliers, "truck"),
            new Spec(BBIconId.NavReservations, "calendar-days"),
            new Spec(BBIconId.NavEconomy, "coins"),
            new Spec(BBIconId.NavMarketing, "megaphone"),
            new Spec(BBIconId.NavReputation, "star"),
            new Spec(BBIconId.NavEditMode, "wrench"),
            new Spec(BBIconId.NavOptions, "settings"),

            // 03. Áreas y objetos
            new Spec(BBIconId.AreaDining, "utensils"),
            new Spec(BBIconId.AreaTerrace, "umbrella"),
            new Spec(BBIconId.AreaBar, "martini"),
            new Spec(BBIconId.AreaKitchen, "cooking-pot"),
            new Spec(BBIconId.AreaBathrooms, "toilet"),
            new Spec(BBIconId.AreaStorage, "warehouse"),
            new Spec(BBIconId.AreaEntrance, "door-open"),
            new Spec(BBIconId.AreaCashRegister, "receipt-text"),
            new Spec(BBIconId.AreaWaste, "trash-2"),
            new Spec(BBIconId.AreaCleaning, "sparkles"),
            new Spec(BBIconId.ObjectTable, "table-2"),
            new Spec(BBIconId.ObjectChair, "armchair"),
            new Spec(BBIconId.ObjectWaiter, "hand-platter"),
            new Spec(BBIconId.ObjectCook, "chef-hat"),
            new Spec(BBIconId.ObjectDish, "concierge-bell"),
            new Spec(BBIconId.ObjectIngredient, "leaf"),
            new Spec(BBIconId.ObjectDrink, "wine"),
            new Spec(BBIconId.ObjectEquipment, "microwave"),
            new Spec(BBIconId.ObjectDecoration, "flower-2"),
            new Spec(BBIconId.ObjectLighting, "lamp-desk"),

            // 04. Acciones
            new Spec(BBIconId.ActionAdd, "circle-plus", BBIconSemanticRole.Positive),
            new Spec(BBIconId.ActionEdit, "pencil"),
            new Spec(BBIconId.ActionDuplicate, "copy"),
            new Spec(BBIconId.ActionDelete, "trash-2", BBIconSemanticRole.Critical),
            new Spec(BBIconId.ActionSave, "save"),
            new Spec(BBIconId.ActionCancel, "circle-x", BBIconSemanticRole.Critical),
            new Spec(BBIconId.ActionMove, "move"),
            new Spec(BBIconId.ActionRotate, "rotate-cw"),
            new Spec(BBIconId.ActionConfirm, "circle-check", BBIconSemanticRole.Positive),
            new Spec(BBIconId.ActionPrioritize, "chevrons-up"),
            new Spec(BBIconId.ActionPause, "pause"),
            new Spec(BBIconId.ActionResume, "play"),

            // 05. Estados
            new Spec(BBIconId.StatusCorrect, "circle-check", BBIconSemanticRole.Positive),
            new Spec(BBIconId.StatusAttention, "circle-alert", BBIconSemanticRole.Warning),
            new Spec(BBIconId.StatusCritical, "triangle-alert", BBIconSemanticRole.Critical),
            new Spec(BBIconId.StatusInformation, "info", BBIconSemanticRole.Information),
            new Spec(BBIconId.StatusWaiting, "clock-3"),
            new Spec(BBIconId.StatusProcessing, "hourglass"),
            new Spec(BBIconId.StatusPaused, "circle-pause"),
            new Spec(BBIconId.StatusBlocked, "lock"),
            new Spec(BBIconId.StatusReserved, "calendar-check", BBIconSemanticRole.Positive),
            new Spec(BBIconId.StatusGroup, "users", BBIconSemanticRole.Information),

            // 06. Clientes y reservas
            new Spec(BBIconId.CustomerClient, "user"),
            new Spec(BBIconId.CustomerGroup, "users"),
            new Spec(BBIconId.CustomerChild, "baby"),
            new Spec(BBIconId.CustomerHighChair, "armchair"),
            new Spec(BBIconId.CustomerPet, "dog"),
            new Spec(BBIconId.CustomerReservation, "calendar-days"),
            new Spec(BBIconId.CustomerVip, "crown", BBIconSemanticRole.Accent),

            // 07. Comida y bebida
            new Spec(BBIconId.FoodStarter, "concierge-bell"),
            new Spec(BBIconId.FoodMain, "soup"),
            new Spec(BBIconId.FoodDessert, "cake-slice"),
            new Spec(BBIconId.FoodDrink, "wine"),
            new Spec(BBIconId.FoodCoffee, "coffee"),
            new Spec(BBIconId.FoodWine, "bottle-wine"),
            new Spec(BBIconId.FoodBeer, "beer"),
            new Spec(BBIconId.FoodVegetarian, "leaf", BBIconSemanticRole.Positive),
            new Spec(BBIconId.FoodVegan, "sprout"),
            new Spec(BBIconId.FoodGlutenFree, "wheat-off"),

            // 08. Economía
            new Spec(BBIconId.EconomyIncome, "coins", BBIconSemanticRole.Accent),
            new Spec(BBIconId.EconomyExpenses, "chart-no-axes-column-increasing"),
            new Spec(BBIconId.EconomyProfit, "trending-up"),
            new Spec(BBIconId.EconomyReport, "calculator"),
            new Spec(BBIconId.EconomyInvoice, "receipt-text"),

            // 09. Direccionales y generales
            new Spec(BBIconId.GeneralBack, "arrow-left"),
            new Spec(BBIconId.GeneralNext, "arrow-right"),
            new Spec(BBIconId.GeneralUp, "chevron-up"),
            new Spec(BBIconId.GeneralDown, "chevron-down"),
            new Spec(BBIconId.GeneralHelp, "circle-help"),
            new Spec(BBIconId.GeneralMore, "ellipsis-vertical"),

            // 10. Indicadores en escena
            new Spec(BBIconId.SceneTableServed, "utensils", BBIconSemanticRole.Positive),
            new Spec(BBIconId.SceneWaiting, "hourglass", BBIconSemanticRole.Warning),
            new Spec(BBIconId.SceneProblem, "circle-alert", BBIconSemanticRole.Critical),
            new Spec(BBIconId.SceneGroup, "users", BBIconSemanticRole.Information),
            new Spec(BBIconId.SceneReservation, "calendar-days"),
            new Spec(BBIconId.SceneCleaning, "sparkles")
        };

        [MenuItem("Bistro Builder/UI/Iconografía/Instalar o actualizar", priority = 2100)]
        public static async void InstallOrUpdate()
        {
            try
            {
                await InstallInternal();
                Debug.Log($"[BB Iconography 21B] PASS — {Specs.Length} usos canónicos registrados. Lucide {LucideVersion}.");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Bistro Builder — Iconografía", "La instalación no se completó. Revisa la consola para ver el error.", "Cerrar");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Bistro Builder/UI/Iconografía/Validar catálogo", priority = 2101)]
        public static void ValidateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BBIconCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[BB Iconography 21B] FAIL — no existe BBIconCatalog.asset. Ejecuta 'Instalar o actualizar'.");
                return;
            }

            var seen = new HashSet<BBIconId>();
            var missing = new List<string>();
            foreach (var entry in catalog.Entries)
            {
                if (!seen.Add(entry.id))
                    missing.Add($"Duplicado: {entry.id}");
                if (entry.sprite == null)
                    missing.Add($"Sin sprite: {entry.id} ({entry.sourceName})");
            }

            foreach (BBIconId id in Enum.GetValues(typeof(BBIconId)))
            {
                if (!seen.Contains(id))
                    missing.Add($"No registrado: {id}");
            }

            if (missing.Count == 0)
                Debug.Log($"[BB Iconography 21B] VALIDATION PASS — {catalog.Entries.Count} entradas, 0 errores.");
            else
                Debug.LogError("[BB Iconography 21B] VALIDATION FAIL\n - " + string.Join("\n - ", missing));
        }

        public static bool IsCatalogReady()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BBIconCatalog>(CatalogPath);
            if (catalog == null)
                return false;

            var seen = new HashSet<BBIconId>();
            foreach (var entry in catalog.Entries)
            {
                if (entry.sprite == null || !seen.Add(entry.id))
                    return false;
            }

            foreach (BBIconId id in Enum.GetValues(typeof(BBIconId)))
            {
                if (!seen.Contains(id))
                    return false;
            }

            return seen.Count == Enum.GetValues(typeof(BBIconId)).Length;
        }

        [MenuItem("Bistro Builder/UI/IconografÃ­a/Reconstruir catÃ¡logo local", priority = 2102)]
        public static void RebuildCatalogFromLocalOrThrow()
        {
            EnsureFolder(IconRoot);
            EnsureFolder(ResourceRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var definitions = new List<BBIconDefinition>(Specs.Length);            var unresolved = new List<string>();
            foreach (var spec in Specs)
            {
                var assetPath = $"{IconRoot}/{spec.Source}.svg";
                var sprite = LoadSprite(assetPath);
                if (sprite == null)
                    unresolved.Add($"{spec.Id} -> {assetPath}");
                definitions.Add(new BBIconDefinition(spec.Id, sprite, spec.Role, spec.Source));
            }

            if (unresolved.Count > 0)
                throw new InvalidOperationException(
                    "BB Iconography 21B: faltan sprites SVG importables.\n - " +
                    string.Join("\n - ", unresolved));

            var catalog = AssetDatabase.LoadAssetAtPath<BBIconCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BBIconCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.EditorSetEntries(definitions);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (!IsCatalogReady())
                throw new InvalidOperationException("BB Iconography 21B: catÃ¡logo incompleto tras reconstrucciÃ³n.");

            Debug.Log($"[BB Iconography 21B] LOCAL PASS â€” {definitions.Count} usos canÃ³nicos listos para build.");
        }

        public static void PrepareForBatch()
        {
            RebuildCatalogFromLocalOrThrow();
        }

        private static async Task InstallInternal()
        {
            EnsureFolder(IconRoot);
            EnsureFolder(ResourceRoot);

            var uniqueSources = new HashSet<string>(StringComparer.Ordinal);
            foreach (var spec in Specs)
                uniqueSources.Add(spec.Source);

            using (var http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromSeconds(20);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("BistroBuilder-Iconography/21B");

                var index = 0;
                foreach (var source in uniqueSources)
                {
                    index++;
                    EditorUtility.DisplayProgressBar(
                        "Bistro Builder — Iconografía",
                        $"Sincronizando {source}.svg ({index}/{uniqueSources.Count})",
                        index / (float)uniqueSources.Count);

                    var assetPath = $"{IconRoot}/{source}.svg";
                    var absolutePath = Path.GetFullPath(assetPath);
                    var svg = await http.GetStringAsync(RawBaseUrl + source + ".svg");
                    svg = NormalizeSvg(svg);

                    Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? IconRoot);
                    if (!File.Exists(absolutePath) || !string.Equals(File.ReadAllText(absolutePath), svg, StringComparison.Ordinal))
                        File.WriteAllText(absolutePath, svg);
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var definitions = new List<BBIconDefinition>(Specs.Length);
            var unresolved = new List<string>();
            foreach (var spec in Specs)
            {
                var assetPath = $"{IconRoot}/{spec.Source}.svg";
                var sprite = LoadSprite(assetPath);
                if (sprite == null)
                    unresolved.Add($"{spec.Id} -> {assetPath}");

                definitions.Add(new BBIconDefinition(spec.Id, sprite, spec.Role, spec.Source));
            }

            var catalog = AssetDatabase.LoadAssetAtPath<BBIconCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BBIconCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.EditorSetEntries(definitions);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (unresolved.Count > 0)
            {
                Debug.LogWarning(
                    "[BB Iconography 21B] SVG sincronizados, pero Unity no expuso Sprite para algunas fuentes. " +
                    "Comprueba el importador SVG/Vector Graphics.\n - " + string.Join("\n - ", unresolved));
            }
        }

        private static Sprite LoadSprite(string assetPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
                return sprite;

            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite candidate)
                    return candidate;
            }

            return null;
        }

        private static string NormalizeSvg(string svg)
        {
            // currentColor no siempre se resuelve de forma predecible en importadores Unity.
            // Blanco mantiene el asset neutro; BBIconButton aplica el color de estado en runtime.
            return svg.Replace("stroke=\"currentColor\"", "stroke=\"#FFFFFF\"")
                      .Replace("fill=\"currentColor\"", "fill=\"#FFFFFF\"");
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
