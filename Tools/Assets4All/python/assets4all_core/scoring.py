from __future__ import annotations

import math
from typing import Dict, List

from .models import (
    ConversionRiskInputs,
    Decision,
    DualDecision,
    ScoreResult,
    ViabilityInputs,
)


_PVS_WEIGHTS: Dict[str, float] = {
    "geometry_integrity": 0.16,
    "topology": 0.12,
    "uv_readiness": 0.06,
    "transform_orientation": 0.06,
    "scale_plausibility": 0.06,
    "artifact_severity_inverse": 0.14,
    "region_separability": 0.16,
    "symmetry_repetition": 0.07,
    "optimization_headroom": 0.08,
    "profile_plausibility": 0.09,
}


def _decision_from_score(score: float) -> Decision:
    if score >= 85.0:
        return Decision.AUTO
    if score >= 60.0:
        return Decision.REVIEW
    return Decision.REGENERATE


def processing_viability_score(inputs: ViabilityInputs) -> ScoreResult:
    """Interpretable weighted repairability score.

    PVS intentionally uses an additive model so users can understand which
    technical dimensions pull the asset up or down. CSE uses a completely
    different multiplicative risk model.
    """

    values = {
        name: getattr(inputs, name).clamped()
        for name in _PVS_WEIGHTS
    }

    weighted = sum(
        values[name].value * weight
        for name, weight in _PVS_WEIGHTS.items()
    )
    confidence = sum(
        values[name].confidence * _PVS_WEIGHTS[name]
        for name in _PVS_WEIGHTS
    ) / max(sum(_PVS_WEIGHTS.values()), 1.0e-9)

    # Weakest-link guard: a catastrophic subscore cannot be hidden by a good
    # average, but the model remains additive and interpretable.
    _, worst_metric = min(values.items(), key=lambda item: item[1].value)
    if worst_metric.value < 20.0:
        weighted = min(weighted, 49.0)
    elif worst_metric.value < 35.0:
        weighted = min(weighted, 59.0)

    reasons: List[str] = []
    for name, metric in sorted(
        values.items(), key=lambda item: item[1].value
    )[:3]:
        if metric.value < 70.0:
            reasons.append(f"{name}={metric.value:.1f}")

    return ScoreResult(
        score=round(max(0.0, min(100.0, weighted)), 2),
        decision=_decision_from_score(weighted),
        confidence=round(confidence, 3),
        reasons=tuple(reasons),
    )


def _clamp_probability(value: float) -> float:
    return max(0.001, min(0.999, float(value)))


def conversion_success_estimate(inputs: ConversionRiskInputs) -> ScoreResult:
    """Independent probability-oriented estimate of conversion success.

    Unlike PVS, CSE is multiplicative and treats conversion as a chain of
    failure opportunities. It applies nonlinear penalties for ambiguity and
    predicted review time. A fragile gate can reduce CSE even when PVS is high.
    """

    gate_probabilities = [
        _clamp_probability(inputs.repair_success_probability),
        _clamp_probability(inputs.segmentation_success_probability),
        _clamp_probability(inputs.semantic_assignment_probability),
        _clamp_probability(inputs.grounding_success_probability),
        _clamp_probability(inputs.optimization_success_probability),
        _clamp_probability(inputs.export_success_probability),
    ]

    log_mean = sum(math.log(p) for p in gate_probabilities) / len(gate_probabilities)
    geometric_mean = math.exp(log_mean)
    weakest = min(gate_probabilities)
    base_probability = geometric_mean * 0.72 + weakest * 0.28

    ambiguity_penalty = math.exp(
        -0.075 * max(0, inputs.ambiguous_decisions)
    )

    budget = max(1.0, float(inputs.review_budget_seconds))
    review_ratio = max(0.0, float(inputs.predicted_review_seconds)) / budget
    if review_ratio <= 1.0:
        review_penalty = 1.0 - 0.08 * review_ratio
    else:
        review_penalty = math.exp(-0.9 * (review_ratio - 1.0)) * 0.92

    severe_penalty = 0.18 ** len(tuple(inputs.severe_failure_flags))

    probability = (
        base_probability
        * ambiguity_penalty
        * review_penalty
        * severe_penalty
    )
    probability = max(0.0, min(1.0, probability))
    score = probability * 100.0

    reasons: List[str] = []
    labels = (
        "repair",
        "segmentation",
        "semantic",
        "grounding",
        "optimization",
        "export",
    )
    for label, probability_gate in sorted(
        zip(labels, gate_probabilities), key=lambda item: item[1]
    )[:2]:
        if probability_gate < 0.85:
            reasons.append(f"{label}_p={probability_gate:.2f}")
    if inputs.ambiguous_decisions > 0:
        reasons.append(f"ambiguous={inputs.ambiguous_decisions}")
    if inputs.predicted_review_seconds > budget:
        reasons.append(
            f"review={inputs.predicted_review_seconds:.0f}s>{budget:.0f}s"
        )
    if inputs.severe_failure_flags:
        reasons.extend(
            f"severe:{flag}" for flag in inputs.severe_failure_flags
        )

    confidence = max(
        0.25,
        min(1.0, 1.0 - 0.25 * min(2.0, review_ratio)),
    )

    return ScoreResult(
        score=round(score, 2),
        decision=_decision_from_score(score),
        confidence=round(confidence, 3),
        reasons=tuple(reasons),
    )


def resolve_dual_decision(
    pvs: ScoreResult,
    cse: ScoreResult,
) -> DualDecision:
    disagreement = abs(pvs.score - cse.score)
    reasons: List[str] = []

    # Disagreement is information, not noise. Large disagreement always
    # triggers Review rather than being hidden in a combined average.
    if disagreement >= 22.0:
        final = Decision.REVIEW
        reasons.append(
            f"large_score_disagreement={disagreement:.1f}"
        )
    elif (
        pvs.decision == Decision.REGENERATE
        and cse.decision == Decision.REGENERATE
    ):
        final = Decision.REGENERATE
    elif (
        pvs.decision == Decision.AUTO
        and cse.decision == Decision.AUTO
    ):
        final = Decision.AUTO
    else:
        final = Decision.REVIEW
        reasons.append(
            f"mixed_decision={pvs.decision.value}/{cse.decision.value}"
        )

    return DualDecision(
        pvs=pvs,
        cse=cse,
        final_decision=final,
        disagreement=round(disagreement, 2),
        reasons=tuple(reasons),
    )
