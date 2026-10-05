using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicThumbnailProbeWindow : EditorWindow { }
    public static class SavicThumbnailGridProbe
    {
        [Serializable] private sealed class Card
        {
            public string id, title, status, imagePath;
            public bool hasImage, selected;
            public float x, y, width, height;
        }
        [Serializable] private sealed class Snapshot
        {
            public string status = "PASS", scope = "Native Unity UI Toolkit layout", selectedId;
            public int count, columns, realizedCards;
            public bool detailMatches, consoleClean;
            public List<Card> cards = new List<Card>();
        }
        private static TcpListener server;
        private static SavicEditorWindow window;
        private static SavicThumbnailProbeWindow fixture;
        private static SavicThumbnailGrid grid;
        private static TcpClient pending;
        private static string pendingTarget;
        private static int frames, errors, requests;
        private static double deadline;
        private static string output;
        public static void StartFromCommandLine()
        {
            output = Path.Combine(SavicEditorContext.Instance.Layout.LogsRoot, "ThumbnailGrid"); Directory.CreateDirectory(output);
            Application.logMessageReceived += OnLog;
            window = EditorWindow.GetWindow<SavicEditorWindow>(); window.Show(); window.CreateGUI(); window.FlushRefreshForDiagnostics();
            window.ShowLibraryForDiagnostics();
            Require(window.rootVisualElement.Q<SavicThumbnailGrid>() != null, "Inventory did not contain thumbnail grid.");
            fixture = ScriptableObject.CreateInstance<SavicThumbnailProbeWindow>(); fixture.titleContent = new GUIContent("SAVIC thumbnail layout test");
            fixture.position = new Rect(50, 50, 800, 540); fixture.Show();
            grid = new SavicThumbnailGrid(_ => { }); grid.style.width = 460; grid.style.height = 460;
            fixture.rootVisualElement.Add(grid); grid.SetItems(Entries());
            server = new TcpListener(IPAddress.Loopback, 19057); server.Start(); deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update += Tick;
            File.WriteAllText(Path.Combine(output, "ready.json"), "{\"port\":19057,\"scope\":\"read-only diagnostic native UI test\"}");
            Debug.Log("[SAVIC] Thumbnail HTTP probe ready on loopback 19057.");
        }
        private static List<SavicThumbnailEntry> Entries()
        {
            var c = SavicEditorContext.Instance;
            c.ProjectInventory.TryLoadPersisted(out var inventory);
            return SavicCanonicalContentInventoryService.Build(c.Layout, c.Manifests.GetAll(), c.Jobs.Jobs,
                inventory)
                .Rows.Select(SavicThumbnailEntry.FromInventory).ToList();
        }
        private static void OnLog(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Thumbnail curl probe deadline exceeded.");
                if (pending != null)
                {
                    if (++frames < 12) return;
                    Handle(pending, pendingTarget); pending.Close(); pending = null; return;
                }
                if (!server.Pending()) return;
                pending = server.AcceptTcpClient(); pending.ReceiveTimeout = 1000;
                byte[] bytes = new byte[4096]; int size = pending.GetStream().Read(bytes, 0, bytes.Length);
                string line = Encoding.ASCII.GetString(bytes, 0, size).Split('\n')[0].Trim();
                if (!line.StartsWith("GET ", StringComparison.Ordinal)) { Send(pending, 405, "text/plain", Encoding.UTF8.GetBytes("GET only")); pending.Close(); pending = null; return; }
                pendingTarget = line.Split(' ')[1]; frames = 0;
                var uri = new Uri("http://127.0.0.1:19057" + pendingTarget);
                if (uri.AbsolutePath == "/grid")
                {
                    float width = 460; string search = "", select = "";
                    foreach (string part in uri.Query.TrimStart('?').Split('&'))
                    {
                        var pieces = part.Split('='); if (pieces.Length != 2) continue;
                        string value = Uri.UnescapeDataString(pieces[1]);
                        if (pieces[0] == "width") float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out width);
                        if (pieces[0] == "search") search = value; if (pieces[0] == "select") select = value;
                    }
                    Require(width >= 132 && width <= 900 && !float.IsNaN(width), "Invalid diagnostic viewport.");
                    grid.style.width = width;
                    grid.SetItems(Entries().Where(e => string.IsNullOrEmpty(search) || e.Title.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
                    if (!string.IsNullOrEmpty(select)) grid.Select(select);
                }
            }
            catch (Exception exception) { Debug.LogError("[SAVIC] Thumbnail probe failed: " + exception); Stop(1); }
        }
        private static void Handle(TcpClient client, string target)
        {
            requests++;
            var uri = new Uri("http://127.0.0.1:19057" + target);
            if (uri.AbsolutePath == "/health") { SendJson(client, "{\"status\":\"ready\",\"nativeWindow\":true}"); return; }
            if (uri.AbsolutePath == "/grid")
            {
                var snapshot = Capture(grid);
                File.WriteAllText(Path.Combine(output, "last-layout.json"), JsonUtility.ToJson(snapshot, true));
                SendJson(client, JsonUtility.ToJson(snapshot)); return;
            }
            if (uri.AbsolutePath.StartsWith("/thumbnail/", StringComparison.Ordinal))
            {
                string id = Uri.UnescapeDataString(uri.AbsolutePath.Substring("/thumbnail/".Length));
                var entry = Entries().FirstOrDefault(e => e.Id == id);
                if (entry == null || string.IsNullOrEmpty(entry.PreviewPath)) { Send(client, 404, "text/plain", Encoding.UTF8.GetBytes("Thumbnail unavailable")); return; }
                Send(client, 200, "image/png", File.ReadAllBytes(SavicEditorContext.Instance.Layout.FromProjectRelativePath(entry.PreviewPath))); return;
            }
            if (uri.AbsolutePath == "/finish")
            {
                RunNativeChecks(); Require(errors == 0, "Console contains errors.");
                File.WriteAllText(Path.Combine(output, "result.json"), "{\"status\":\"PASS\",\"curlRequests\":" + requests + ",\"consoleErrors\":0,\"nativeSelectionAndFilters\":true,\"fallbackAndVirtualization\":true}");
                SendJson(client, "{\"status\":\"PASS\"}"); Stop(0); return;
            }
            Send(client, 404, "text/plain", Encoding.UTF8.GetBytes("Unknown diagnostic path"));
        }
        private static Snapshot Capture(SavicThumbnailGrid component)
        {
            var snapshot = new Snapshot { count = component.ItemCount, columns = component.Columns, selectedId = component.SelectedId, consoleClean = errors == 0 };
            var cards = component.Query<Button>("savic-thumbnail-card").ToList().Where(b => b.userData is SavicThumbnailEntry).ToList();
            snapshot.realizedCards = cards.Count;
            foreach (var card in cards)
            {
                var entry = (SavicThumbnailEntry)card.userData; var rect = card.worldBound;
                Require(rect.width > 20 && rect.height > 20 && !float.IsNaN(rect.width), "Native card did not have a finite rendered size.");
                Require(card.Q<Image>("thumbnail").scaleMode == ScaleMode.ScaleToFit, "Thumbnail must preserve aspect ratio.");
                snapshot.cards.Add(new Card { id = entry.Id, title = entry.Title, status = entry.Status, imagePath = entry.PreviewPath,
                    hasImage = card.Q<Image>("thumbnail").image != null, selected = component.SelectedId == entry.Id,
                    x = rect.x, y = rect.y, width = rect.width, height = rect.height });
            }
            for (int i = 0; i < cards.Count; i++) for (int j = i + 1; j < cards.Count; j++)
                Require(!cards[i].worldBound.Overlaps(cards[j].worldBound), "Native thumbnail cards overlap.");
            return snapshot;
        }
        private static void RunNativeChecks()
        {
            var real = window.rootVisualElement.Q<SavicThumbnailGrid>(); Require(real.ItemCount == Entries().Count, "Real inventory grid lost content.");
            var button = real.Query<Button>("savic-thumbnail-card").ToList().Last(b => b.userData is SavicThumbnailEntry);
            var entry = (SavicThumbnailEntry)button.userData;
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
            Require(real.SelectedId == entry.Id && (string)window.rootVisualElement.Q("savic-library-detail").userData == entry.Id, "Native click selected wrong detail.");
            var view = window.rootVisualElement.Q<Button>("savic-view-list"); using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = view; view.SendEvent(submit); }
            view = window.rootVisualElement.Q<Button>("savic-view-thumbnails"); using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = view; view.SendEvent(submit); }
            Require(real.SelectedId == entry.Id, "Changing view lost selection.");
            window.ShowLibraryForDiagnostics("__no_match_savic__"); Require(window.rootVisualElement.Q<SavicThumbnailGrid>().ItemCount == 0, "Real filter empty state failed.");
            window.ShowLibraryForDiagnostics("Bar_Stoo"); Require(window.rootVisualElement.Q<SavicThumbnailGrid>().ItemCount > 0, "Real search failed.");
            var missing = new SavicThumbnailEntry { Id = "missing-fixture", Title = new string('X', 500), Type = "Fixture", Status = "NEEDS_REVIEW", PreviewPath = "Assets/nonexistent-thumbnail.png" };
            grid.SetItems(new[] { missing }); grid.Rows.Rebuild();
            var row = grid.Rows.makeItem(); grid.Rows.bindItem(row, 0);
            var card = row.Q<Button>(); Require(card.Q<Image>("thumbnail").image == null && card.Q<Label>("missing").style.display == DisplayStyle.Flex, "Missing thumbnail fallback failed.");
            grid.Rows.unbindItem(row, 0); Require(card.userData == null && card.Q<Image>("thumbnail").image == null && card.Q<Label>("thumbnail-title").text == "", "Recycled card retains previous asset.");
            grid.SetItems(Enumerable.Range(0, 10000).Select(i => new SavicThumbnailEntry { Id = "fixture-" + i, Title = "Fixture " + i, Type = "Fixture", Status = "INGESTED" }));
            Require(grid.Rows.virtualizationMethod == CollectionVirtualizationMethod.FixedHeight && grid.Rows.itemsSource.Count == (10000 + grid.Columns - 1) / grid.Columns, "Large grid does not virtualize rows.");
            Require(SavicThumbnailGrid.ColumnsForWidth(float.NaN) == 1 && SavicThumbnailGrid.ColumnsForWidth(132) == 1 && SavicThumbnailGrid.ColumnsForWidth(460) == 3, "Responsive sizing is invalid.");
            window.ShowLibraryForDiagnostics();
            Debug.Log("[SAVIC] THUMBNAIL GRID - PASS: native geometry, selection/detail, view switch, filters, fallback, recycling and 10k row virtualization.");
        }
        internal static void RunContractChecks()
        {
            string selected = "";
            var component = new SavicThumbnailGrid(e => selected = e?.Id ?? "");
            var real = Entries().FirstOrDefault(e => !string.IsNullOrEmpty(e.PreviewPath));
            Require(real != null, "Contract test needs an existing canonical thumbnail.");
            var missing = new SavicThumbnailEntry { Id = "contract-missing", Title = new string('X', 500), Type = "Fixture", Status = "NEEDS_REVIEW", PreviewPath = "Assets/missing-thumbnail-contract.png" };
            component.SetItems(new[] { real });
            var row = component.Rows.makeItem(); component.Rows.bindItem(row, 0);
            var card = row.Q<Button>(); Require(card.Q<Image>("thumbnail").image != null, "Real thumbnail did not bind.");
            component.SetItems(new[] { missing }); component.Rows.bindItem(row, 0);
            Require(card.Q<Image>("thumbnail").image == null && card.Q<Label>("missing").style.display == DisplayStyle.Flex, "Rebinding kept another asset's image.");
            component.Rows.unbindItem(row, 0);
            Require(card.userData == null && card.Q<Image>("thumbnail").image == null && card.Q<Label>("thumbnail-title").text == "", "Unbind did not clear the card.");
            component.SetItems(new[] { real, missing }, missing.Id); Require(selected == missing.Id, "Preferred selection was lost.");
            component.SetItems(new[] { real }, missing.Id); Require(selected == real.Id, "Filtered selection did not fall back safely.");
            component.SetItems(Array.Empty<SavicThumbnailEntry>()); Require(selected == "" && component.SelectedId == "", "Empty filter retained selection.");
            component.SetItems(Enumerable.Range(0, 10000).Select(i => new SavicThumbnailEntry { Id = "large-fixture-" + i, Title = "Fixture", Type = "Fixture", Status = "INGESTED" }));
            Require(component.Rows.itemsSource.Count == (10000 + component.Columns - 1) / component.Columns && component.Rows.virtualizationMethod == CollectionVirtualizationMethod.FixedHeight, "10k assets lost row virtualization.");
            Require(component.Query<Button>("savic-thumbnail-card").ToList().Count < 100, "Off-screen 10k cards were all created.");
            Require(SavicThumbnailGrid.ColumnsForWidth(float.NaN) == 1 && SavicThumbnailGrid.ColumnsForWidth(132) == 1 && SavicThumbnailGrid.ColumnsForWidth(330) == 2 && SavicThumbnailGrid.ColumnsForWidth(460) == 3 && SavicThumbnailGrid.ColumnsForWidth(740) == 5, "Responsive columns changed unexpectedly.");
            Debug.Log("[SAVIC] THUMBNAIL CONTRACT - PASS: actual image, stale rebind, placeholder, selection, empty filter and 10k virtualization.");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void SendJson(TcpClient client, string json) => Send(client, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));
        private static void Send(TcpClient client, int code, string type, byte[] body)
        {
            byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + code + (code == 200 ? " OK" : " Error") + "\r\nContent-Type: " + type + "\r\nContent-Length: " + body.Length + "\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
            var stream = client.GetStream(); stream.Write(header, 0, header.Length); stream.Write(body, 0, body.Length); stream.Flush();
        }
        private static void Stop(int code)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog; server?.Stop();
            if (fixture != null) fixture.Close(); if (window != null) window.Close(); EditorApplication.Exit(code);
        }
    }
}