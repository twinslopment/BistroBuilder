using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderDishImageCatalogImporter
{
    public const string ImageFolder = "Assets/Art/UI/Dishes";
    public const string CatalogAssetPath =
        "Assets/Resources/BistroBuilder/Menu/DishImageCatalog.asset";
    public const string ManifestAssetPath =
        ImageFolder + "/dishes_manifest.csv";

    private static readonly HashSet<string> SupportedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".webp"
        };

    private static readonly Dictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "gazpacho", "gazpacho_andaluz" },
            { "pantumaca", "pan_con_tomate" },
            { "pan_tumaca", "pan_con_tomate" },
            { "pancontomate", "pan_con_tomate" },
            { "tablaembutidos", "tabla_embutidos_ibericos" },
            { "tabla_embutidos", "tabla_embutidos_ibericos" },
            { "patatasbravas", "patatas_bravas" },
            { "pimientospadron", "pimientos_padron" },
            { "pimientosdepadron", "pimientos_padron" },
            { "tortillapatatas", "tortilla_patatas" },
            { "tortilladepatatas", "tortilla_patatas" },
            { "ensaladillarusa", "ensaladilla_rusa" },
            { "aceitunasalinadas", "aceitunas_alinadas" },
            { "arrozconleche", "arroz_con_leche" },
            { "champinonesajillo", "champinones_al_ajillo" },
            { "polloajillo", "pollo_al_ajillo" },
            { "calamaresromana", "calamares_a_la_romana" },
            { "huevosrotos", "huevos_rotos" },
            { "pulpogallega", "pulpo_a_la_gallega" },
            { "quesomanchego", "queso_manchego" },
            { "atunencebollado", "atun_encebollado" },
            { "cazonadobo", "cazon_en_adobo" },
            { "jamoniberico", "jamon_iberico" },
            { "tartaqueso", "tarta_queso" },
            { "cocido", "cocido_madrileno" },
            { "fabada", "fabada_asturiana" },
            { "lentejas", "lentejas_estofadas" },
            { "marmitako_stew", "marmitako" },
            { "pote", "pote_asturiano" },
            { "croquetasjamoniberico", "croquetas_jamon_iberico" },
            { "croquetasdejamoniberico", "croquetas_jamon_iberico" }
        };

    private sealed class ResolvedImage
    {
        public string DishId;
        public string AssetPath;
        public string FileName;
        public Sprite Sprite;
        public string Status;
        public string DisplayName;
    }

    [MenuItem("Tools/Bistro Builder/Menu/Rebuild Dish Image Catalog")]
    public static void RebuildFromMenu()
    {
        Rebuild();
    }

    public static void RebuildFromCommandLine()
    {
        Rebuild();
    }

    public static void Rebuild()
    {
        EnsureFolders();
        AssetDatabase.Refresh();

        Dictionary<string, string> dishNames = LoadDishNames();
        List<string> imagePaths = FindImageAssetPaths();
        List<ResolvedImage> resolved = new List<ResolvedImage>(imagePaths.Count);
        HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);
        List<string> warnings = new List<string>();

        for (int i = 0; i < imagePaths.Count; i++)
        {
            string path = imagePaths[i];
            string fileName = Path.GetFileName(path);
            string dishId = ResolveDishId(Path.GetFileNameWithoutExtension(path));
            if (!dishNames.ContainsKey(dishId))
            {
                string prefixedDishId = "dish_" + dishId;
                if (dishNames.ContainsKey(prefixedDishId))
                {
                    dishId = prefixedDishId;
                }
            }

            if (string.IsNullOrWhiteSpace(dishId))
            {
                warnings.Add(fileName + ": nombre no vÃ¡lido.");
                continue;
            }

            if (!seenIds.Add(dishId))
            {
                warnings.Add(fileName + ": DishId duplicado " + dishId + ".");
                continue;
            }

            EnsureSpriteImporter(path);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                warnings.Add(fileName + ": Unity no pudo cargar el Sprite.");
                continue;
            }

            bool knownDish = dishNames.TryGetValue(dishId, out string displayName);
            resolved.Add(new ResolvedImage
            {
                DishId = dishId,
                AssetPath = path,
                FileName = fileName,
                Sprite = sprite,
                Status = knownDish ? "OK" : "IMAGE_ONLY",
                DisplayName = knownDish ? displayName : string.Empty
            });
        }

        resolved.Sort((a, b) =>
            string.CompareOrdinal(a.DishId, b.DishId));

        BistroBuilderDishImageCatalog catalog = EnsureCatalogAsset();
        WriteCatalogEntries(catalog, resolved);
        WriteManifest(resolved, warnings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "BB_DISH_IMAGES_PASS|IMAGES=" + resolved.Count +
            "|WARNINGS=" + warnings.Count +
            "|FOLDER=" + ImageFolder
        );
    }

    public static bool IsSupportedDishImagePath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath) ||
            !assetPath.StartsWith(ImageFolder + "/", StringComparison.Ordinal))
        {
            return false;
        }

        return SupportedExtensions.Contains(Path.GetExtension(assetPath));
    }

    private static void EnsureFolders()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        Directory.CreateDirectory(Path.Combine(projectRoot, ImageFolder));
        Directory.CreateDirectory(Path.Combine(
            projectRoot,
            "Assets/Resources/BistroBuilder/Menu"
        ));
    }

    private static List<string> FindImageAssetPaths()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string fullFolder = Path.Combine(projectRoot, ImageFolder);
        if (!Directory.Exists(fullFolder))
        {
            return new List<string>();
        }

        return Directory.GetFiles(fullFolder, "*.*", SearchOption.AllDirectories)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .Select(path => path.Replace('\\', '/').Substring(projectRoot.Replace('\\', '/').Length + 1))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static Dictionary<string, string> LoadDishNames()
    {
        Dictionary<string, string> result =
            new Dictionary<string, string>(StringComparer.Ordinal);

        string[] guids = AssetDatabase.FindAssets("t:BistroBuilderDishDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            BistroBuilderDishDefinition dish =
                AssetDatabase.LoadAssetAtPath<BistroBuilderDishDefinition>(path);
            if (dish == null || string.IsNullOrWhiteSpace(dish.DishId))
            {
                continue;
            }

            result[dish.DishId.Trim()] = dish.DisplayName ?? string.Empty;
        }

        return result;
    }

    private static void EnsureSpriteImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static BistroBuilderDishImageCatalog EnsureCatalogAsset()
    {
        BistroBuilderDishImageCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderDishImageCatalog>(
                CatalogAssetPath
            );

        if (catalog != null)
        {
            return catalog;
        }

        catalog = ScriptableObject.CreateInstance<BistroBuilderDishImageCatalog>();
        AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        return catalog;
    }

    private static void WriteCatalogEntries(
        BistroBuilderDishImageCatalog catalog,
        List<ResolvedImage> resolved
    )
    {
        SerializedObject serialized = new SerializedObject(catalog);
        SerializedProperty entries = serialized.FindProperty("entries");
        entries.arraySize = resolved.Count;

        for (int i = 0; i < resolved.Count; i++)
        {
            ResolvedImage source = resolved[i];
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("dishId").stringValue = source.DishId;
            entry.FindPropertyRelative("image").objectReferenceValue = source.Sprite;
            entry.FindPropertyRelative("sourceFileName").stringValue = source.FileName;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static string ResolveDishId(string rawName)
    {
        string normalized = NormalizeId(rawName);
        if (Aliases.TryGetValue(normalized, out string alias))
        {
            return alias;
        }

        string compact = normalized.Replace("_", string.Empty);
        return Aliases.TryGetValue(compact, out alias) ? alias : normalized;
    }

    private static string NormalizeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(decomposed.Length);
        bool previousUnderscore = false;

        for (int i = 0; i < decomposed.Length; i++)
        {
            char c = decomposed[i];
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                previousUnderscore = false;
            }
            else if (!previousUnderscore && builder.Length > 0)
            {
                builder.Append('_');
                previousUnderscore = true;
            }
        }

        return builder.ToString().Trim('_');
    }

    private static void WriteManifest(
        List<ResolvedImage> resolved,
        List<string> warnings
    )
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("dish_id,display_name,file,status");

        for (int i = 0; i < resolved.Count; i++)
        {
            ResolvedImage entry = resolved[i];
            csv.Append(EscapeCsv(entry.DishId)).Append(',')
                .Append(EscapeCsv(entry.DisplayName)).Append(',')
                .Append(EscapeCsv(entry.FileName)).Append(',')
                .Append(EscapeCsv(entry.Status)).AppendLine();
        }

        for (int i = 0; i < warnings.Count; i++)
        {
            csv.Append(",,,").Append(EscapeCsv("WARNING: " + warnings[i])).AppendLine();
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        File.WriteAllText(
            Path.Combine(projectRoot, ManifestAssetPath),
            csv.ToString(),
            new UTF8Encoding(false)
        );
    }

    private static string EscapeCsv(string value)
    {
        string safe = value ?? string.Empty;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}
