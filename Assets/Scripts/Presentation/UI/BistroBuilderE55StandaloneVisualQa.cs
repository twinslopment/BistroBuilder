using System;
using System.Collections;
using BistroBuilder.UI.Iconography;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Opt-in diagnostic for standalone E5.5/V21 screenshots.
/// Requires -bb-e55-visual-qa; never changes normal gameplay startup.
/// Suppresses the New Game and Construction IMGUI layers only for a visual QA run.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderE55StandaloneVisualQa : MonoBehaviour
{
    private BistroBuilderNewGameOpeningPlayerScreen opening;
    private BistroBuilderConstructionAuthoringRuntimeTool construction;
    private bool logged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeIfRequested()
    {
        string[] args = Environment.GetCommandLineArgs();
        bool requested = Array.Exists(args, a =>
            string.Equals(a, "-bb-e55-visual-qa", StringComparison.OrdinalIgnoreCase));
        if (!requested) return;
        GameObject go = new GameObject("BB_E55_StandaloneVisualQA");
        go.AddComponent<BistroBuilderE55StandaloneVisualQa>();
        Debug.Log("BB_E55_VISUAL_QA_MODE_ENABLED");
    }

    private void Awake()
    {
        opening = FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>(FindObjectsInactive.Include);
        construction = FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        if (opening == null)
            opening = FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>(FindObjectsInactive.Include);
        if (opening != null && opening.enabled)
        {
            opening.Hide();
            opening.enabled = false;
        }
        if (construction == null)
            construction = FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>(FindObjectsInactive.Include);
        if (construction != null && construction.enabled)
            construction.enabled = false;

        if (!logged && Time.unscaledTime > 3f)
        {
            logged = true;
            BBIconCatalog catalog = BBIconCatalog.LoadDefault();
            Sprite activity = catalog != null ? catalog.GetSprite(BBIconId.NavActivity) : null;
            GameObject nav = GameObject.Find("BBNav_Actividad");
            Image[] images = nav != null ? nav.GetComponentsInChildren<Image>(true) : Array.Empty<Image>();
            string details = string.Empty;
            for (int i = 0; i < images.Length; i++)
                details += "|"+images[i].name+":"+
                    (images[i].sprite != null ? images[i].sprite.name : "NO-SPRITE")+
                    ":alpha="+images[i].color.a.ToString("0.00")+
                    ":enabled="+images[i].enabled;
            Debug.Log("BB_E55_VISUAL_QA_ICONS catalog="+(catalog != null)+
                " activity="+(activity != null ? activity.name : "NULL")+
                " nav="+(nav != null)+details);
            Debug.Log("BB_E55_VISUAL_QA_LAYERS opening="+(opening != null ? opening.enabled.ToString() : "null")+
                " construction="+(construction != null ? construction.enabled.ToString() : "null"));
        }
    }
}
