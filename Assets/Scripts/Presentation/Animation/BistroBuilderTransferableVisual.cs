using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class BistroBuilderTransferableVisual : MonoBehaviour
{
    private Transform homeParent;
    private Vector3 homeLocalPosition;
    private Quaternion homeLocalRotation;
    private Vector3 homeLocalScale;
    private bool homeCaptured;

    public Transform CurrentVisualParent => transform.parent;

    private void Awake() => CaptureHomeIfNeeded();

    public void CaptureHomeIfNeeded()
    {
        if (homeCaptured) return;
        homeParent = transform.parent;
        homeLocalPosition = transform.localPosition;
        homeLocalRotation = transform.localRotation;
        homeLocalScale = transform.localScale;
        homeCaptured = true;
    }

    public bool AttachVisual(Transform socket)
    {
        if (socket == null) return false;
        CaptureHomeIfNeeded();
        transform.SetParent(socket, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        return true;
    }

    public bool ReconcileVisualTo(Transform authoritativeSocket)
    {
        if (authoritativeSocket == null) return false;
        return AttachVisual(authoritativeSocket);
    }

    public void RestoreHome()
    {
        if (!homeCaptured) return;
        transform.SetParent(homeParent, false);
        transform.localPosition = homeLocalPosition;
        transform.localRotation = homeLocalRotation;
        transform.localScale = homeLocalScale;
    }
}
