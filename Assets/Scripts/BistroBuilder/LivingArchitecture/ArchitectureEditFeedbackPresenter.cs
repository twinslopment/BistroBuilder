using System.Collections.Generic;
using BistroBuilder.LivingArchitecture.Domain;
using UnityEngine;

namespace BistroBuilder.LivingArchitecture.Runtime
{
    /// <summary>
    /// Presentador LA10 del feedback universal. Consume frames declarativos del controlador y los proyecta
    /// como tintado de preview, marcadores de snap y señales sonoras mínimas. No valida ni modifica arquitectura.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArchitectureEditFeedbackPresenter : MonoBehaviour
    {
        [SerializeField] private ArchitectureEditToolController controller;
        [SerializeField] private ArchitectureRuntimePresenter runtimePresenter;
        [Header("Preview")]
        [SerializeField] private Material validPreviewMaterial;
        [SerializeField] private Material warningPreviewMaterial;
        [SerializeField] private Material invalidPreviewMaterial;
        [Header("Snap")]
        [SerializeField] private float snapMarkerRadius = 0.055f;
        [SerializeField] private float snapMarkerHeight = 0.04f;
        [SerializeField] private Material snapHighConfidenceMaterial;
        [SerializeField] private Material snapMediumConfidenceMaterial;
        [SerializeField] private Material snapLowConfidenceMaterial;
        [Header("Audio opcional")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip confirmClip;
        [SerializeField] private AudioClip cancelClip;
        [SerializeField] private AudioClip undoRedoClip;

        private readonly List<GameObject> snapMarkers = new List<GameObject>();
        public ArchitectureEditFeedbackFrame CurrentFrame { get; private set; }

        private void Awake()
        {
            if (controller == null) controller = GetComponent<ArchitectureEditToolController>();
            if (runtimePresenter == null) runtimePresenter = GetComponent<ArchitectureRuntimePresenter>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (controller == null) controller = GetComponent<ArchitectureEditToolController>();
            if (controller != null)
            {
                controller.FeedbackChanged += HandleFeedbackChanged;
                controller.EnsureInitialized();
                HandleFeedbackChanged(controller.FeedbackFrame);
            }
        }

        private void OnDisable()
        {
            if (controller != null) controller.FeedbackChanged -= HandleFeedbackChanged;
            ClearSnapMarkers();
            if (runtimePresenter != null) runtimePresenter.ClearFeedbackMaterial();
        }

        private void HandleFeedbackChanged(ArchitectureEditFeedbackFrame frame)
        {
            CurrentFrame = frame;
            ClearSnapMarkers();
            ApplyPreviewMaterial(frame);
            if (frame == null) return;

            foreach (var cue in frame.Cues)
            {
                if (cue == null) continue;
                if ((cue.Kind == ArchitectureFeedbackCueKind.SnapPoint || cue.Kind == ArchitectureFeedbackCueKind.SnapGuide) && cue.HasPosition)
                    CreateSnapMarker(cue);
                else if (cue.Kind == ArchitectureFeedbackCueKind.CommitPulse)
                    Play(confirmClip);
                else if (cue.Kind == ArchitectureFeedbackCueKind.CancelFade)
                    Play(cancelClip);
                else if (cue.Kind == ArchitectureFeedbackCueKind.UndoPulse || cue.Kind == ArchitectureFeedbackCueKind.RedoPulse)
                    Play(undoRedoClip);
            }
        }

        private void ApplyPreviewMaterial(ArchitectureEditFeedbackFrame frame)
        {
            if (runtimePresenter == null) return;
            if (frame == null || !frame.HasPreview)
            {
                runtimePresenter.ClearFeedbackMaterial();
                return;
            }

            Material material = null;
            if (frame.State == ArchitectureFeedbackState.Valid) material = validPreviewMaterial;
            else if (frame.State == ArchitectureFeedbackState.Warning) material = warningPreviewMaterial;
            else if (frame.State == ArchitectureFeedbackState.Invalid) material = invalidPreviewMaterial;
            runtimePresenter.SetFeedbackMaterial(material);
        }

        private void CreateSnapMarker(ArchitectureFeedbackCue cue)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "LA10_Snap_" + cue.Key;
            marker.transform.SetParent(transform, false);
            marker.transform.position = new Vector3((float)cue.Position.X, snapMarkerHeight, (float)cue.Position.Y);
            marker.transform.localScale = Vector3.one * (snapMarkerRadius * 2f);

            var collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }

            var renderer = marker.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material material = snapLowConfidenceMaterial;
                if (cue.SnapConfidence == ArchitectureSnapConfidence.High) material = snapHighConfidenceMaterial;
                else if (cue.SnapConfidence == ArchitectureSnapConfidence.Medium) material = snapMediumConfidenceMaterial;
                if (material != null) renderer.sharedMaterial = material;
            }
            snapMarkers.Add(marker);
        }

        private void ClearSnapMarkers()
        {
            foreach (var marker in snapMarkers)
            {
                if (marker == null) continue;
                if (Application.isPlaying) Destroy(marker);
                else DestroyImmediate(marker);
            }
            snapMarkers.Clear();
        }

        private void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }
    }
}
