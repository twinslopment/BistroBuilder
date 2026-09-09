using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BistroBuilderBBSISPhase2ADiagnostic
{
    public static void RunFromCommandLine()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity", OpenSceneMode.Single);
        RestaurantSeat[] seats = Object.FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        for (int i = 0; i < seats.Length; i++)
        {
            RestaurantSeat seat = seats[i];
            BistroBuilderSpatialSubject subject = seat.GetComponent<BistroBuilderSpatialSubject>();
            Vector3 sp = default;
            Vector3 ap = default;
            bool seatOk = subject != null && subject.TryGetPortWorld(
                "seat", out sp, out _, out _, out _);
            bool approachOk = subject != null && subject.TryGetPortWorld(
                "approach", out ap, out _, out _, out _);
            float sd = seatOk && seat.SeatPoint != null ? Vector3.Distance(sp, seat.SeatPoint.position) : -1f;
            float ad = approachOk && seat.CustomerApproachPoint != null ? Vector3.Distance(ap, seat.CustomerApproachPoint.position) : -1f;
            if (!seatOk || !approachOk || sd > 0.01f || ad > 0.01f)
                Debug.LogError("SEAT_FAIL " + seat.name + " subject=" +
                    (subject != null ? subject.SubjectId : "null") +
                    " seatOk=" + seatOk + " approachOk=" + approachOk +
                    " seatDist=" + sd + " approachDist=" + ad);
        }
        RestaurantTableSeatingConfiguration[] tables =
            Object.FindObjectsByType<RestaurantTableSeatingConfiguration>(
                FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        for (int i = 0; i < tables.Length; i++)
        {
            RestaurantTableSeatingConfiguration table = tables[i];
            BistroBuilderSpatialSubject subject = table.GetComponent<BistroBuilderSpatialSubject>();
            BistroBuilderAdaptiveSpatialProxy proxy = table.GetComponent<BistroBuilderAdaptiveSpatialProxy>();
            BistroBuilderTableSpatialAdapter adapter = table.GetComponent<BistroBuilderTableSpatialAdapter>();
            bool ok = subject != null && proxy != null && adapter != null &&
                subject.Contract != null && subject.Contract.FamilyId == "seating.table" &&
                proxy.Mode == BistroBuilderAdaptiveSpatialProxyMode.Layered;
            if (!ok)
                Debug.LogError("TABLE_FAIL " + table.name +
                    " subject=" + (subject != null ? subject.SubjectId : "null") +
                    " proxy=" + (proxy != null ? proxy.Mode.ToString() : "null") +
                    " adapter=" + (adapter != null) +
                    " contract=" + (subject != null && subject.Contract != null
                        ? subject.Contract.FamilyId + "/v" + subject.Contract.ContractVersion
                        : "null"));
        }
        Debug.Log("BBSIS_2A_DIAGNOSTIC_DONE");
        EditorApplication.Exit(0);
    }
}

