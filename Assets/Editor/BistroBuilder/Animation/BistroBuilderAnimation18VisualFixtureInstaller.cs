using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Publica el fixture Humanoid CC0 usado para demostrar visualmente BB18.
/// No convierte a Quaternius en dependencia de gameplay: sÃ³lo alimenta el
/// catÃ¡logo semÃ¡ntico mediante Motion Profiles sustituibles.
/// </summary>
public static class BistroBuilderAnimation18VisualFixtureInstaller
{
    private const string ModelPath =
        "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/UAL1_Standard.fbx";
    private const string CatalogPath =
        "Assets/Data/Animation/BistroBuilderMotionCatalog.asset";
    private const string MotionFolder = "Assets/Data/Animation/Motions";
    private const string SourceUrl =
        "https://quaternius.itch.io/universal-animation-library";
    private const string SourceVersion = "UAL1 Standard 2026-06-16";

    private readonly struct MotionSeed
    {
        public readonly string id;
        public readonly string clipName;
        public readonly BistroBuilderAnimationBodyMode bodyMode;
        public readonly bool loop;
        public readonly float speed;
        public readonly bool mirrorable;
        public readonly string fallback;

        public MotionSeed(
            string id,
            string clipName,
            BistroBuilderAnimationBodyMode bodyMode,
            bool loop,
            float speed,
            bool mirrorable = false,
            string fallback = "")
        {
            this.id = id;
            this.clipName = clipName;
            this.bodyMode = bodyMode;
            this.loop = loop;
            this.speed = speed;
            this.mirrorable = mirrorable;
            this.fallback = fallback;
        }
    }

    [MenuItem("Bistro Builder/18 Animacion e interacciones/Instalar fixture visual Humanoid")]
    public static void Install()
    {
        if (!File.Exists(Path.GetFullPath(ModelPath)))
            throw new FileNotFoundException("Falta el FBX CC0 de Quaternius.", ModelPath);

        EnsureFolder(MotionFolder);
        ConfigureImporter();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
        Avatar avatar = subAssets.OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
            throw new InvalidOperationException(
                "UAL1_Standard no produjo un Avatar Humanoid vÃ¡lido en Unity.");

        List<AnimationClip> clips = subAssets
            .OfType<AnimationClip>()
            .Where(item => item != null && !item.name.StartsWith("__preview__", StringComparison.Ordinal))
            .ToList();
        if (clips.Count < 8)
            throw new InvalidOperationException("UAL1_Standard no expone suficientes clips.");

        MotionSeed[] seeds =
        {
            new MotionSeed("locomotion.idle", "Idle_Loop", BistroBuilderAnimationBodyMode.FullBody, true, 0.01f),
            new MotionSeed("locomotion.walk", "Walk_Loop", BistroBuilderAnimationBodyMode.FullBody, true, 1.25f),
            new MotionSeed("locomotion.fastwalk", "Jog_Fwd_Loop", BistroBuilderAnimationBodyMode.FullBody, true, 2.15f),
            new MotionSeed("seat.sit.standard", "Sitting_Enter", BistroBuilderAnimationBodyMode.FullBody, false, 0.01f),
            new MotionSeed("seat.stand.standard", "Sitting_Exit", BistroBuilderAnimationBodyMode.FullBody, false, 0.01f),
            new MotionSeed("seat.idle.standard", "Sitting_Idle_Loop", BistroBuilderAnimationBodyMode.FullBody, true, 0.01f),
            new MotionSeed("seat.standard", "Sitting_Enter", BistroBuilderAnimationBodyMode.FullBody, false, 0.01f, false, "seat.generic"),
            new MotionSeed("seat.generic", "Sitting_Enter", BistroBuilderAnimationBodyMode.FullBody, false, 0.01f),
            new MotionSeed("portal.open.standard", "Interact", BistroBuilderAnimationBodyMode.FullBody, false, 0.01f, true, "transfer.generic"),
            new MotionSeed("portal.close.standard", "Interact", BistroBuilderAnimationBodyMode.FullBody, false, 0.01f, true, "transfer.generic"),
            new MotionSeed("transfer.table.1h", "PickUp_Table", BistroBuilderAnimationBodyMode.FullBody, false, 0.01f, true, "transfer.generic"),
            new MotionSeed("transfer.generic", "Interact", BistroBuilderAnimationBodyMode.FullBody, false, 0.01f, true),
            new MotionSeed("social.talk.standard", "Idle_Talking_Loop", BistroBuilderAnimationBodyMode.FullBody, true, 0.01f)
        };

        BistroBuilderMotionCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderMotionCatalog>(CatalogPath);
        if (catalog == null)
            throw new InvalidOperationException("Falta el catÃ¡logo BB18 canÃ³nico.");

        var merged = new List<BistroBuilderMotionProfile>(catalog.Motions);
        int published = 0;
        for (int i = 0; i < seeds.Length; i++)
        {
            MotionSeed seed = seeds[i];
            AnimationClip clip = FindClip(clips, seed.clipName);
            if (clip == null)
                throw new InvalidOperationException("No se encontrÃ³ clip: " + seed.clipName);

            BistroBuilderMotionProfile profile = GetOrCreateMotion(seed.id);
            profile.ConfigureForEditor(
                seed.id,
                clip,
                seed.bodyMode,
                seed.loop,
                seed.speed,
                seed.mirrorable,
                seed.fallback,
                "Quaternius",
                SourceUrl,
                "CC0 1.0 Universal",
                SourceVersion);
            EditorUtility.SetDirty(profile);
            Upsert(merged, profile);
            published++;
        }

        catalog.ConfigureForEditor(merged);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!catalog.ValidateConfiguration(out string catalogError))
            throw new InvalidOperationException("CatÃ¡logo BB18 invÃ¡lido: " + catalogError);

