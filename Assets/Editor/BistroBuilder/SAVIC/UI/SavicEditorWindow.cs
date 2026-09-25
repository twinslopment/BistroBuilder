using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicEditorWindow : EditorWindow
    {
        private const string MenuPath =
            "Tools/Bistro Builder/SAVIC/Open Control Center";

        private const float ListRowHeight = 54f;

        private static readonly Color Background =
            new Color(0.105f, 0.11f, 0.10f);

        private static readonly Color Panel =
            new Color(0.145f, 0.15f, 0.135f);

        private static readonly Color PanelRaised =
            new Color(0.18f, 0.185f, 0.165f);

        private static readonly Color Border =
            new Color(0.29f, 0.30f, 0.255f);

        private static readonly Color TextPrimary =
            new Color(0.92f, 0.91f, 0.84f);

        private static readonly Color TextMuted =
            new Color(0.67f, 0.67f, 0.60f);

        private static readonly Color Accent =
            new Color(0.62f, 0.69f, 0.31f);

        private static readonly Color Pass =
            new Color(0.34f, 0.70f, 0.45f);

        private static readonly Color Warning =
            new Color(0.90f, 0.68f, 0.25f);

        private static readonly Color Error =
            new Color(0.88f, 0.34f, 0.28f);

        [SerializeField]
        private SavicEditorSection selectedSection =
            SavicEditorSection.Summary;

        [SerializeField]
        private string queueSearch = string.Empty;

        [SerializeField]
        private string queueState = "Todos";

        [SerializeField]
        private string reviewSearch = string.Empty;

        [SerializeField]
        private string reviewSeverity = "Todos";

        [SerializeField]
        private string reviewSource = "Todos";

        [SerializeField]
        private string librarySearch = string.Empty;

        [SerializeField]
        private string libraryFamily = "Todos";

        [SerializeField]
        private string libraryCategory = "Todos";

        [SerializeField]
        private string libraryStatus = "Todos";

        [SerializeField]
        private string libraryOrigin = "Todos";

        [SerializeField]
        private string libraryVersion = "Todos";

        [SerializeField]
        private string validationSearch = string.Empty;

        [SerializeField]
        private string validationResult = "Todos";

        [SerializeField]
        private string validationSeverity = "Todos";

        [SerializeField]
        private string validationSource = "Todos";

        [SerializeField]
        private string historySearch = string.Empty;

        [SerializeField]
        private string historyKind = "Todos";

        private readonly Dictionary<SavicEditorSection, Button> navigation =
            new Dictionary<SavicEditorSection, Button>();

        private SavicEditorContext context;
        private SavicEditorSnapshot snapshot = SavicEditorSnapshot.Empty;
        private VisualElement contentHost;
        private Label footerLabel;
        private bool subscribed;
        private bool refreshQueued;
        private bool refreshInProgress;
        private bool reloadRequested;

        [MenuItem(MenuPath, false, 0)]
        public static void Open()
        {
            SavicEditorWindow window =
                GetWindow<SavicEditorWindow>();

            window.titleContent =
                new GUIContent("SAVIC");

            window.minSize =
                new Vector2(920f, 600f);

            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("SAVIC");
            minSize = new Vector2(920f, 600f);
        }

        private void OnDisable()
        {
            UnsubscribeFromData();
            EditorApplication.delayCall -= ExecuteQueuedRefresh;
            refreshQueued = false;
        }

        private void OnFocus()
        {
            RequestRefresh(false);
        }

        public void CreateGUI()
        {
            BuildChrome();
            SubscribeToData();
            RequestRefresh(true);
        }

        internal static void ConfigureVirtualizedList(
            ListView listView,
            float fixedItemHeight)
        {
            if (listView == null)
                throw new ArgumentNullException(nameof(listView));

            listView.fixedItemHeight =
                Mathf.Max(18f, fixedItemHeight);

            listView.virtualizationMethod =
                CollectionVirtualizationMethod.FixedHeight;

            listView.selectionType =
                SelectionType.Single;

            listView.reorderable = false;
        }

        internal static void ConfigureStandardRowUnbinding(
            ListView listView)
        {
            if (listView == null)
                throw new ArgumentNullException(nameof(listView));

            listView.unbindItem =
                (element, _) =>
                {
                    ClearRowLabel(element, "title");
                    ClearRowLabel(element, "subtitle");
                    ClearRowLabel(element, "badge");
                };
        }

        private void SubscribeToData()
        {
            if (subscribed)
                return;

            try
            {
                context = SavicEditorContext.Instance;
                context.Manifests.Changed += OnSourceDataChanged;
                context.Jobs.Changed += OnSourceDataChanged;
                context.ProjectInventory.Changed += OnSourceDataChanged;
                subscribed = true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SAVIC] Control Center subscription failed safely: " +
                    exception);
            }
        }

        private void UnsubscribeFromData()
        {
            if (!subscribed || context == null)
                return;

            context.Manifests.Changed -= OnSourceDataChanged;
            context.Jobs.Changed -= OnSourceDataChanged;
            context.ProjectInventory.Changed -= OnSourceDataChanged;
            subscribed = false;
        }

        private void OnSourceDataChanged()
        {
            if (!refreshInProgress)
                RequestRefresh(false);
        }

        private void RequestRefresh(bool reloadFromDisk)
        {
            reloadRequested |= reloadFromDisk;

            if (refreshQueued)
                return;

            refreshQueued = true;
            EditorApplication.delayCall += ExecuteQueuedRefresh;
        }

        private void ExecuteQueuedRefresh()
        {
            EditorApplication.delayCall -= ExecuteQueuedRefresh;
            refreshQueued = false;

            if (this == null)
                return;

            bool shouldReload = reloadRequested;
            reloadRequested = false;
            RefreshSnapshot(shouldReload);
        }

        private void RefreshSnapshot(bool reloadFromDisk)
        {
            if (refreshInProgress)
                return;

            refreshInProgress = true;

            try
            {
                SubscribeToData();
                context ??= SavicEditorContext.Instance;

                if (reloadFromDisk)
                {
                    context.Manifests.Reload();
                    context.Jobs.Reload();
                }

                context.ProjectInventory.TryLoadPersisted(
                    out SavicProjectInventorySnapshot inventory);

                snapshot = SavicEditorReadModel.Build(
                    context.Manifests.GetAll(),
                    context.Jobs.Jobs,
                    inventory);

                RenderSelectedSection();
                SetFooter(
                    "Datos actualizados · " +
                    DateTime.UtcNow.ToString(
                        "yyyy-MM-dd HH:mm:ss 'UTC'",
                        CultureInfo.InvariantCulture),
                    TextMuted);
            }
            catch (Exception exception)
            {
                SetFooter(
                    "No se pudo actualizar: " + exception.Message,
                    Error);

                Debug.LogError(
                    "[SAVIC] Control Center refresh failed safely: " +
                    exception);

                if (contentHost != null)
                {
                    contentHost.Clear();
                    contentHost.Add(
                        CreateNotice(
                            "No se pudo leer el estado de SAVIC.",
                            exception.Message,
                            Error));
                }
            }
            finally
            {
                refreshInProgress = false;
            }
        }

        private void BuildChrome()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1f;
            root.style.backgroundColor = Background;
            root.style.color = TextPrimary;

            root.Add(BuildHeader());

            VisualElement body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1f;
            body.style.minHeight = 0f;

            body.Add(BuildNavigation());

            contentHost = new VisualElement();
            contentHost.name = "savic-content-host";
            contentHost.style.flexGrow = 1f;
            contentHost.style.minWidth = 0f;
            contentHost.style.minHeight = 0f;
            body.Add(contentHost);

            root.Add(body);

            footerLabel = new Label("Preparando datos…");
            footerLabel.style.height = 24f;
            footerLabel.style.paddingLeft = 12f;
            footerLabel.style.paddingRight = 12f;
            footerLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            footerLabel.style.fontSize = 10f;
            footerLabel.style.color = TextMuted;
            footerLabel.style.backgroundColor = Panel;
            footerLabel.style.borderTopWidth = 1f;
            footerLabel.style.borderTopColor = Border;
            root.Add(footerLabel);

            contentHost.Add(
                CreateNotice(
                    "Cargando SAVIC",
                    "Leyendo manifests, cola e inventario persistido.",
                    Accent));
        }

        private VisualElement BuildHeader()
        {
            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 16f;
            header.style.paddingRight = 12f;
            header.style.paddingTop = 10f;
            header.style.paddingBottom = 10f;
            header.style.backgroundColor = Panel;
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = Border;

            VisualElement identity = new VisualElement();
            identity.style.flexGrow = 1f;

            Label title = new Label("SAVIC");
            title.style.fontSize = 20f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = TextPrimary;
            identity.Add(title);

            Label subtitle = new Label(
                "Autoría, validación e integración de contenido · " +
                SavicVersion.ProductVersion +
                " · " +
                SavicVersion.PipelineVersion);

            subtitle.style.fontSize = 10f;
            subtitle.style.color = TextMuted;
            identity.Add(subtitle);
            header.Add(identity);

            header.Add(
                CreateHeaderButton(
                    "Escanear entrada",
                    () => RunAction(
                        "Escaneo de entrada",
                        () => context.Intake.Tick(),
                        true)));

            header.Add(
                CreateHeaderButton(
                    "Actualizar inventario",
                    () => RunAction(
                        "Inventario",
                        () => context.ProjectInventory.ScanAndPersist(),
                        false)));

            header.Add(
                CreateHeaderButton(
                    "Recargar",
                    () => RequestRefresh(true)));

            return header;
        }

        private VisualElement BuildNavigation()
        {
            navigation.Clear();

            VisualElement rail = new VisualElement();
            rail.style.width = 166f;
            rail.style.flexShrink = 0f;
            rail.style.paddingTop = 12f;
            rail.style.paddingLeft = 8f;
            rail.style.paddingRight = 8f;
            rail.style.backgroundColor = Panel;
            rail.style.borderRightWidth = 1f;
            rail.style.borderRightColor = Border;

            AddNavigationButton(
                rail,
                SavicEditorSection.Summary,
                "Resumen");

            AddNavigationButton(
                rail,
                SavicEditorSection.Queue,
                "Cola");

            AddNavigationButton(
                rail,
                SavicEditorSection.Review,
                "Revisión");

            AddNavigationButton(
                rail,
                SavicEditorSection.Library,
                "Biblioteca");

            AddNavigationButton(
                rail,
                SavicEditorSection.Adoption,
                "Adopción");

            AddNavigationButton(
                rail,
                SavicEditorSection.Validation,
                "Validación");

            AddNavigationButton(
                rail,
                SavicEditorSection.History,
                "Historial");

            AddNavigationButton(
                rail,
                SavicEditorSection.Settings,
                "Ajustes");

            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            rail.Add(spacer);

            Label mode = new Label(
                "Editor-only\nLectura segura");

            mode.style.fontSize = 9f;
            mode.style.color = TextMuted;
            mode.style.marginLeft = 8f;
            mode.style.marginBottom = 12f;
            rail.Add(mode);

            UpdateNavigationStyles();
            return rail;
        }

        private void AddNavigationButton(
            VisualElement rail,
            SavicEditorSection section,
            string text)
        {
            Button button =
                new Button(
                    () => SelectSection(section))
                {
                    text = text
                };

            button.style.height = 34f;
            button.style.marginBottom = 4f;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.paddingLeft = 12f;
            button.style.borderLeftWidth = 3f;
            button.style.borderTopWidth = 0f;
            button.style.borderRightWidth = 0f;
            button.style.borderBottomWidth = 0f;

            navigation.Add(section, button);
            rail.Add(button);
        }

        private void SelectSection(SavicEditorSection section)
        {
            selectedSection = section;
            UpdateNavigationStyles();
            RenderSelectedSection();
        }

        private void UpdateNavigationStyles()
        {
            foreach (
                KeyValuePair<SavicEditorSection, Button> pair in navigation)
            {
                bool selected = pair.Key == selectedSection;
                pair.Value.style.backgroundColor =
                    selected ? PanelRaised : Color.clear;

                pair.Value.style.color =
                    selected ? TextPrimary : TextMuted;

                pair.Value.style.borderLeftColor =
                    selected ? Accent : Color.clear;
            }
        }

        private void RenderSelectedSection()
        {
            if (contentHost == null)
                return;

            contentHost.Clear();

            switch (selectedSection)
            {
                case SavicEditorSection.Summary:
                    RenderSummary();
                    break;

                case SavicEditorSection.Queue:
                    RenderQueue();
                    break;

                case SavicEditorSection.Review:
                    RenderReview();
                    break;

                case SavicEditorSection.Library:
                    RenderLibrary();
                    break;

                case SavicEditorSection.Adoption:
                    RenderAdoption();
                    break;

                case SavicEditorSection.Validation:
                    RenderValidation();
                    break;

                case SavicEditorSection.History:
                    RenderHistory();
                    break;

                case SavicEditorSection.Settings:
                    RenderSettings();
                    break;

                default:
                    SelectSection(SavicEditorSection.Summary);
                    break;
            }
        }

        private void RenderSummary()
        {
            ScrollView page = CreatePageScroll();
            AddSectionTitle(
                page,
                "Resumen",
                "Estado operativo consolidado de SAVIC.");

            VisualElement cards = new VisualElement();
            cards.style.flexDirection = FlexDirection.Row;
            cards.style.flexWrap = Wrap.Wrap;
            cards.style.marginTop = 12f;
            cards.style.marginBottom = 10f;

            SavicEditorSummary summary = snapshot.Summary;
            cards.Add(CreateMetricCard("Gestionados", summary.TotalManaged, Accent));
            cards.Add(CreateMetricCard("PASS", summary.Passed, Pass));
            cards.Add(CreateMetricCard("Autocorregidos", summary.AutoCorrected, Pass));
            cards.Add(CreateMetricCard("Revisión", summary.NeedsReview, Warning));
            cards.Add(CreateMetricCard("Errores", summary.Errors, Error));
            cards.Add(CreateMetricCard("Stale", summary.Stale, TextMuted));
            cards.Add(CreateMetricCard("En cola / activos", summary.QueuedOrActive, Accent));
            page.Add(cards);

            if (summary.Errors > 0 || summary.NeedsReview > 0)
            {
                page.Add(
                    CreateNotice(
                        "Atención requerida",
                        summary.Errors +
                        " errores y " +
                        summary.NeedsReview +
                        " excepciones están visibles en Revisión.",
                        summary.Errors > 0 ? Error : Warning));
            }
            else
            {
                page.Add(
                    CreateNotice(
                        "Sin excepciones pendientes",
                        "No hay errores ni revisiones registradas en la instantánea actual.",
                        Pass));
            }

            VisualElement inventory = CreatePanel();
            AddGroupTitle(inventory, "Inventario del proyecto");
            AddField(inventory, "Items", snapshot.Inventory.totalItems.ToString());
            AddField(inventory, "Gestionados por SAVIC", snapshot.Inventory.managedBySavic.ToString());
            AddField(inventory, "Legacy pendiente de adopción", summary.LegacyPendingAdoption.ToString());
            AddField(inventory, "Incidencias", summary.InventoryIssues.ToString());
            AddField(
                inventory,
                "Instantánea",
                FormatTimestamp(snapshot.Inventory.generatedUtc));

            if (summary.LegacyPendingAdoption > 0)
            {
                inventory.Add(
                    CreateSecondaryButton(
                        "Revisar adopción legacy (" +
                        summary.LegacyPendingAdoption +
                        ")",
                        () => SelectSection(
                            SavicEditorSection.Adoption)));
            }

            page.Add(inventory);

            AddSubheading(page, "Actividad reciente");

            List<SavicEditorHistoryRow> recent =
                snapshot.History.Take(12).ToList();

            if (recent.Count == 0)
            {
                page.Add(
                    CreateEmptyState(
                        "Todavía no hay actividad registrada."));
            }
            else
            {
                ListView historyList =
                    CreateListView(
                        recent,
                        CreateStandardRow,
                        BindHistoryRow);

                historyList.style.height =
                    Mathf.Min(360f, recent.Count * ListRowHeight + 2f);

                historyList.selectionType = SelectionType.None;
                page.Add(historyList);
            }

            contentHost.Add(page);
        }

        private void RenderQueue()
        {
            VisualElement page = CreatePage();
            AddSectionTitle(
                page,
                "Cola",
                "Procesamiento serializado, persistente y recuperable entre recargas del Editor.");

            VisualElement batch = CreatePanel();
            AddGroupTitle(batch, "Batch / Recovery");
            AddField(
                batch,
                "Estado",
                context.Jobs.IsPaused ? "PAUSADO" : "ACTIVO");
            AddField(
                batch,
                "Pendientes / activos",
                context.Jobs.PendingProcessCount.ToString());

            VisualElement batchActions = new VisualElement();
            batchActions.style.flexDirection = FlexDirection.Row;
            batchActions.style.flexWrap = Wrap.Wrap;
            batchActions.style.marginTop = 6f;

            batchActions.Add(
                CreateSecondaryButton(
                    context.Jobs.IsPaused
                        ? "Reanudar cola"
                        : "Pausar cola",
                    () => RunAction(
                        context.Jobs.IsPaused
                            ? "Reanudación de cola"
                            : "Pausa de cola",
                        () => context.Jobs.SetPaused(
                            !context.Jobs.IsPaused),
                        false)));

            batch.Add(batchActions);
            page.Add(batch);

            VisualElement filters = CreateFilterBar();
            TextField search = CreateSearchField(queueSearch);
            List<string> stateChoices =
                BuildChoices(snapshot.Jobs, row => row.State);

            queueState = NormalizeChoice(queueState, stateChoices);
            DropdownField state =
                CreateDropdown("Estado", stateChoices, queueState);

            filters.Add(search);
            filters.Add(state);
            page.Add(filters);

            List<SavicEditorJobRow> visible =
                SavicEditorReadModel.FilterJobs(
                    snapshot.Jobs,
                    queueSearch,
                    queueState);

            VisualElement detail = CreateDetailScroll();
            ListView list =
                CreateListView(
                    visible,
                    CreateStandardRow,
                    BindJobRow);

            list.selectionChanged +=
                selection =>
                {
                    SavicEditorJobRow row =
                        FirstSelection<SavicEditorJobRow>(selection);

                    RenderJobDetail(detail, row);
                };

            VisualElement split = CreateSplit(list, detail);
            page.Add(split);
            contentHost.Add(page);

            void ApplyFilters()
            {
                visible = SavicEditorReadModel.FilterJobs(
                    snapshot.Jobs,
                    queueSearch,
                    queueState);

                SetItems(list, visible);
                SelectFirstOrClear(list, visible, detail, RenderJobDetail);
            }

            search.RegisterValueChangedCallback(
                change =>
                {
                    queueSearch = change.newValue ?? string.Empty;
                    ApplyFilters();
                });

            state.RegisterValueChangedCallback(
                change =>
                {
                    queueState = change.newValue ?? "Todos";
                    ApplyFilters();
                });

            SelectFirstOrClear(list, visible, detail, RenderJobDetail);
        }

        private void RenderReview()
        {
            VisualElement page = CreatePage();
            AddSectionTitle(
                page,
                "Revisión",
                "Excepciones concretas; el contenido sano no necesita intervención.");

            VisualElement filters = CreateFilterBar();
            TextField search = CreateSearchField(reviewSearch);

            List<string> severityChoices =
                BuildChoices(snapshot.Reviews, row => row.Severity);

            List<string> sourceChoices =
                BuildChoices(snapshot.Reviews, row => row.Source);

            reviewSeverity = NormalizeChoice(reviewSeverity, severityChoices);
            reviewSource = NormalizeChoice(reviewSource, sourceChoices);

            DropdownField severity =
                CreateDropdown(
                    "Severidad",
                    severityChoices,
                    reviewSeverity);

            DropdownField source =
                CreateDropdown(
                    "Origen",
                    sourceChoices,
                    reviewSource);

            filters.Add(search);
            filters.Add(severity);
            filters.Add(source);
            page.Add(filters);

            List<SavicEditorReviewRow> visible =
                SavicEditorReadModel.FilterReviews(
                    snapshot.Reviews,
                    reviewSearch,
                    reviewSeverity,
                    reviewSource);

            VisualElement detail = CreateDetailScroll();
            ListView list =
                CreateListView(
                    visible,
                    CreateStandardRow,
                    BindReviewRow);

            list.selectionChanged +=
                selection =>
                    RenderReviewDetail(
                        detail,
                        FirstSelection<SavicEditorReviewRow>(selection));

            page.Add(CreateSplit(list, detail));
            contentHost.Add(page);

            void ApplyFilters()
            {
                visible = SavicEditorReadModel.FilterReviews(
                    snapshot.Reviews,
                    reviewSearch,
                    reviewSeverity,
                    reviewSource);

                SetItems(list, visible);
                SelectFirstOrClear(list, visible, detail, RenderReviewDetail);
            }

            search.RegisterValueChangedCallback(
                change =>
                {
                    reviewSearch = change.newValue ?? string.Empty;
                    ApplyFilters();
                });

            severity.RegisterValueChangedCallback(
                change =>
                {
                    reviewSeverity = change.newValue ?? "Todos";
                    ApplyFilters();
                });

            source.RegisterValueChangedCallback(
                change =>
                {
                    reviewSource = change.newValue ?? "Todos";
                    ApplyFilters();
                });

            SelectFirstOrClear(list, visible, detail, RenderReviewDetail);
        }

        private void RenderLibrary()
        {
            VisualElement page = CreatePage();
            AddSectionTitle(
                page,
                "Biblioteca",
                "Inventario canónico de contenido gestionado por SAVIC.");

            VisualElement filters = CreateFilterBar();
            TextField search = CreateSearchField(librarySearch);

            List<string> familyChoices =
                BuildChoices(snapshot.Assets, row => row.Family);

            List<string> categoryChoices =
                BuildChoices(snapshot.Assets, row => row.Category);

            List<string> statusChoices =
                BuildChoices(snapshot.Assets, row => row.Status);

            List<string> originChoices =
                BuildChoices(snapshot.Assets, row => row.Origin);

            List<string> versionChoices =
                BuildChoices(snapshot.Assets, row => row.Version);

            libraryFamily = NormalizeChoice(libraryFamily, familyChoices);
            libraryCategory = NormalizeChoice(libraryCategory, categoryChoices);
            libraryStatus = NormalizeChoice(libraryStatus, statusChoices);
            libraryOrigin = NormalizeChoice(libraryOrigin, originChoices);
            libraryVersion = NormalizeChoice(libraryVersion, versionChoices);

            DropdownField family =
                CreateDropdown("Familia", familyChoices, libraryFamily);

            DropdownField category =
                CreateDropdown("Categoría", categoryChoices, libraryCategory);

            DropdownField status =
                CreateDropdown("Estado", statusChoices, libraryStatus);

            DropdownField origin =
                CreateDropdown("Origen", originChoices, libraryOrigin);

            DropdownField version =
                CreateDropdown("Versión", versionChoices, libraryVersion);

            filters.Add(search);
            filters.Add(family);
            filters.Add(category);
            filters.Add(status);
            filters.Add(origin);
            filters.Add(version);
            page.Add(filters);

            List<SavicEditorAssetRow> visible =
                FilterLibrary();

            VisualElement detail = CreateDetailScroll();
            ListView list =
                CreateListView(
                    visible,
                    CreateStandardRow,
                    BindAssetRow);

            list.selectionChanged +=
                selection =>
                    RenderAssetDetail(
                        detail,
                        FirstSelection<SavicEditorAssetRow>(selection));

            page.Add(CreateSplit(list, detail));
            contentHost.Add(page);

            List<SavicEditorAssetRow> FilterLibrary()
            {
                return SavicEditorReadModel.FilterAssets(
                    snapshot.Assets,
                    librarySearch,
                    libraryFamily,
                    libraryCategory,
                    libraryStatus,
                    libraryOrigin,
                    libraryVersion);
            }

            void ApplyFilters()
            {
                visible = FilterLibrary();
                SetItems(list, visible);
                SelectFirstOrClear(list, visible, detail, RenderAssetDetail);
            }

            search.RegisterValueChangedCallback(
                change =>
                {
                    librarySearch = change.newValue ?? string.Empty;
                    ApplyFilters();
                });

            family.RegisterValueChangedCallback(
                change =>
                {
                    libraryFamily = change.newValue ?? "Todos";
                    ApplyFilters();
                });

            category.RegisterValueChangedCallback(
                change =>
                {
                    libraryCategory = change.newValue ?? "Todos";
                    ApplyFilters();
                });

            status.RegisterValueChangedCallback(
                change =>
                {
                    libraryStatus = change.newValue ?? "Todos";
                    ApplyFilters();
                });

            origin.RegisterValueChangedCallback(
                change =>
                {
                    libraryOrigin = change.newValue ?? "Todos";
                    ApplyFilters();
                });

            version.RegisterValueChangedCallback(
                change =>
                {
                    libraryVersion = change.newValue ?? "Todos";
                    ApplyFilters();
                });

            SelectFirstOrClear(list, visible, detail, RenderAssetDetail);
        }

        private void RenderAdoption()
        {
            VisualElement page = CreatePage();

            AddSectionTitle(
                page,
                "Adopción",
                "Incorpora contenido ya existente al inventario SAVIC sin reconstruir ni sustituir assets.");

            IReadOnlyList<SavicLegacyAdoptionCandidate> preview =
                context.LegacyAdoption.GetPreview(false);

            List<SavicLegacyAdoptionCandidate> candidates =
                preview.ToList();

            int eligible =
                candidates.Count(candidate => candidate.Eligible);

            int recommended =
                candidates.Count(
                    candidate =>
                        candidate.Eligible &&
                        candidate.RecommendedForBatch);

            page.Add(
                CreateNotice(
                    "Adopción no destructiva",
                    "SAVIC registra identidad, referencias y baseline. " +
                    "No cambia GUID, prefab, materiales, iconos ni ItemId existentes.",
                    Pass));

            VisualElement summary = CreatePanel();
            AddGroupTitle(summary, "Vista previa");
            AddField(summary, "Pendientes", candidates.Count.ToString());
            AddField(summary, "Elegibles", eligible.ToString());
            AddField(summary, "Recomendados en lote", recommended.ToString());
            AddField(
                summary,
                "Manual / excluido del lote",
                Math.Max(0, candidates.Count - recommended).ToString());

            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 8f;

            actions.Add(
                CreateSecondaryButton(
                    "Actualizar vista",
                    () => RunAction(
                        "Vista previa de adopción",
                        () => context.ProjectInventory.ScanAndPersist(),
                        false)));

            if (recommended > 0)
            {
                actions.Add(
                    CreateSecondaryButton(
                        "Adoptar recomendados (" +
                        recommended +
                        ")",
                        () =>
                        {
                            bool confirmed =
                                EditorUtility.DisplayDialog(
                                    "Adoptar contenido legacy",
                                    "Se registrarán " +
                                    recommended +
                                    " assets existentes como gestionados por SAVIC.\n\n" +
                                    "No se reemplazarán prefabs, GUID, materiales ni entradas del catálogo.",
                                    "Adoptar",
                                    "Cancelar");

                            if (!confirmed)
                                return;

                            RunAction(
                                "Adopción legacy por lote",
                                () =>
                                {
                                    SavicLegacyAdoptionBatchResult result =
                                        context.LegacyAdoption.AdoptRecommended();

                                    Debug.Log(
                                        "[SAVIC] LEGACY ADOPTION BATCH\n" +
                                        "Adopted: " + result.Adopted +
                                        "\nAlready managed: " +
                                        result.AlreadyManaged +
                                        "\nSkipped: " + result.Skipped);
                                },
                                true);
                        }));
            }

            summary.Add(actions);
            page.Add(summary);

            if (candidates.Count == 0)
            {
                page.Add(
                    CreateEmptyState(
                        "No hay contenido legacy pendiente de adopción."));
                contentHost.Add(page);
                return;
            }

            VisualElement detail = CreateDetailScroll();

            ListView list =
                CreateListView(
                    candidates,
                    CreateStandardRow,
                    BindCandidate);

            list.selectionChanged +=
                selection =>
                    RenderCandidateDetail(
                        detail,
                        FirstSelection
                            <SavicLegacyAdoptionCandidate>(
                                selection));

            page.Add(CreateSplit(list, detail));
            contentHost.Add(page);

            SelectFirstOrClear(
                list,
                candidates,
                detail,
                RenderCandidateDetail);

            void BindCandidate(
                VisualElement element,
                SavicLegacyAdoptionCandidate candidate)
            {
                string badge =
                    !candidate.Eligible
                        ? "BLOQUEADO"
                        : candidate.RecommendedForBatch
                            ? "LISTO"
                            : "MANUAL";

                BindStandardRow(
                    element,
                    candidate.Record?.displayName,
                    (candidate.Record?.category ?? "Sin categoría") +
                    " · " +
                    (candidate.Record?.itemId ?? "Sin ItemId"),
                    badge);
            }

            void RenderCandidateDetail(
                VisualElement host,
                SavicLegacyAdoptionCandidate candidate)
            {
                host.Clear();

                if (candidate?.Record == null)
                {
                    host.Add(
                        CreateEmptyState(
                            "Selecciona un asset legacy."));
                    return;
                }

                SavicProjectInventoryItemRecord record =
                    candidate.Record;

                string state =
                    !candidate.Eligible
                        ? "BLOQUEADO"
                        : candidate.RecommendedForBatch
                            ? "LISTO PARA ADOPTAR"
                            : "ADOPCIÓN MANUAL";

                AddDetailTitle(
                    host,
                    record.displayName,
                    state);

                host.Add(
                    CreateNotice(
                        candidate.RecommendedForBatch
                            ? "Sin cambios sobre el asset"
                            : candidate.Eligible
                                ? "Revisión manual"
                                : "No elegible",
                        candidate.Reason,
                        candidate.Eligible
                            ? candidate.RecommendedForBatch
                                ? Pass
                                : Warning
                            : Error));

                AddField(host, "ItemId", record.itemId);
                AddField(host, "SavicId propuesto", candidate.ProposedSavicId);
                AddField(host, "Categoría", record.category);
                AddField(host, "Placeable", record.itemAssetPath);
                AddField(host, "Prefab", record.prefabAssetPath);
                AddField(host, "Prefab GUID", record.prefabGuid);
                AddField(host, "Baseline", record.dependencyHash);

                VisualElement candidateActions =
                    new VisualElement();

                candidateActions.style.flexDirection =
                    FlexDirection.Row;
                candidateActions.style.flexWrap =
                    Wrap.Wrap;
                candidateActions.style.marginTop = 8f;

                UnityEngine.Object asset =
                    AssetDatabase.LoadMainAssetAtPath(
                        record.itemAssetPath);

                if (asset != null)
                {
                    candidateActions.Add(
                        CreateSecondaryButton(
                            "Localizar asset",
                            () => EditorGUIUtility.PingObject(asset)));
                }

                if (candidate.Eligible)
                {
                    candidateActions.Add(
                        CreateSecondaryButton(
                            "Adoptar este asset",
                            () =>
                            {
                                bool confirmed =
                                    EditorUtility.DisplayDialog(
                                        "Adoptar " +
                                        record.displayName,
                                        "SAVIC registrará el asset existente in-place.\n\n" +
                                        "No sustituirá su prefab, GUID, materiales, imágenes ni ItemId.",
                                        "Adoptar",
                                        "Cancelar");

                                if (!confirmed)
                                    return;

                                RunAction(
                                    "Adopción de " +
                                    record.itemId,
                                    () =>
                                    {
                                        SavicLegacyAdoptionResult result =
                                            context.LegacyAdoption.AdoptOne(
                                                record.itemAssetPath);

                                        if (!result.Succeeded)
                                        {
                                            throw new InvalidOperationException(
                                                result.Message);
                                        }

                                        Debug.Log(
                                            "[SAVIC] LEGACY ADOPTION - PASS\n" +
                                            record.itemId +
                                            "\nSavicId: " +
                                            result.SavicId +
                                            "\n" +
                                            result.Message);
                                    },
                                    true);
                            }));
                }

                host.Add(candidateActions);
            }
        }

        private void RenderValidation()
        {
            VisualElement page = CreatePage();
            AddSectionTitle(
                page,
                "Validación",
                "Resultados del pipeline y del inventario, priorizados por severidad.");

            VisualElement filters = CreateFilterBar();
            TextField search = CreateSearchField(validationSearch);

            List<string> resultChoices =
                BuildChoices(snapshot.Validations, row => row.Result);

            List<string> severityChoices =
                BuildChoices(snapshot.Validations, row => row.Severity);

            List<string> sourceChoices =
                BuildChoices(snapshot.Validations, row => row.Source);

            validationResult = NormalizeChoice(validationResult, resultChoices);
            validationSeverity = NormalizeChoice(validationSeverity, severityChoices);
            validationSource = NormalizeChoice(validationSource, sourceChoices);

            DropdownField result =
                CreateDropdown("Resultado", resultChoices, validationResult);

            DropdownField severity =
                CreateDropdown(
                    "Severidad",
                    severityChoices,
                    validationSeverity);

            DropdownField source =
                CreateDropdown("Origen", sourceChoices, validationSource);

            filters.Add(search);
            filters.Add(result);
            filters.Add(severity);
            filters.Add(source);
            page.Add(filters);

            List<SavicEditorValidationRow> visible =
                SavicEditorReadModel.FilterValidations(
                    snapshot.Validations,
                    validationSearch,
                    validationResult,
                    validationSeverity,
                    validationSource);

            VisualElement detail = CreateDetailScroll();
            ListView list =
                CreateListView(
                    visible,
                    CreateStandardRow,
                    BindValidationRow);

            list.selectionChanged +=
                selection =>
                    RenderValidationDetail(
                        detail,
                        FirstSelection<SavicEditorValidationRow>(selection));

            page.Add(CreateSplit(list, detail));
            contentHost.Add(page);

            void ApplyFilters()
            {
                visible = SavicEditorReadModel.FilterValidations(
                    snapshot.Validations,
                    validationSearch,
                    validationResult,
                    validationSeverity,
                    validationSource);

                SetItems(list, visible);
                SelectFirstOrClear(list, visible, detail, RenderValidationDetail);
            }

            search.RegisterValueChangedCallback(
                change =>
                {
                    validationSearch = change.newValue ?? string.Empty;
                    ApplyFilters();
                });

            result.RegisterValueChangedCallback(
                change =>
                {
                    validationResult = change.newValue ?? "Todos";
                    ApplyFilters();
                });

            severity.RegisterValueChangedCallback(
                change =>
                {
                    validationSeverity = change.newValue ?? "Todos";
                    ApplyFilters();
                });

            source.RegisterValueChangedCallback(
                change =>
                {
                    validationSource = change.newValue ?? "Todos";
                    ApplyFilters();
                });

            SelectFirstOrClear(list, visible, detail, RenderValidationDetail);
        }

        private void RenderHistory()
        {
            VisualElement page = CreatePage();
            AddSectionTitle(
                page,
                "Historial",
                "Cronología unificada de assets y trabajos de ingesta.");

            VisualElement filters = CreateFilterBar();
            TextField search = CreateSearchField(historySearch);
            List<string> kindChoices =
                BuildChoices(snapshot.History, row => row.Kind);

            historyKind = NormalizeChoice(historyKind, kindChoices);
            DropdownField kind =
                CreateDropdown("Tipo", kindChoices, historyKind);

            filters.Add(search);
            filters.Add(kind);
            page.Add(filters);

            List<SavicEditorHistoryRow> visible =
                SavicEditorReadModel.FilterHistory(
                    snapshot.History,
                    historySearch,
                    historyKind);

            ListView list =
                CreateListView(
                    visible,
                    CreateStandardRow,
                    BindHistoryRow);

            list.selectionType = SelectionType.None;
            page.Add(list);
            contentHost.Add(page);

            void ApplyFilters()
            {
                visible = SavicEditorReadModel.FilterHistory(
                    snapshot.History,
                    historySearch,
                    historyKind);

                SetItems(list, visible);
            }

            search.RegisterValueChangedCallback(
                change =>
                {
                    historySearch = change.newValue ?? string.Empty;
                    ApplyFilters();
                });

            kind.RegisterValueChangedCallback(
                change =>
                {
                    historyKind = change.newValue ?? "Todos";
                    ApplyFilters();
                });
        }

        private void RenderSettings()
        {
            ScrollView page = CreatePageScroll();
            AddSectionTitle(
                page,
                "Ajustes",
                "Configuración efectiva y ubicaciones de trabajo de SAVIC.");

            VisualElement versions = CreatePanel();
            AddGroupTitle(versions, "Versiones");
            AddField(versions, "Producto SAVIC", SavicVersion.ProductVersion);
            AddField(versions, "Pipeline", SavicVersion.PipelineVersion);
            AddField(
                versions,
                "Manifest schema",
                SavicVersion.ManifestSchemaVersion.ToString());
            AddField(
                versions,
                "Queue schema",
                SavicVersion.QueueSchemaVersion.ToString());
            AddField(
                versions,
                "Inventory scanner",
                SavicProjectInventoryService.Version);
            page.Add(versions);

            VisualElement state = CreatePanel();
            AddGroupTitle(state, "Estado");
            AddField(state, "Manifests", snapshot.Assets.Count.ToString());
            AddField(state, "Jobs", snapshot.Jobs.Count.ToString());
            AddField(state, "Validaciones", snapshot.Validations.Count.ToString());
            AddField(
                state,
                "Inicialización",
                string.IsNullOrWhiteSpace(
                    SavicEditorBootstrap.LastInitializationError)
                    ? "OK"
                    : SavicEditorBootstrap.LastInitializationError);
            page.Add(state);

            VisualElement paths = CreatePanel();
            AddGroupTitle(paths, "Rutas");
            AddField(paths, "Proyecto", context?.Layout.ProjectRoot ?? string.Empty);
            AddField(paths, "DropHere", context?.Layout.DropHereRoot ?? string.Empty);
            AddField(paths, "ContentSource", context?.Layout.ContentSourceRoot ?? string.Empty);
            AddField(paths, "Manifests", context?.Layout.ManifestsRoot ?? string.Empty);
            AddField(paths, "Runtime", context?.Layout.RuntimeRoot ?? string.Empty);

            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 10f;

            actions.Add(
                CreateSecondaryButton(
                    "Abrir DropHere",
                    () => RevealPath(context.Layout.DropHereRoot)));

            actions.Add(
                CreateSecondaryButton(
                    "Abrir ContentSource",
                    () => RevealPath(context.Layout.ContentSourceRoot)));

            actions.Add(
                CreateSecondaryButton(
                    "Abrir manifests",
                    () => RevealPath(context.Layout.ManifestsRoot)));

            actions.Add(
                CreateSecondaryButton(
                    "Abrir runtime",
                    () => RevealPath(context.Layout.RuntimeRoot)));

            paths.Add(actions);
            page.Add(paths);
            contentHost.Add(page);
        }

        private void RenderJobDetail(
            VisualElement detail,
            SavicEditorJobRow row)
        {
            detail.Clear();

            if (row == null)
            {
                detail.Add(CreateEmptyState("No hay trabajos para mostrar."));
                return;
            }

            AddDetailTitle(detail, row.DisplayName, row.State);
            AddField(detail, "JobId", row.JobId);
            AddField(detail, "SavicId", row.Job?.manifestSavicId);
            AddField(detail, "Estado", row.State);
            AddField(detail, "Checkpoint", row.Job?.checkpoint);
            AddField(detail, "Intentos", (row.Job?.attempts ?? 0).ToString());
            AddField(
                detail,
                "Duración última operación",
                (row.Job?.lastDurationMilliseconds ?? 0) + " ms");
            AddField(
                detail,
                "Cancelación solicitada",
                row.Job?.cancelRequested == true ? "Sí" : "No");
            AddField(detail, "Actualizado", FormatTimestamp(row.UpdatedUtc));
            AddField(detail, "Hash", row.Job?.sourceHash);
            AddField(detail, "Mensaje", row.Message);
            AddField(detail, "Archivo", row.Job?.archivedRelativePath);

            string archivedPath =
                context.Layout.FromProjectRelativePath(
                    row.Job?.archivedRelativePath);

            if (!string.IsNullOrWhiteSpace(archivedPath) &&
                File.Exists(archivedPath))
            {
                detail.Add(
                    CreateSecondaryButton(
                        "Mostrar fuente archivada",
                        () => RevealPath(archivedPath)));
            }

            if (row.Job?.batchEligible == true &&
                IsCancellableJobState(row.State))
            {
                detail.Add(
                    CreateSecondaryButton(
                        "Cancelar trabajo",
                        () => RunAction(
                            "Cancelación de trabajo",
                            () => context.Jobs.RequestCancel(
                                row.JobId),
                            false)));
            }
        }

        private void RenderReviewDetail(
            VisualElement detail,
            SavicEditorReviewRow row)
        {
            detail.Clear();

            if (row == null)
            {
                detail.Add(CreateEmptyState("No hay excepciones pendientes."));
                return;
            }

            AddDetailTitle(detail, row.DisplayName, row.Severity);
            detail.Add(
                CreateNotice(
                    "Motivo de revisión",
                    row.Issue,
                    StatusColor(row.Severity)));

            AddField(detail, "Origen", row.Source);
            AddField(detail, "Estado", row.Status);
            AddField(detail, "SavicId", row.SavicId);
            AddField(detail, "Actualizado", FormatTimestamp(row.UpdatedUtc));
            AddField(detail, "Asset", row.AssetPath);

            AddRevealActions(
                detail,
                row.Manifest,
                row.AssetPath);

            if (row.Manifest != null)
            {
                Foldout technical = new Foldout
                {
                    text = "Contexto técnico",
                    value = false
                };

                AddCompactManifestContext(technical, row.Manifest);
                detail.Add(technical);
            }
        }

        private void RenderAssetDetail(
            VisualElement detail,
            SavicEditorAssetRow row)
        {
            detail.Clear();

            if (row?.Manifest == null)
            {
                detail.Add(CreateEmptyState("No hay assets para mostrar."));
                return;
            }

            SavicManifest manifest = row.Manifest;
            AddDetailTitle(detail, row.DisplayName, row.Status);
            AddPreview(detail, manifest);

            VisualElement identity = CreatePanel();
            AddGroupTitle(identity, "Identidad");
            AddField(identity, "SavicId", row.SavicId);
            AddField(identity, "ContentId", row.CanonicalContentId);
            AddField(identity, "Estado", row.Status);
            AddField(identity, "Fuente", manifest.source?.originalFileName);
            AddField(identity, "Actualizado", FormatTimestamp(row.UpdatedUtc));
            detail.Add(identity);

            VisualElement classification = CreatePanel();
            AddGroupTitle(classification, "Clasificación");
            AddField(classification, "Familia", row.Family);
            AddField(classification, "Tipo", row.Type);
            AddField(classification, "Categoría", row.Category);
            AddField(
                classification,
                "Confianza",
                manifest.classification == null
                    ? "Sin dato"
                    : manifest.classification.confidence +
                      " · " +
                      manifest.classification.score.ToString(
                          "0.000",
                          CultureInfo.InvariantCulture));
            detail.Add(classification);

            AddMeasures(detail, manifest);
            AddMaterials(detail, manifest);
            AddParts(detail, manifest);
            AddIntegrations(detail, manifest);
            AddValidationSummary(detail, manifest);
            AddRevealActions(detail, manifest, string.Empty);

            Foldout advanced = new Foldout
            {
                text = "Detalles técnicos avanzados",
                value = false
            };

            AddCompactManifestContext(advanced, manifest);
            AddArtifacts(advanced, manifest);
            detail.Add(advanced);
        }

        private void RenderValidationDetail(
            VisualElement detail,
            SavicEditorValidationRow row)
        {
            detail.Clear();

            if (row == null)
            {
                detail.Add(CreateEmptyState("No hay validaciones para mostrar."));
                return;
            }

            AddDetailTitle(detail, row.ValidationId, row.Result);
            detail.Add(
                CreateNotice(
                    row.Severity,
                    row.Message,
                    StatusColor(row.Severity)));

            AddField(detail, "Asset", row.DisplayName);
            AddField(detail, "SavicId", row.SavicId);
            AddField(detail, "Origen", row.Source);
            AddField(detail, "Resultado", row.Result);
            AddField(detail, "Severidad", row.Severity);
            AddField(detail, "Validador", row.ValidatorVersion);
            AddField(detail, "Actualizado", FormatTimestamp(row.UpdatedUtc));
            AddField(detail, "Ruta", row.AssetPath);
            AddRevealActions(detail, row.Manifest, row.AssetPath);
        }

        private void AddMeasures(
            VisualElement parent,
            SavicManifest manifest)
        {
            SavicModelAnalysisRecord model = manifest.model3D;
            VisualElement group = CreatePanel();
            AddGroupTitle(group, "Medidas");

            if (model == null || !model.analyzed)
            {
                AddField(group, "Estado", "Sin análisis 3D");
                parent.Add(group);
                return;
            }

            AddField(
                group,
                "Ancho × alto × fondo",
                FormatMeters(model.widthMeters) +
                " × " +
                FormatMeters(model.heightMeters) +
                " × " +
                FormatMeters(model.depthMeters));
            AddField(group, "Meshes", model.meshInstanceCount.ToString());
            AddField(group, "Vértices", model.vertexCount.ToString("N0"));
            AddField(group, "Triángulos", model.triangleCount.ToString("N0"));
            parent.Add(group);
        }

        private void AddMaterials(
            VisualElement parent,
            SavicManifest manifest)
        {
            VisualElement group = CreatePanel();
            AddGroupTitle(group, "Materiales");

            List<SavicMaterialAnalysisRecord> materials =
                manifest.model3D?.materials;

            if (materials == null || materials.Count == 0)
            {
                AddField(group, "Estado", "Sin materiales analizados");
                parent.Add(group);
                return;
            }

            AddField(group, "Slots", manifest.model3D.materialSlotCount.ToString());
            AddField(group, "Únicos", manifest.model3D.uniqueMaterialCount.ToString());
            AddField(group, "Semántica dominante", manifest.model3D.dominantMaterialSemantic);

            for (int index = 0;
                 index < Math.Min(materials.Count, 8);
                 index++)
            {
                SavicMaterialAnalysisRecord material = materials[index];
                if (material == null)
                    continue;

                AddField(
                    group,
                    FirstNonEmpty(material.materialName, "Material " + (index + 1)),
                    FirstNonEmpty(material.semantic, "Unknown") +
                    " · " +
                    FirstNonEmpty(material.semanticConfidence, "UNKNOWN"));
            }

            if (materials.Count > 8)
            {
                AddField(
                    group,
                    "Otros",
                    (materials.Count - 8) + " materiales adicionales");
            }

            parent.Add(group);
        }

        private void AddParts(
            VisualElement parent,
            SavicManifest manifest)
        {
            VisualElement group = CreatePanel();
            AddGroupTitle(group, "Piezas semánticas");

            SavicSemanticPartAnalysisRecord analysis =
                manifest.model3D?.semanticParts;

            if (analysis?.parts == null || analysis.parts.Count == 0)
            {
                AddField(group, "Estado", "Sin piezas semánticas");
                parent.Add(group);
                return;
            }

            AddField(
                group,
                "Automation ready",
                analysis.automationReady ? "Sí" : "No");
            AddField(
                group,
                "Cobertura",
                analysis.semanticCoverage.ToString(
                    "P1",
                    CultureInfo.InvariantCulture));

            for (int index = 0;
                 index < analysis.parts.Count;
                 index++)
            {
                SavicSemanticPartRecord part = analysis.parts[index];
                if (part == null)
                    continue;

                AddField(
                    group,
                    FirstNonEmpty(part.partId, "Parte " + (index + 1)),
                    FirstNonEmpty(part.role, "Unresolved") +
                    " · " +
                    FirstNonEmpty(part.confidence, "UNKNOWN") +
                    " · " +
                    part.areaFraction.ToString(
                        "P1",
                        CultureInfo.InvariantCulture));
            }

            parent.Add(group);
        }

        private void AddIntegrations(
            VisualElement parent,
            SavicManifest manifest)
        {
            VisualElement group = CreatePanel();
            AddGroupTitle(group, "Integraciones");

            string contentType =
                string.IsNullOrWhiteSpace(manifest.type) ||
                string.Equals(
                    manifest.type,
                    "Unknown",
                    StringComparison.OrdinalIgnoreCase)
                    ? manifest.classification?.type
                    : manifest.type;

            if (string.Equals(
                    contentType,
                    "Table",
                    StringComparison.OrdinalIgnoreCase))
            {
                AddField(
                    group,
                    "BBSIS",
                    manifest.tableSpatial?.validated == true
                        ? "PASS · " + manifest.tableSpatial.contractId
                        : "Pendiente");
                AddField(
                    group,
                    "Navigation",
                    manifest.tableNavigation?.validated == true
                        ? "PASS"
                        : "Pendiente");
                AddField(
                    group,
                    "Save/Load",
                    manifest.tablePersistence?.validated == true
                        ? "PASS"
                        : "Pendiente");
            }
            else if (string.Equals(
                         contentType,
                         "Chair",
                         StringComparison.OrdinalIgnoreCase))
            {
                AddField(
                    group,
                    "BBSIS",
                    manifest.chairSpatial?.validated == true
                        ? "PASS · " + manifest.chairSpatial.contractId
                        : "Pendiente");
                AddField(
                    group,
                    "Navigation",
                    manifest.chairNavigation?.validated == true
                        ? "PASS"
                        : "Pendiente");
                AddField(
                    group,
                    "Save/Load",
                    manifest.chairPersistence?.validated == true
                        ? "PASS"
                        : "Pendiente");
            }
            else
            {
                AddField(group, "Estado", "Sin adaptador de familia publicado");
            }

            parent.Add(group);
        }

        private void AddValidationSummary(
            VisualElement parent,
            SavicManifest manifest)
        {
            VisualElement group = CreatePanel();
            AddGroupTitle(group, "Validación");

            List<SavicValidationRecord> validations =
                manifest.validations;

            if (validations == null || validations.Count == 0)
            {
                AddField(group, "Estado", "Sin validaciones registradas");
                parent.Add(group);
                return;
            }

            int passes = validations.Count(
                validation =>
                    validation != null &&
                    string.Equals(
                        validation.result,
                        "PASS",
                        StringComparison.OrdinalIgnoreCase));

            int failures = validations.Count - passes;
            AddField(group, "PASS", passes.ToString());
            AddField(group, "No PASS", failures.ToString());

            foreach (
                SavicValidationRecord validation in validations
                    .Where(item => item != null)
                    .Take(10))
            {
                AddField(
                    group,
                    validation.validationId,
                    validation.result +
                    (string.IsNullOrWhiteSpace(validation.message)
                        ? string.Empty
                        : " · " + validation.message));
            }

            if (validations.Count > 10)
            {
                AddField(
                    group,
                    "Otros",
                    (validations.Count - 10) + " resultados adicionales");
            }

            parent.Add(group);
        }

        private void AddPreview(
            VisualElement parent,
            SavicManifest manifest)
        {
            string previewPath =
                FindArtifactPath(manifest, "preview.large");

            Texture2D preview =
                string.IsNullOrWhiteSpace(previewPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<Texture2D>(previewPath);

            if (preview == null)
                return;

            Image image = new Image
            {
                image = preview,
                scaleMode = ScaleMode.ScaleToFit
            };

            image.style.height = 220f;
            image.style.marginBottom = 10f;
            image.style.backgroundColor = Panel;
            image.style.borderTopWidth = 1f;
            image.style.borderBottomWidth = 1f;
            image.style.borderLeftWidth = 1f;
            image.style.borderRightWidth = 1f;
            image.style.borderTopColor = Border;
            image.style.borderBottomColor = Border;
            image.style.borderLeftColor = Border;
            image.style.borderRightColor = Border;
            parent.Add(image);
        }

        private void AddCompactManifestContext(
            VisualElement parent,
            SavicManifest manifest)
        {
            AddField(parent, "SavicId", manifest.savicId);
            AddField(parent, "ContentId", manifest.canonicalContentId);
            AddField(parent, "SourceHash", manifest.source?.sourceHash);
            AddField(parent, "Archivo archivado", manifest.source?.archivedRelativePath);
            AddField(parent, "SAVIC version", manifest.savicVersion);
            AddField(parent, "Pipeline version", manifest.pipelineVersion);
            AddField(parent, "Schema", manifest.schemaVersion.ToString());
            AddField(parent, "Creado", FormatTimestamp(manifest.createdUtc));
            AddField(parent, "Actualizado", FormatTimestamp(manifest.updatedUtc));
        }

        private void AddArtifacts(
            VisualElement parent,
            SavicManifest manifest)
        {
            if (manifest.artifacts == null || manifest.artifacts.Count == 0)
                return;

            AddSubheading(parent, "Artefactos");

            foreach (
                SavicArtifactRecord artifact in manifest.artifacts
                    .Where(item => item != null))
            {
                AddField(
                    parent,
                    artifact.role,
                    artifact.projectRelativePath +
                    (string.IsNullOrWhiteSpace(artifact.builderVersion)
                        ? string.Empty
                        : " · " + artifact.builderVersion));
            }
        }

        private void AddRevealActions(
            VisualElement parent,
            SavicManifest manifest,
            string assetPath)
        {
            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 8f;
            bool hasActions = false;

            if (manifest != null &&
                context.Manifests.TryGetManifestPath(
                    manifest.savicId,
                    out string manifestPath) &&
                File.Exists(manifestPath))
            {
                actions.Add(
                    CreateSecondaryButton(
                        "Mostrar manifest",
                        () => RevealPath(manifestPath)));
                hasActions = true;
            }

            string archivedPath =
                manifest?.source == null
                    ? string.Empty
                    : context.Layout.FromProjectRelativePath(
                        manifest.source.archivedRelativePath);

            if (!string.IsNullOrWhiteSpace(archivedPath) &&
                File.Exists(archivedPath))
            {
                actions.Add(
                    CreateSecondaryButton(
                        "Mostrar fuente",
                        () => RevealPath(archivedPath)));
                hasActions = true;
            }

            string projectAssetPath =
                FirstNonEmpty(
                    assetPath,
                    FindArtifactPath(manifest, "catalog.item_definition"),
                    FindArtifactPath(manifest, "published.table.prefab"),
                    FindArtifactPath(manifest, "published.chair.prefab"));

            UnityEngine.Object asset =
                string.IsNullOrWhiteSpace(projectAssetPath)
                    ? null
                    : AssetDatabase.LoadMainAssetAtPath(projectAssetPath);

            if (asset != null)
            {
                actions.Add(
                    CreateSecondaryButton(
                        "Localizar asset",
                        () => EditorGUIUtility.PingObject(asset)));
                hasActions = true;
            }

            if (hasActions)
                parent.Add(actions);
        }

        private static bool IsCancellableJobState(
            string state)
        {
            return string.Equals(
                       state,
                       SavicJobState.Waiting.ToString(),
                       StringComparison.Ordinal) ||
                   string.Equals(
                       state,
                       SavicJobState.Hashing.ToString(),
                       StringComparison.Ordinal) ||
                   string.Equals(
                       state,
                       SavicJobState.Ingested.ToString(),
                       StringComparison.Ordinal) ||
                   string.Equals(
                       state,
                       SavicJobState.Processing.ToString(),
                       StringComparison.Ordinal);
        }

        private void RunAction(
            string label,
            Action action,
            bool reloadFromDisk)
        {
            try
            {
                context ??= SavicEditorContext.Instance;
                action?.Invoke();
                SetFooter(label + " completado.", Pass);
                RequestRefresh(reloadFromDisk);
            }
            catch (Exception exception)
            {
                SetFooter(label + " falló: " + exception.Message, Error);
                Debug.LogError(
                    "[SAVIC] " + label + " failed safely: " + exception);
            }
        }

        private static void RevealPath(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) &&
                (File.Exists(path) || Directory.Exists(path)))
            {
                EditorUtility.RevealInFinder(path);
            }
        }

        private void SetFooter(string text, Color color)
        {
            if (footerLabel == null)
                return;

            footerLabel.text = text ?? string.Empty;
            footerLabel.style.color = color;
        }

        private static VisualElement CreatePage()
        {
            VisualElement page = new VisualElement();
            page.style.flexGrow = 1f;
            page.style.minWidth = 0f;
            page.style.minHeight = 0f;
            page.style.paddingLeft = 16f;
            page.style.paddingRight = 16f;
            page.style.paddingTop = 14f;
            page.style.paddingBottom = 12f;
            return page;
        }

        private static ScrollView CreatePageScroll()
        {
            ScrollView page = new ScrollView(ScrollViewMode.Vertical);
            page.style.flexGrow = 1f;
            page.style.paddingLeft = 16f;
            page.style.paddingRight = 16f;
            page.style.paddingTop = 14f;
            page.style.paddingBottom = 12f;
            return page;
        }

        private static void AddSectionTitle(
            VisualElement parent,
            string title,
            string subtitle)
        {
            Label titleLabel = new Label(title);
            titleLabel.style.fontSize = 18f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = TextPrimary;
            parent.Add(titleLabel);

            Label subtitleLabel = new Label(subtitle);
            subtitleLabel.style.fontSize = 11f;
            subtitleLabel.style.color = TextMuted;
            subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
            subtitleLabel.style.marginTop = 2f;
            parent.Add(subtitleLabel);
        }

        private static void AddDetailTitle(
            VisualElement parent,
            string title,
            string status)
        {
            Label titleLabel = new Label(FirstNonEmpty(title, "Sin título"));
            titleLabel.style.fontSize = 16f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = TextPrimary;
            titleLabel.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(titleLabel);

            Label statusLabel = new Label(FirstNonEmpty(status, "Sin estado"));
            statusLabel.style.fontSize = 10f;
            statusLabel.style.color = StatusColor(status);
            statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            statusLabel.style.marginTop = 3f;
            statusLabel.style.marginBottom = 10f;
            parent.Add(statusLabel);
        }

        private static void AddSubheading(
            VisualElement parent,
            string text)
        {
            Label label = new Label(text);
            label.style.fontSize = 13f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = TextPrimary;
            label.style.marginTop = 12f;
            label.style.marginBottom = 6f;
            parent.Add(label);
        }

        private static VisualElement CreateMetricCard(
            string label,
            int value,
            Color color)
        {
            VisualElement card = CreatePanel();
            card.style.width = 154f;
            card.style.minHeight = 82f;
            card.style.marginRight = 8f;
            card.style.marginBottom = 8f;

            Label valueLabel = new Label(value.ToString());
            valueLabel.style.fontSize = 26f;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.color = color;
            card.Add(valueLabel);

            Label nameLabel = new Label(label);
            nameLabel.style.fontSize = 10f;
            nameLabel.style.color = TextMuted;
            card.Add(nameLabel);
            return card;
        }

        private static VisualElement CreatePanel()
        {
            VisualElement panel = new VisualElement();
            panel.style.backgroundColor = Panel;
            panel.style.borderTopWidth = 1f;
            panel.style.borderBottomWidth = 1f;
            panel.style.borderLeftWidth = 1f;
            panel.style.borderRightWidth = 1f;
            panel.style.borderTopColor = Border;
            panel.style.borderBottomColor = Border;
            panel.style.borderLeftColor = Border;
            panel.style.borderRightColor = Border;
            panel.style.paddingLeft = 12f;
            panel.style.paddingRight = 12f;
            panel.style.paddingTop = 10f;
            panel.style.paddingBottom = 10f;
            panel.style.marginBottom = 10f;
            return panel;
        }

        private static void AddGroupTitle(
            VisualElement group,
            string title)
        {
            Label label = new Label(title);
            label.style.fontSize = 12f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = Accent;
            label.style.marginBottom = 7f;
            group.Add(label);
        }

        private static void AddField(
            VisualElement parent,
            string label,
            string value)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4f;

            Label key = new Label(FirstNonEmpty(label, "Dato"));
            key.style.width = 142f;
            key.style.flexShrink = 0f;
            key.style.fontSize = 10f;
            key.style.color = TextMuted;
            row.Add(key);

            Label data = new Label(FirstNonEmpty(value, "—"));
            data.style.flexGrow = 1f;
            data.style.minWidth = 0f;
            data.style.fontSize = 10f;
            data.style.color = TextPrimary;
            data.style.whiteSpace = WhiteSpace.Normal;
            data.tooltip = value ?? string.Empty;
            row.Add(data);
            parent.Add(row);
        }

        private static VisualElement CreateNotice(
            string title,
            string message,
            Color color)
        {
            VisualElement notice = CreatePanel();
            notice.style.borderLeftWidth = 4f;
            notice.style.borderLeftColor = color;

            Label titleLabel = new Label(title);
            titleLabel.style.fontSize = 12f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = color;
            notice.Add(titleLabel);

            Label messageLabel = new Label(FirstNonEmpty(message, "Sin detalle"));
            messageLabel.style.fontSize = 10f;
            messageLabel.style.color = TextPrimary;
            messageLabel.style.whiteSpace = WhiteSpace.Normal;
            messageLabel.style.marginTop = 4f;
            notice.Add(messageLabel);
            return notice;
        }

        private static VisualElement CreateEmptyState(string message)
        {
            Label label = new Label(message);
            label.style.flexGrow = 1f;
            label.style.minHeight = 100f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.color = TextMuted;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static VisualElement CreateFilterBar()
        {
            VisualElement filters = new VisualElement();
            filters.style.flexDirection = FlexDirection.Row;
            filters.style.flexWrap = Wrap.Wrap;
            filters.style.alignItems = Align.FlexEnd;
            filters.style.marginTop = 10f;
            filters.style.marginBottom = 10f;
            return filters;
        }

        private static TextField CreateSearchField(string value)
        {
            TextField search = new TextField("Buscar");
            search.value = value ?? string.Empty;
            search.style.width = 230f;
            search.style.marginRight = 8f;
            search.style.marginBottom = 4f;
            return search;
        }

        private static DropdownField CreateDropdown(
            string label,
            List<string> choices,
            string value)
        {
            int index = Math.Max(0, choices.IndexOf(value));
            DropdownField dropdown =
                new DropdownField(label, choices, index);

            dropdown.style.width = 155f;
            dropdown.style.marginRight = 8f;
            dropdown.style.marginBottom = 4f;
            return dropdown;
        }

        private static VisualElement CreateSplit(
            VisualElement list,
            VisualElement detail)
        {
            VisualElement split = new VisualElement();
            split.style.flexDirection = FlexDirection.Row;
            split.style.flexGrow = 1f;
            split.style.minHeight = 0f;

            VisualElement left = new VisualElement();
            left.style.width = 460f;
            left.style.minWidth = 330f;
            left.style.flexShrink = 1f;
            left.style.marginRight = 10f;
            left.Add(list);

            split.Add(left);
            split.Add(detail);
            return split;
        }

        private static ScrollView CreateDetailScroll()
        {
            ScrollView detail = new ScrollView(ScrollViewMode.Vertical);
            detail.style.flexGrow = 1f;
            detail.style.minWidth = 300f;
            detail.style.paddingLeft = 12f;
            detail.style.paddingRight = 8f;
            detail.style.paddingTop = 8f;
            detail.style.paddingBottom = 12f;
            detail.style.backgroundColor = PanelRaised;
            detail.style.borderLeftWidth = 1f;
            detail.style.borderLeftColor = Border;
            return detail;
        }

        private static ListView CreateListView<T>(
            List<T> items,
            Func<VisualElement> makeItem,
            Action<VisualElement, T> bindItem)
        {
            ListView list = new ListView();
            list.itemsSource = items;
            list.makeItem = makeItem;
            list.bindItem =
                (element, index) =>
                {
                    IList source = list.itemsSource;

                    if (source != null &&
                        index >= 0 &&
                        index < source.Count &&
                        source[index] is T item)
                    {
                        bindItem(element, item);
                    }
                };

            ConfigureStandardRowUnbinding(list);

            ConfigureVirtualizedList(list, ListRowHeight);
            list.style.flexGrow = 1f;
            list.style.minHeight = 0f;
            list.style.backgroundColor = Panel;
            list.style.borderTopWidth = 1f;
            list.style.borderBottomWidth = 1f;
            list.style.borderLeftWidth = 1f;
            list.style.borderRightWidth = 1f;
            list.style.borderTopColor = Border;
            list.style.borderBottomColor = Border;
            list.style.borderLeftColor = Border;
            list.style.borderRightColor = Border;
            return list;
        }

        private static void ClearRowLabel(
            VisualElement element,
            string name)
        {
            Label label = element?.Q<Label>(name);
            if (label == null)
                return;

            label.text = string.Empty;
            label.tooltip = string.Empty;
        }

        private static VisualElement CreateStandardRow()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 10f;
            row.style.paddingRight = 10f;
            row.style.paddingTop = 5f;
            row.style.paddingBottom = 5f;

            VisualElement text = new VisualElement();
            text.style.flexGrow = 1f;
            text.style.minWidth = 0f;

            Label title = new Label();
            title.name = "title";
            title.style.fontSize = 11f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = TextPrimary;
            title.style.whiteSpace = WhiteSpace.NoWrap;
            text.Add(title);

            Label subtitle = new Label();
            subtitle.name = "subtitle";
            subtitle.style.fontSize = 9f;
            subtitle.style.color = TextMuted;
            subtitle.style.whiteSpace = WhiteSpace.NoWrap;
            text.Add(subtitle);

            row.Add(text);

            Label badge = new Label();
            badge.name = "badge";
            badge.style.fontSize = 9f;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.marginLeft = 8f;
            badge.style.flexShrink = 0f;
            row.Add(badge);
            return row;
        }

        private static void BindStandardRow(
            VisualElement element,
            string title,
            string subtitle,
            string badge)
        {
            Label titleLabel = element.Q<Label>("title");
            Label subtitleLabel = element.Q<Label>("subtitle");
            Label badgeLabel = element.Q<Label>("badge");

            titleLabel.text = FirstNonEmpty(title, "Sin título");
            titleLabel.tooltip = titleLabel.text;
            subtitleLabel.text = subtitle ?? string.Empty;
            subtitleLabel.tooltip = subtitleLabel.text;
            badgeLabel.text = FirstNonEmpty(badge, "Sin dato");
            badgeLabel.style.color = StatusColor(badge);
        }

        private static void BindAssetRow(
            VisualElement element,
            SavicEditorAssetRow row)
        {
            BindStandardRow(
                element,
                row.DisplayName,
                row.Type + " · " + row.Category + " · " + FormatTimestamp(row.UpdatedUtc),
                row.Status);
        }

        private static void BindJobRow(
            VisualElement element,
            SavicEditorJobRow row)
        {
            BindStandardRow(
                element,
                row.DisplayName,
                FormatTimestamp(row.UpdatedUtc) +
                (string.IsNullOrWhiteSpace(row.Message)
                    ? string.Empty
                    : " · " + row.Message),
                row.State);
        }

        private static void BindReviewRow(
            VisualElement element,
            SavicEditorReviewRow row)
        {
            BindStandardRow(
                element,
                row.DisplayName,
                row.Source + " · " + row.Issue,
                row.Severity);
        }

        private static void BindValidationRow(
            VisualElement element,
            SavicEditorValidationRow row)
        {
            BindStandardRow(
                element,
                row.ValidationId,
                row.DisplayName + " · " + row.Message,
                row.Result);
        }

        private static void BindHistoryRow(
            VisualElement element,
            SavicEditorHistoryRow row)
        {
            BindStandardRow(
                element,
                row.Title,
                row.Detail,
                FormatTimestamp(row.TimestampUtc));
        }

        private static void SetItems<T>(
            ListView list,
            List<T> items)
        {
            list.ClearSelection();
            list.itemsSource = items;
            list.RefreshItems();
        }

        private static void SelectFirstOrClear<T>(
            ListView list,
            List<T> rows,
            VisualElement detail,
            Action<VisualElement, T> render)
            where T : class
        {
            if (rows.Count == 0)
            {
                list.ClearSelection();
                render(detail, null);
                return;
            }

            list.selectedIndex = 0;
            render(detail, rows[0]);
        }

        private static T FirstSelection<T>(
            IEnumerable<object> selection)
            where T : class
        {
            if (selection == null)
                return null;

            foreach (object item in selection)
                return item as T;

            return null;
        }

        private static List<string> BuildChoices<T>(
            IEnumerable<T> rows,
            Func<T, string> selector)
        {
            List<string> choices = new List<string> { "Todos" };

            choices.AddRange(
                (rows ?? Array.Empty<T>())
                    .Select(selector)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));

            return choices;
        }

        private static string NormalizeChoice(
            string value,
            IReadOnlyCollection<string> choices)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Todos";

            foreach (string choice in choices)
            {
                if (string.Equals(
                        choice,
                        value,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return choice;
                }
            }

            return "Todos";
        }

        private static Button CreateHeaderButton(
            string text,
            Action action)
        {
            Button button = new Button(action)
            {
                text = text
            };

            button.style.height = 28f;
            button.style.marginLeft = 6f;
            return button;
        }

        private static Button CreateSecondaryButton(
            string text,
            Action action)
        {
            Button button = new Button(action)
            {
                text = text
            };

            button.style.height = 26f;
            button.style.marginRight = 6f;
            button.style.marginBottom = 6f;
            return button;
        }

        private static Color StatusColor(string value)
        {
            if (ContainsAny(
                    value,
                    "PASS",
                    "PUBLISHED",
                    "AUTO_CORRECTED",
                    "INGESTED",
                    "MANAGED_BY_SAVIC"))
            {
                return Pass;
            }

            if (ContainsAny(
                    value,
                    "FAIL",
                    "ERROR",
                    "QUARANTINED",
                    "BLOCKER"))
            {
                return Error;
            }

            if (ContainsAny(
                    value,
                    "REVIEW",
                    "WARNING",
                    "STALE",
                    "WAITING"))
            {
                return Warning;
            }

            return Accent;
        }

        private static bool ContainsAny(
            string value,
            params string[] candidates)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            for (int index = 0;
                 index < candidates.Length;
                 index++)
            {
                if (value.IndexOf(
                        candidates[index],
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FindArtifactPath(
            SavicManifest manifest,
            string role)
        {
            if (manifest?.artifacts == null)
                return string.Empty;

            for (int index = 0;
                 index < manifest.artifacts.Count;
                 index++)
            {
                SavicArtifactRecord artifact = manifest.artifacts[index];

                if (artifact != null &&
                    string.Equals(
                        artifact.role,
                        role,
                        StringComparison.Ordinal))
                {
                    return artifact.projectRelativePath ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static string FormatTimestamp(string value)
        {
            if (!DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal |
                    DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset timestamp))
            {
                return "Sin fecha";
            }

            return timestamp.ToString(
                "yyyy-MM-dd HH:mm 'UTC'",
                CultureInfo.InvariantCulture);
        }

        private static string FormatMeters(float value)
        {
            return value.ToString(
                       "0.###",
                       CultureInfo.InvariantCulture) +
                   " m";
        }

        private static string FirstNonEmpty(
            params string[] values)
        {
            for (int index = 0;
                 index < values.Length;
                 index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                    return values[index];
            }

            return string.Empty;
        }
    }
}
