using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace BistroBuilder.UI.Iconography
{
    /// <summary>
    /// Puente transversal 21B entre la UI heredada y el catálogo canónico.
    /// Solo modifica Presentation: detecta semántica de controles y añade su icono.
    /// </summary>
    public static class BBIconographyRuntime
    {
        private const string RuntimeIconName = "BB_Icon21B";
        private static BBIconCatalog catalog;

        private readonly struct Rule
        {
            public readonly string Token;
            public readonly BBIconId Id;
            public readonly bool Semantic;

            public Rule(string token, BBIconId id, bool semantic = false)
            {
                Token = token;
                Id = id;
                Semantic = semantic;
            }
        }

        // Orden: términos específicos antes que los genéricos.
        private static readonly Rule[] Rules =
        {
            new Rule("open menu editor", BBIconId.NavMenu),
            new Rule("open menu portfolio", BBIconId.NavMenu),
            new Rule("editar carta", BBIconId.NavMenu),
            new Rule("carta", BBIconId.NavMenu),
            new Rule("open inventory warehouse", BBIconId.NavInventory),
            new Rule("inventario", BBIconId.NavInventory),
            new Rule("almacen", BBIconId.NavInventory),
            new Rule("open suppliers", BBIconId.NavSuppliers),
            new Rule("proveedores", BBIconId.NavSuppliers),
            new Rule("reservas", BBIconId.NavReservations),
            new Rule("actividad", BBIconId.NavActivity),
            new Rule("personal", BBIconId.NavStaff),
            new Rule("plantilla", BBIconId.NavStaff),
            new Rule("economia", BBIconId.NavEconomy),
            new Rule("marketing", BBIconId.NavMarketing),
            new Rule("reputacion", BBIconId.NavReputation),
            new Rule("modo edicion", BBIconId.NavEditMode),
            new Rule("modo diseño", BBIconId.NavEditMode),
            new Rule("construccion", BBIconId.NavEditMode),
            new Rule("opciones", BBIconId.NavOptions),
            new Rule("configuracion", BBIconId.NavOptions),

            new Rule("eliminar", BBIconId.ActionDelete, true),
            new Rule("borrar", BBIconId.ActionDelete, true),
            new Rule("delete", BBIconId.ActionDelete, true),
            new Rule("cancelar", BBIconId.ActionCancel, true),
            new Rule("cancel", BBIconId.ActionCancel, true),
            new Rule("guardar", BBIconId.ActionSave),
            new Rule("save", BBIconId.ActionSave),
            new Rule("confirmar", BBIconId.ActionConfirm, true),
            new Rule("aceptar", BBIconId.ActionConfirm, true),
            new Rule("validar", BBIconId.ActionConfirm, true),
            new Rule("aplicar", BBIconId.ActionConfirm, true),
            new Rule("duplicar", BBIconId.ActionDuplicate),
            new Rule("copiar", BBIconId.ActionDuplicate),
            new Rule("editar", BBIconId.ActionEdit),
            new Rule("edit", BBIconId.ActionEdit),
            new Rule("añadir", BBIconId.ActionAdd, true),
            new Rule("agregar", BBIconId.ActionAdd, true),
            new Rule("crear", BBIconId.ActionAdd, true),
            new Rule("nuevo", BBIconId.ActionAdd, true),
            new Rule("nueva", BBIconId.ActionAdd, true),
            new Rule("priorizar", BBIconId.ActionPrioritize),
            new Rule("pausar", BBIconId.ActionPause),
            new Rule("pause", BBIconId.ActionPause),
            new Rule("reanudar", BBIconId.ActionResume),
            new Rule("continuar", BBIconId.ActionResume),
            new Rule("mover", BBIconId.ActionMove),
            new Rule("rotar", BBIconId.ActionRotate),
            new Rule("girar", BBIconId.ActionRotate),

            new Rule("volver", BBIconId.GeneralBack),
            new Rule("atras", BBIconId.GeneralBack),
            new Rule("back", BBIconId.GeneralBack),
            new Rule("siguiente", BBIconId.GeneralNext),
            new Rule("next", BBIconId.GeneralNext),
            new Rule("ayuda", BBIconId.GeneralHelp),
            new Rule("help", BBIconId.GeneralHelp),
            new Rule("mas", BBIconId.GeneralMore),

            new Rule("comedor", BBIconId.AreaDining),
            new Rule("terraza", BBIconId.AreaTerrace),
            new Rule("barra", BBIconId.AreaBar),
            new Rule("cocina", BBIconId.AreaKitchen),
            new Rule("baños", BBIconId.AreaBathrooms),
            new Rule("aseos", BBIconId.AreaBathrooms),
            new Rule("entrada", BBIconId.AreaEntrance),
            new Rule("caja", BBIconId.AreaCashRegister),
            new Rule("limpieza", BBIconId.AreaCleaning),
            new Rule("mesa", BBIconId.ObjectTable),
            new Rule("silla", BBIconId.ObjectChair),
            new Rule("camarero", BBIconId.ObjectWaiter),
            new Rule("cocinero", BBIconId.ObjectCook),
            new Rule("plato", BBIconId.ObjectDish),
            new Rule("ingrediente", BBIconId.ObjectIngredient),
            new Rule("bebida", BBIconId.ObjectDrink),
            new Rule("decoracion", BBIconId.ObjectDecoration),
            new Rule("iluminacion", BBIconId.ObjectLighting)
        };

        public static int DecorateAll()
        {
            if (!EnsureCatalog())
                return 0;

            var buttons = UnityEngine.Object.FindObjectsByType<Button>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            var changed = 0;
            for (var i = 0; i < buttons.Length; i++)
            {
                if (TryDecorate(buttons[i]))
                    changed++;
            }

            return changed;
        }

        public static bool TryDecorate(Button button)
        {
            if (button == null ||
                button.GetComponentInParent<BistroBuilderEditChromeSurface>(true) != null ||
                button.GetComponent<BistroBuilderApprovedTopBarHotspot>() != null)
                return false;

            // El catálogo de artículos usa su propia iconografía vectorial,
            // definida por la dirección visual aprobada. Evita superponer 21B.
            if (button.GetComponentInParent<RestaurantPlaceableCatalogPanel>() != null)
                return false;

            var existing = button.GetComponent<BBIconButton>();
            if (existing != null)
            {
                existing.SetInteractable(button.interactable);
                return false;
            }

            if (!TryInfer(button, out var id, out var semantic))
                return false;

            return Decorate(button, id, semantic);
        }

        public static bool Decorate(
            Button button,
            BBIconId id,
            bool semanticColor = false,
            bool iconOnly = false)
        {
            if (button == null ||
                button.GetComponentInParent<BistroBuilderEditChromeSurface>(true) != null ||
                button.GetComponent<BistroBuilderApprovedTopBarHotspot>() != null ||
                !EnsureCatalog())
                return false;

            var sprite = catalog.GetSprite(id);
            if (sprite == null)
                return false;

            var existing = button.GetComponent<BBIconButton>();
            if (existing != null)
            {
                existing.Configure(id, semanticColor);
                existing.SetInteractable(button.interactable);
                return false;
            }

            var iconTransform = button.transform.Find(RuntimeIconName) as RectTransform;
            Image iconImage;
            if (iconTransform == null)
            {
                var iconObject = new GameObject(RuntimeIconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.layer = button.gameObject.layer;
                iconTransform = iconObject.GetComponent<RectTransform>();
                iconTransform.SetParent(button.transform, false);
                iconImage = iconObject.GetComponent<Image>();
            }
            else
            {
                iconImage = iconTransform.GetComponent<Image>();
                if (iconImage == null)
                    iconImage = iconTransform.gameObject.AddComponent<Image>();
            }

            iconImage.sprite = sprite;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            var labelTransform = FindLabelTransform(button, out var label);
            var hasLabel = !iconOnly && !string.IsNullOrWhiteSpace(label);

            const float size = 22f;
            iconTransform.sizeDelta = new Vector2(size, size);
            iconTransform.localScale = Vector3.one;

            if (hasLabel)
            {
                iconTransform.anchorMin = new Vector2(0f, 0.5f);
                iconTransform.anchorMax = new Vector2(0f, 0.5f);
                iconTransform.pivot = new Vector2(0.5f, 0.5f);
                iconTransform.anchoredPosition = new Vector2(18f, 0f);

                if (labelTransform != null)
                {
                    var min = labelTransform.offsetMin;
                    if (min.x < 38f)
                    {
                        min.x = 38f;
                        labelTransform.offsetMin = min;
                    }
                }
            }
            else
            {
                iconTransform.anchorMin = new Vector2(0.5f, 0.5f);
                iconTransform.anchorMax = new Vector2(0.5f, 0.5f);
                iconTransform.pivot = new Vector2(0.5f, 0.5f);
                iconTransform.anchoredPosition = Vector2.zero;
            }

            iconTransform.SetAsFirstSibling();

            var presenter = button.gameObject.AddComponent<BBIconButton>();
            presenter.ConfigureRuntime(id, iconImage, button, semanticColor);
            presenter.SetInteractable(button.interactable);
            return true;
        }

        public static bool TryInfer(Button button, out BBIconId id, out bool semantic)
        {
            id = default;
            semantic = false;
            if (button == null ||
                button.GetComponentInParent<BistroBuilderEditChromeSurface>(true) != null ||
                button.GetComponent<BistroBuilderApprovedTopBarHotspot>() != null)
                return false;

            FindLabelTransform(button, out var label);
            // Visible action wins over generic implementation names such as Menu_*.
            var haystack = Normalize(string.IsNullOrWhiteSpace(label) ? button.name : label);

            if (string.IsNullOrWhiteSpace(haystack))
                return false;

            for (var i = 0; i < Rules.Length; i++)
            {
                if (!ContainsToken(haystack, Rules[i].Token))
                    continue;

                id = Rules[i].Id;
                semantic = Rules[i].Semantic;
                return true;
            }

            return false;
        }

        private static bool EnsureCatalog()
        {
            if (catalog == null)
                catalog = BBIconCatalog.LoadDefault();
            return catalog != null;
        }

        private static RectTransform FindLabelTransform(Button button, out string label)
        {
            label = string.Empty;
            if (button == null || button.GetComponentInParent<BistroBuilderEditChromeSurface>(true) != null)
                return null;

            var legacyTexts = button.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < legacyTexts.Length; i++)
            {
                if (legacyTexts[i] == null || string.IsNullOrWhiteSpace(legacyTexts[i].text))
                    continue;

                label = legacyTexts[i].text;
                return legacyTexts[i].rectTransform;
            }

            // Evita una dependencia nueva: TMP ya existe en el proyecto, pero aquí
            // se consulta por reflexión para que el puente siga desacoplado.
            var components = button.GetComponentsInChildren<Component>(true);
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                    continue;

                var type = component.GetType();
                if (!string.Equals(type.Name, "TextMeshProUGUI", StringComparison.Ordinal))
                    continue;

                var property = type.GetProperty("text");
                if (property == null || property.PropertyType != typeof(string))
                    continue;

                var value = property.GetValue(component) as string;
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                label = value;
                return component.transform as RectTransform;
            }

            return null;
        }

        private static bool ContainsToken(string haystack, string token)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(token))
                return false;

            var normalizedToken = Normalize(token);
            if (haystack == normalizedToken)
                return true;

            return haystack.Contains(" " + normalizedToken + " ", StringComparison.Ordinal) ||
                   haystack.StartsWith(normalizedToken + " ", StringComparison.Ordinal) ||
                   haystack.EndsWith(" " + normalizedToken, StringComparison.Ordinal) ||
                   haystack.Contains(normalizedToken, StringComparison.Ordinal);
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var formD = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length + 2);
            sb.Append(' ');

            var previousWasSpace = true;
            for (var i = 0; i < formD.Length; i++)
            {
                var c = formD[i];
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;

                c = char.ToLowerInvariant(c);
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                    previousWasSpace = false;
                }
                else if (!previousWasSpace)
                {
                    sb.Append(' ');
                    previousWasSpace = true;
                }
            }

            if (!previousWasSpace)
                sb.Append(' ');

            return sb.ToString();
        }
    }

    /// <summary>
    /// Bootstrap ligero. Reescanea a baja frecuencia para cubrir pantallas que
    /// se construyen dinámicamente durante la partida.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class BBIconographyRuntimeBootstrap : MonoBehaviour
    {
        private const float RescanSeconds = 1.25f;
        private static BBIconographyRuntimeBootstrap instance;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance != null)
                return;

            var go = new GameObject("BB_Iconography21B_Runtime");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<BBIconographyRuntimeBootstrap>();
        }

        private void OnEnable()
        {
            nextScan = 0f;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScan)
                return;

            BBIconographyRuntime.DecorateAll();
            nextScan = Time.unscaledTime + RescanSeconds;
        }
    }
}
