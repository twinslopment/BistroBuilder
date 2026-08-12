from __future__ import annotations

from typing import Sequence

from .models import GateState, GroundingInputs, GroundingResult, GroundingSample


def _weighted_quantile(samples: Sequence[GroundingSample], quantile: float) -> float:
    if not samples:
        return 0.0
    ordered = sorted(samples, key=lambda sample: sample.z)
    total = sum(max(0.0, sample.area_weight) for sample in ordered)
    if total <= 1.0e-12:
        return ordered[0].z
    target = max(0.0, min(1.0, quantile)) * total
    cumulative = 0.0
    for sample in ordered:
        cumulative += max(0.0, sample.area_weight)
        if cumulative >= target:
            return sample.z
    return ordered[-1].z


def analyse_grounding(inputs: GroundingInputs) -> GroundingResult:
    samples = list(inputs.samples)
    if not samples:
        return GroundingResult(
            state=GateState.FAIL,
            translation_z=0.0,
            absolute_min_z=0.0,
            robust_support_z=0.0,
            support_weight=0.0,
            support_fraction=0.0,
            below_ground_weight=0.0,
            message="No geometry samples available for grounding.",
        )

    absolute_min = min(sample.z for sample in samples)
    robust_support = _weighted_quantile(samples, inputs.robust_percentile)
    support_limit = robust_support + max(1.0e-6, inputs.support_band_m)
    total_weight = sum(max(0.0, sample.area_weight) for sample in samples)
    support_weight = sum(
        max(0.0, sample.area_weight)
        for sample in samples
        if sample.z <= support_limit
    )
    support_fraction = support_weight / max(total_weight, 1.0e-12)
    below_ground_weight = sum(
        max(0.0, sample.area_weight)
        for sample in samples
        if sample.z < -abs(inputs.penetration_tolerance_m)
    )

    translation = -robust_support
    deep_delta = robust_support - absolute_min
    deep_limit = max(
        inputs.support_band_m * 2.0,
        inputs.penetration_tolerance_m * 3.0,
    )

    if deep_delta > deep_limit:
        return GroundingResult(
            state=GateState.FAIL,
            translation_z=translation,
            absolute_min_z=absolute_min,
            robust_support_z=robust_support,
            support_weight=support_weight,
            support_fraction=support_fraction,
            below_ground_weight=below_ground_weight,
            message=(
                "Localized geometry extends below the robust support level; "
                "automatic artifact repair is required before grounding."
            ),
        )

    if support_fraction < max(0.0, inputs.min_support_fraction):
        return GroundingResult(
            state=GateState.REVIEW,
            translation_z=translation,
            absolute_min_z=absolute_min,
            robust_support_z=robust_support,
            support_weight=support_weight,
            support_fraction=support_fraction,
            below_ground_weight=below_ground_weight,
            message="Insufficient stable support evidence; feet/base contact needs review.",
        )

    if (
        abs(robust_support) <= inputs.float_tolerance_m
        and absolute_min >= -inputs.penetration_tolerance_m
    ):
        return GroundingResult(
            state=GateState.PASS,
            translation_z=0.0,
            absolute_min_z=absolute_min,
            robust_support_z=robust_support,
            support_weight=support_weight,
            support_fraction=support_fraction,
            below_ground_weight=below_ground_weight,
            message="Asset is grounded at Z=0 within tolerance.",
        )

    return GroundingResult(
        state=GateState.REVIEW,
        translation_z=translation,
        absolute_min_z=absolute_min,
        robust_support_z=robust_support,
        support_weight=support_weight,
        support_fraction=support_fraction,
        below_ground_weight=below_ground_weight,
        message=(
            "Asset can be auto-grounded by translating WORK on Z; "
            "Ground Integrity must be rechecked after translation."
        ),
    )