        Debug.Log(
            "BB18_VISUAL_FIXTURE_PASS|AVATAR_HUMANOID=1|CLIPS=" + clips.Count +
            "|MOTIONS=" + published + "|SOURCE=QUATERNIUS_CC0");
    }

    public static void InstallFromCommandLine()
    {
        try
        {
            Install();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void ConfigureImporter()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("No se pudo obtener ModelImporter de UAL1_Standard.");

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.optimizeGameObjects = false;

        ModelImporterClipAnimation[] importedClips = importer.defaultClipAnimations;
        for (int i = 0; i < importedClips.Length; i++)
        {
            ModelImporterClipAnimation clip = importedClips[i];
            clip.loopTime = clip.name.IndexOf("_Loop", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        importer.clipAnimations = importedClips;
        importer.SaveAndReimport();
    }

    private static AnimationClip FindClip(
        List<AnimationClip> clips,
        string expectedName)
    {
        for (int i = 0; i < clips.Count; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null) continue;
            if (string.Equals(clip.name, expectedName, StringComparison.OrdinalIgnoreCase) ||
                clip.name.EndsWith(expectedName, StringComparison.OrdinalIgnoreCase))
                return clip;
        }
        return null;
    }

    private static BistroBuilderMotionProfile GetOrCreateMotion(string motionId)
    {
        string safe = motionId.Replace('.', '_');
        string path = MotionFolder + "/" + safe + ".asset";
        BistroBuilderMotionProfile profile =
            AssetDatabase.LoadAssetAtPath<BistroBuilderMotionProfile>(path);
        if (profile != null) return profile;

        profile = ScriptableObject.CreateInstance<BistroBuilderMotionProfile>();
        profile.name = safe;
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    private static void Upsert(
        List<BistroBuilderMotionProfile> profiles,
        BistroBuilderMotionProfile replacement)
    {
        for (int i = profiles.Count - 1; i >= 0; i--)
        {
            BistroBuilderMotionProfile current = profiles[i];
            if (current != null && current.MotionId == replacement.MotionId)
                profiles.RemoveAt(i);
        }
        profiles.Add(replacement);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Ruta de carpeta invÃ¡lida: " + path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
