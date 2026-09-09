using UnityEngine;

/// <summary>
/// Sockets visuales reutilizables para objetos transportados.
/// Puede autoconfigurarse sobre cualquier Avatar Humanoid válido.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderCarrySocketSet : MonoBehaviour
{
    [SerializeField] private Transform rightHand;
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform center;
    [SerializeField] private Transform tray;
    [SerializeField] private Transform twoHandCenter;

    public bool EnsureFromHumanoid(Animator animator, out string error)
    {
        error = string.Empty;
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
        {
            error = name + " necesita Animator Humanoid válido para generar Carry Sockets.";
            return false;
        }

        Transform rightBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform leftBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (rightBone == null || leftBone == null || chest == null)
        {
            error = name + " no expone manos/pecho Humanoid requeridos.";
            return false;
        }
        rightHand = EnsureSocket(rightBone, "BB_RightHandSocket", Vector3.zero);
        leftHand = EnsureSocket(leftBone, "BB_LeftHandSocket", Vector3.zero);
        center = EnsureSocket(chest, "BB_CarryCenter", new Vector3(0f, -0.18f, 0.28f));
        tray = EnsureSocket(chest, "BB_TraySocket", new Vector3(0f, -0.30f, 0.38f));
        twoHandCenter = EnsureSocket(chest, "BB_TwoHandCenter", new Vector3(0f, -0.18f, 0.42f));
        return true;
    }

    public bool TryGet(BistroBuilderCarrySocketKind kind, out Transform socket)
    {
        socket = kind switch
        {
            BistroBuilderCarrySocketKind.RightHand => rightHand,
            BistroBuilderCarrySocketKind.LeftHand => leftHand,
            BistroBuilderCarrySocketKind.Center => center,
            BistroBuilderCarrySocketKind.Tray => tray,
            BistroBuilderCarrySocketKind.TwoHandCenter => twoHandCenter,
            _ => null
        };
        return socket != null;
    }

    private static Transform EnsureSocket(Transform parent, string socketName, Vector3 localPosition)
    {
        Transform existing = parent.Find(socketName);
        if (existing != null) return existing;
        GameObject go = new GameObject(socketName);
        Transform socket = go.transform;
        socket.SetParent(parent, false);
        socket.localPosition = localPosition;
        socket.localRotation = Quaternion.identity;
        return socket;
    }
}
