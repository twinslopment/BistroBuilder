using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Actual customer prefab, table grant, Navigation arrival and Animation V1.
/// Does not move the logical group or fabricate a seat/reservation as acceptance.</summary>
[InitializeOnLoad]
public static class BistroBuilderDiningSeatPresentationPlaytest
{
    const string Key = "BB.DiningSeatPresentation";
    static int stage, checks, firstUnityId;
    static bool restored;
    static CustomerGroupState savedState;
    static double deadline, holdUntil;
    static GameObject customer;
    static CustomerGroup group;
    static CustomerMovementView movement;
    static RestaurantTable table;
    static BistroBuilderCustomerBarSeatPresenter[] presenters;
    static Vector3 logicalPosition;
    static Quaternion logicalRotation;
    static readonly List<string> evidence = new List<string>();
    static BistroBuilderDiningSeatPresentationPlaytest()
    {
        EditorApplication.playModeStateChanged += Changed;
        Application.logMessageReceived += Log;
    }
    public static void Run()
    {
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Pass", false);
        SessionState.SetString(Key + ".Error", "");
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Log(string text, string stack, LogType type)
    {
        if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            SessionState.SetString(Key + ".Error", SessionState.GetString(Key + ".Error", "") + "\n" + text + "\n" + stack);
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            stage = checks = 0; restored = false; evidence.Clear(); deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            bool pass = SessionState.GetBool(Key + ".Pass", false) && SessionState.GetString(Key + ".Error", "") == "";
            string report = DateTime.UtcNow.ToString("O") + "\n" + (pass ? "PASS" : "FAIL") + " checks=" + checks + "\n" +
                string.Join("\n", evidence) + "\n" + SessionState.GetString(Key + ".Error", "");
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/dining-seat-presentation.txt", report);
            SessionState.SetBool(Key, false); Debug.Log(report); EditorApplication.Exit(pass ? 0 : 1);
        }
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); checks++; evidence.Add("PASS " + message); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < 8) return;
        try
        {
            if (SessionState.GetString(Key + ".Error", "") != "") throw new Exception("Console was not clean.");
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout stage=" + stage + " state=" + group?.CurrentState + " moving=" + movement?.IsMoving + " presentation=" + presenters?.FirstOrDefault()?.State + " error=" + presenters?.FirstOrDefault()?.LastError);
            Time.timeScale = 1f;
            if (stage == 0)
            {
                Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>()?.Hide();
                var seats = Object.FindFirstObjectByType<RestaurantSeatRegistry>();
                table = Object.FindObjectsByType<RestaurantTable>(FindObjectsSortMode.None)
                    .Where(t => t.CanSeatGroup(2) && t.CustomerApproachPoint != null)
                    .FirstOrDefault(t => seats.RegisteredSeats.Count(s => s.IsAssociated && s.AssociatedTable.Table == t) >= 2);
                Check(table != null, "Existing real table with at least two associated operational chairs");
                customer = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Customers/CustomerGroupPrefab.prefab"));
                foreach (var behaviour in customer.GetComponents<MonoBehaviour>())
                    if (!(behaviour is CustomerGroup) && !(behaviour is CustomerMovementView) &&
                        !(behaviour is CustomerSeatingFlow) && !(behaviour is BistroBuilderAdvancedCustomerMemberVisualGroup)) behaviour.enabled = false;
                group = customer.GetComponent<CustomerGroup>(); movement = customer.GetComponent<CustomerMovementView>();
                Check(group.Initialize(99982, 2), "Canonical two-member customer initialized");
                Vector3 outward = table.CustomerApproachPoint.position - table.transform.position; outward.y = 0f;
                customer.transform.position = table.CustomerApproachPoint.position + outward.normalized * .8f;
                Check(group.AssignTable(table), "Gameplay authority grants the table");
                Check(customer.GetComponent<BistroBuilderAdvancedCustomerMemberVisualGroup>().EnsureVisuals(), "Actual Humanoid members materialized");
                presenters = customer.GetComponentsInChildren<BistroBuilderCustomerBarSeatPresenter>();
                Check(presenters.Length == 2 && presenters.All(p => p.Animator.isHuman), "Two independent real Humanoid presenters");
                group.SetState(CustomerGroupState.WalkingToTable);
                Check(presenters.All(p => p.AlignedSeat == null), "No seat representation before actual arrival");
                stage = 1;
            }
            else if (stage == 1 && movement.HasReachedDestination)
            {
                Check(group.CurrentState == CustomerGroupState.WaitingForWaiter, "Real CustomerSeatingFlow advances after Navigation arrival");
                logicalPosition = customer.transform.position; logicalRotation = customer.transform.rotation;
                stage = 2;
            }
            else if (stage == 2 && presenters.All(p => p.State == BistroBuilderCustomerBarSeatPresenter.VisualState.Seated))
            { holdUntil = EditorApplication.timeSinceStartup + .5; stage = 3; }
            else if (stage == 3 && EditorApplication.timeSinceStartup >= holdUntil)
            {
                Check(presenters.Select(p => p.AlignedSeat).Distinct().Count() == 2, "Members use different physical chairs");
                foreach (var p in presenters)
                {
                    Check(p.CurrentMotionId == "seat.idle.standard", "Certified seated idle on actual member");
                    float error = Vector3.Distance(p.Hips.position, p.AlignedSeat.position + p.AlignedSeat.up * .10f);
                    Check(error < .025f, "Pelvis matches authored dining SeatPoint; error=" + error.ToString("F5"));
                    var seat = p.AlignedSeat.GetComponentInParent<RestaurantSeat>();
                    Check(seat.AssociatedTable.Table == table, "Seat belongs to the granted table");
                    Check(Vector3.Dot(p.transform.forward, seat.CalculateFacingDirectionAtPose(seat.transform.rotation)) > .99f, "Member faces the table using chair's functional facing axis");
                    foreach (bool left in new[] { true, false })
                    {
                        Vector3 upper = p.Animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg).position;
                        Vector3 knee = p.Animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg).position;
                        Check(Mathf.Abs(knee.y - upper.y) < .18f && Vector3.Distance(knee, upper) > .2f, "Real seated thigh pose, not a standing capsule");
                    }
                }
                Check(Vector3.Distance(customer.transform.position, logicalPosition) < .0001f && Quaternion.Angle(customer.transform.rotation, logicalRotation) < .001f, "Animation preserves Navigation root pose");
                Check(table.AssignedCustomerGroup == group && group.AssignedTable == table, "Animation preserves logical table ownership");
                Capture(); savedState = group.CurrentState; group.ClearAssignedTable(); stage = 4;
            }
            else if (stage == 4 && presenters.All(p => p.State == BistroBuilderCustomerBarSeatPresenter.VisualState.Baseline))
            {
                Check(presenters.All(p => p.AlignedSeat == null), "Releasing the table returns all members to baseline");
                Check(Vector3.Distance(customer.transform.position, logicalPosition) < .0001f, "Standing preserves the logical root");
                                int previousUnityId = customer.GetInstanceID();
                Object.DestroyImmediate(customer); customer = null;
                Check(table.AssignedCustomerGroup == null, "Cleanup leaves the actual table unassigned");
                if (!restored)
                {
                    restored = true; firstUnityId = previousUnityId;
                    customer = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Customers/CustomerGroupPrefab.prefab"), logicalPosition, logicalRotation);
                    foreach (var behaviour in customer.GetComponents<MonoBehaviour>())
                        if (!(behaviour is CustomerGroup) && !(behaviour is CustomerMovementView) &&
                            !(behaviour is CustomerSeatingFlow) && !(behaviour is BistroBuilderAdvancedCustomerMemberVisualGroup)) behaviour.enabled = false;
                    group = customer.GetComponent<CustomerGroup>(); movement = customer.GetComponent<CustomerMovementView>();
                    Check(group.TryRestoreRuntimeIdentity(99982, 2, BistroBuilderServiceMode.TableService, 0f, out string identityError), identityError);
                    Check(group.AssignTable(table), "Reconstructed group receives its existing table grant");
                    Check(group.TryRestoreRuntimeState(savedState, BistroBuilderServiceMode.TableService, false, out string restoreError), restoreError);
                    Check(!movement.HasReachedDestination && !movement.IsMoving, "Recreated Navigation does not fabricate a saved arrival flag");
                    Check(customer.GetInstanceID() != firstUnityId && group.GroupId == 99982, "Reconstruction preserves logical identity with a new Unity instance");
                    Check(customer.GetComponent<BistroBuilderAdvancedCustomerMemberVisualGroup>().EnsureVisuals(), "Reconstructed members materialize through their canonical visual group");
                    presenters = customer.GetComponentsInChildren<BistroBuilderCustomerBarSeatPresenter>();
                    Check(presenters.Length == 2, "Both restored members have real presenters");
                    deadline = EditorApplication.timeSinceStartup + 60; stage = 2;
                }
                else
                { SessionState.SetBool(Key + ".Pass", true); EditorApplication.ExitPlaymode(); stage = 5; }
            }
        }
        catch (Exception e)
        {
            SessionState.SetString(Key + ".Error", SessionState.GetString(Key + ".Error", "") + "\n" + e);
            if (group != null) group.ClearAssignedTable();
            if (customer != null) Object.DestroyImmediate(customer);
            EditorApplication.ExitPlaymode();
        }
    }
    static void Capture()
    {
        var cameraObject = new GameObject("DiningSeatCapture", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        Vector3 center = (presenters[0].Hips.position + presenters[1].Hips.position) * .5f;
        camera.transform.position = center + table.transform.right * 3f - table.transform.forward * 3f + Vector3.up * 1.2f;
        camera.transform.LookAt(center); camera.nearClipPlane = .05f;
        var target = new RenderTexture(1280, 720, 24); var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            Directory.CreateDirectory("Logs"); File.WriteAllBytes(restored ? "Logs/dining-seated-restored-customers.png" : "Logs/dining-seated-actual-customers.png", image.EncodeToPNG());
        }
        finally
        { RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); if(image != null) Object.DestroyImmediate(image); Object.DestroyImmediate(cameraObject); }
    }
}
