using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Aplica semántica explícita a las piezas estructurales del HUD definitivo.
/// Evita depender de heurísticas para las acciones y superficies canónicas.
/// </summary>
public static class BistroBuilderUiSemanticBootstrap
{
    public const string RuntimeRevision = "21A-UIUX-SEMANTICS-V1.0";

    public static void Apply(Canvas canvas)
    {
        if (canvas == null) return;
        Transform root = canvas.transform.Find(BistroBuilderUiShell.RootName);
        if (root == null) return;

        Tag(root.Find(BistroBuilderUiShell.ActivityPanelName), BistroBuilderUiStyleRole.Panel);
        Tag(root.Find(BistroBuilderUiShell.ContextPanelName), BistroBuilderUiStyleRole.Panel);

        Transform top = root.Find(BistroBuilderUiShell.TopBarName);
        Transform nav = top != null ? top.Find("NavigationContent") : null;
        if (nav != null)
        {
            for (int i = 0; i < nav.childCount; i++)
            {
                Transform child = nav.GetChild(i);
                if (child == null) continue;
                if (child.name.StartsWith("BBNav_", System.StringComparison.Ordinal))
                    Tag(child, BistroBuilderUiStyleRole.NavButton);
            }
        }

        Transform bottom = root.Find(BistroBuilderUiShell.BottomBarName);
        if (bottom != null)
        {
            Transform serviceAction = bottom.Find(BistroBuilderUiShell.ServiceActionName);
            Tag(serviceAction, BistroBuilderUiStyleRole.PrimaryButton);

            Transform status = bottom.Find("StatusContent");
            if (status != null)
            {
                Tag(status.Find("Cash"), BistroBuilderUiStyleRole.Row);
                Tag(status.Find("Satisfaction"), BistroBuilderUiStyleRole.Row);
                Tag(status.Find("Kitchen"), BistroBuilderUiStyleRole.Row);
                Tag(status.Find("Waiting"), BistroBuilderUiStyleRole.Row);
            }
        }

        Transform context = root.Find(BistroBuilderUiShell.ContextPanelName);
        if (context != null)
        {
            TagText(context.Find("Title"), BistroBuilderUiStyleRole.Heading);
            TagText(context.Find("Body"), BistroBuilderUiStyleRole.Body);
        }
    }

    private static void Tag(Transform target, BistroBuilderUiStyleRole role)
    {
        if (target == null) return;
        BistroBuilderUiStyleTag tag = target.GetComponent<BistroBuilderUiStyleTag>();
        if (tag == null) tag = target.gameObject.AddComponent<BistroBuilderUiStyleTag>();
        tag.Configure(role);
    }

    private static void TagText(Transform target, BistroBuilderUiStyleRole role)
    {
        if (target == null || target.GetComponent<TMP_Text>() == null) return;
        Tag(target, role);
    }
}
