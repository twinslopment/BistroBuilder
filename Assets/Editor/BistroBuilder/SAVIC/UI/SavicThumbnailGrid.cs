using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicThumbnailEntry
    {
        internal string Id, Title, Status, Type, PreviewPath;
        internal SavicCanonicalContentInventoryRow InventoryRow;
        internal static SavicThumbnailEntry FromInventory(SavicCanonicalContentInventoryRow row)
        {
            string path = "";
            foreach (string role in new[] { "preview.catalog", "preview.large" })
            {
                path = row.Manifest?.artifacts?.FirstOrDefault(a => a != null && a.role == role)?.projectRelativePath;
                if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) break;
                path = "";
            }
            return new SavicThumbnailEntry { Id = row.StableKey, Title = row.DisplayName, Status = row.Lifecycle,
                Type = row.Type, PreviewPath = path, InventoryRow = row };
        }
    }

    // Only visible rows retain Texture references. The AssetDatabase owns the images.
    internal sealed class SavicThumbnailGrid : VisualElement
    {
        internal const float RowHeight = 166f;
        internal const float CardMinimumWidth = 132f;
        internal const float Gap = 8f;
        private readonly ListView list;
        private readonly Label empty;
        private readonly Action<SavicThumbnailEntry> selected;
        private List<SavicThumbnailEntry> items = new List<SavicThumbnailEntry>();
        private int columns = 2;
        private string selectedId = "";
        internal int Columns => columns;
        internal int ItemCount => items.Count;
        internal string SelectedId => selectedId;
        internal ListView Rows => list;
        internal IReadOnlyList<SavicThumbnailEntry> Items => items;
        internal static int ColumnsForWidth(float width) => float.IsNaN(width) || float.IsInfinity(width)
            ? 1 : Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(0, width) - 22f + Gap) / (CardMinimumWidth + Gap)), 1, 6);

        internal SavicThumbnailGrid(Action<SavicThumbnailEntry> onSelected)
        {
            name = "savic-thumbnail-grid"; selected = onSelected;
            style.flexGrow = 1; style.minHeight = 0; style.minWidth = 0;
            empty = new Label("No hay assets que coincidan con los filtros.");
            empty.style.paddingTop = 24; empty.style.whiteSpace = WhiteSpace.Normal;
            empty.style.color = new Color(.67f, .67f, .60f); Add(empty);
            list = new ListView { name = "savic-thumbnail-rows", fixedItemHeight = RowHeight,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight, selectionType = SelectionType.None };
            list.style.flexGrow = 1; list.style.minHeight = 0;
            list.makeItem = MakeRow;
            list.bindItem = BindRow;
            list.unbindItem = (row, _) => ClearRow(row);
            Add(list);
            RegisterCallback<GeometryChangedEvent>(evt =>
            {
                int next = ColumnsForWidth(evt.newRect.width);
                if (columns == next) return;
                columns = next; RebuildRows();
            });
        }

        internal void SetItems(IEnumerable<SavicThumbnailEntry> entries, string preferredId = null)
        {
            items = (entries ?? Enumerable.Empty<SavicThumbnailEntry>()).Where(e => e != null).ToList();
            selectedId = items.Any(e => e.Id == (preferredId ?? selectedId)) ? preferredId ?? selectedId : items.FirstOrDefault()?.Id ?? "";
            RebuildRows(); selected?.Invoke(items.FirstOrDefault(e => e.Id == selectedId));
        }
        private void RebuildRows()
        {
            empty.style.display = items.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            list.style.display = items.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            list.itemsSource = Enumerable.Range(0, (items.Count + columns - 1) / columns).ToList();
            list.Rebuild();
        }
        private VisualElement MakeRow()
        {
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row;
            row.style.height = RowHeight; row.style.paddingBottom = Gap;
            for (int i = 0; i < columns; i++)
            {
                var card = new Button { name = "savic-thumbnail-card", text = "" };
                card.style.flexGrow = 1; card.style.flexBasis = 0; card.style.minWidth = 0;
                card.style.marginLeft = 0; card.style.marginRight = i < columns - 1 ? Gap : 0;
                card.style.marginTop = 0; card.style.marginBottom = 0;
                card.style.paddingLeft = 7; card.style.paddingRight = 7;
                card.style.paddingTop = 6; card.style.paddingBottom = 5;
                card.style.backgroundColor = new Color(.145f, .15f, .135f);
                card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 1;
                card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 5;
                var image = new Image { name = "thumbnail", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                image.style.height = 85; image.style.flexShrink = 0; card.Add(image);
                var missing = new Label("Sin miniatura") { name = "missing", pickingMode = PickingMode.Ignore };
                missing.style.height = 85; missing.style.unityTextAlign = TextAnchor.MiddleCenter;
                missing.style.color = new Color(.67f, .67f, .60f); card.Add(missing);
                var title = new Label { name = "thumbnail-title", pickingMode = PickingMode.Ignore };
                title.style.height = 34; title.style.flexShrink = 0; title.style.fontSize = 11;
                title.style.whiteSpace = WhiteSpace.Normal; title.style.overflow = Overflow.Hidden;
                title.style.color = new Color(.92f, .91f, .84f); card.Add(title);
                var state = new Label { name = "thumbnail-status", pickingMode = PickingMode.Ignore };
                state.style.height = 17; state.style.fontSize = 10; state.style.whiteSpace = WhiteSpace.NoWrap;
                state.style.overflow = Overflow.Hidden; state.style.textOverflow = TextOverflow.Ellipsis; card.Add(state);
                card.clicked += () => { if (card.userData is SavicThumbnailEntry entry) Select(entry.Id); };
                card.RegisterCallback<KeyDownEvent>(evt =>
                {
                    int delta = evt.keyCode == KeyCode.RightArrow ? 1 : evt.keyCode == KeyCode.LeftArrow ? -1 :
                        evt.keyCode == KeyCode.DownArrow ? columns : evt.keyCode == KeyCode.UpArrow ? -columns : 0;
                    if (delta == 0 || !(card.userData is SavicThumbnailEntry entry)) return;
                    int index = items.IndexOf(entry); int next = Mathf.Clamp(index + delta, 0, items.Count - 1);
                    Select(items[next].Id); list.ScrollToItem(next / columns);
                    schedule.Execute(() => this.Query<Button>("savic-thumbnail-card").ToList().FirstOrDefault(b =>
                        b.userData is SavicThumbnailEntry e && e.Id == selectedId)?.Focus());
                    evt.StopPropagation();
                });
                row.Add(card);
            }
            return row;
        }
        private void BindRow(VisualElement row, int index)
        {
            ClearRow(row);
            for (int i = 0; i < row.childCount; i++)
            {
                var card = (Button)row[i]; int itemIndex = index * columns + i;
                bool valid = itemIndex < items.Count; card.style.visibility = valid ? Visibility.Visible : Visibility.Hidden;
                card.SetEnabled(valid); if (!valid) continue;
                var entry = items[itemIndex]; card.userData = entry; card.tooltip = entry.Title + "\n" + entry.Type + " · " + entry.Status;
                Texture2D texture = !string.IsNullOrEmpty(entry.PreviewPath) && entry.PreviewPath.StartsWith("Assets/", StringComparison.Ordinal)
                    ? AssetDatabase.LoadAssetAtPath<Texture2D>(entry.PreviewPath) : null;
                var image = card.Q<Image>("thumbnail"); image.image = texture;
                image.style.display = texture == null ? DisplayStyle.None : DisplayStyle.Flex;
                card.Q<Label>("missing").style.display = texture == null ? DisplayStyle.Flex : DisplayStyle.None;
                card.Q<Label>("thumbnail-title").text = entry.Title;
                var state = card.Q<Label>("thumbnail-status"); state.text = entry.Type + " · " + entry.Status;
                state.style.color = entry.Status == "FAILED" ? new Color(.88f, .34f, .28f) : entry.Status == "NEEDS_REVIEW"
                    ? new Color(.90f, .68f, .25f) : new Color(.67f, .67f, .60f);
                PaintSelection(card, entry.Id == selectedId);
            }
        }
        private static void ClearRow(VisualElement row)
        {
            foreach (var card in row.Children().OfType<Button>())
            {
                card.userData = null; card.tooltip = ""; card.Q<Image>("thumbnail").image = null;
                card.Q<Label>("thumbnail-title").text = ""; card.Q<Label>("thumbnail-status").text = "";
                card.Q<Label>("missing").style.display = DisplayStyle.None;
            }
        }
        private static void PaintSelection(Button card, bool active)
        {
            Color border = active ? new Color(.62f, .69f, .31f) : new Color(.29f, .30f, .255f);
            card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = border;
        }
        internal void Select(string id)
        {
            var entry = items.FirstOrDefault(e => e.Id == id); if (entry == null) return;
            selectedId = id;
            foreach (var card in this.Query<Button>("savic-thumbnail-card").ToList())
                PaintSelection(card, card.userData is SavicThumbnailEntry e && e.Id == id);
            selected?.Invoke(entry);
        }
    }
}