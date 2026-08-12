from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Dict, Tuple


PROFILE_RULES: Dict[str, Dict[str, object]] = {
    "GENERIC_PROP": {
        "budget": 80000,
        "dims": ((0.01, 8.0), (0.01, 8.0), (0.01, 8.0)),
        "typical": None,
    },
    "CHAIR": {
        "budget": 35000,
        "dims": ((0.30, 0.90), (0.30, 0.95), (0.55, 1.35)),
        "typical": (0.50, 0.55, 0.86),
    },
    "TABLE": {
        "budget": 45000,
        "dims": ((0.35, 4.0), (0.35, 4.0), (0.30, 1.30)),
        "typical": (1.20, 0.80, 0.75),
    },
    "SOFA": {
        "budget": 70000,
        "dims": ((0.60, 4.5), (0.45, 2.0), (0.40, 1.60)),
        "typical": (1.90, 0.90, 0.85),
    },
    "CABINET": {
        "budget": 70000,
        "dims": ((0.20, 4.0), (0.15, 2.0), (0.20, 3.5)),
        "typical": (1.00, 0.45, 1.80),
    },
    "LAMP": {
        "budget": 45000,
        "dims": ((0.03, 4.0), (0.03, 4.0), (0.03, 5.0)),
        "typical": None,
    },
    "PLANT": {
        "budget": 60000,
        "dims": ((0.05, 4.0), (0.05, 4.0), (0.05, 5.0)),
        "typical": None,
    },
    "KITCHEN_EQUIPMENT": {
        "budget": 90000,
        "dims": ((0.15, 5.0), (0.15, 5.0), (0.15, 3.0)),
        "typical": None,
    },
    "DECORATION": {
        "budget": 50000,
        "dims": ((0.01, 6.0), (0.01, 6.0), (0.01, 6.0)),
        "typical": None,
    },
}


@dataclass(frozen=True)
class ScaleInference:
    factor: float
    confidence: float
    recommended: bool
    current_plausibility: float
    scaled_plausibility: float
    reason: str


def get_profile_rule(profile_id: str) -> Dict[str, object]:
    return PROFILE_RULES.get(profile_id, PROFILE_RULES["GENERIC_PROP"])


def profile_plausibility(
    profile_id: str,
    dims: Tuple[float, float, float],
) -> float:
    rule = get_profile_rule(profile_id)
    scores = []
    for value, (minimum, maximum) in zip(dims, rule["dims"]):
        if minimum <= value <= maximum:
            scores.append(100.0)
        else:
            distance = minimum - value if value < minimum else value - maximum
            span = max(maximum - minimum, 0.05)
            scores.append(max(0.0, 100.0 - 130.0 * distance / span))
    return sum(scores) / max(len(scores), 1)


def _median(values):
    ordered = sorted(values)
    count = len(ordered)
    if count == 0:
        return 0.0
    midpoint = count // 2
    if count % 2:
        return ordered[midpoint]
    return (ordered[midpoint - 1] + ordered[midpoint]) * 0.5


def infer_uniform_scale(
    profile_id: str,
    dims: Tuple[float, float, float],
) -> ScaleInference:
    current = profile_plausibility(profile_id, dims)
    rule = get_profile_rule(profile_id)
    typical = rule.get("typical")

    if not typical or any(value <= 1.0e-6 for value in dims):
        return ScaleInference(
            factor=1.0,
            confidence=0.0,
            recommended=False,
            current_plausibility=current,
            scaled_plausibility=current,
            reason="El perfil no tiene una referencia dimensional fiable para autoescala.",
        )

    log_ratios = [
        math.log(max(1.0e-9, target / value))
        for target, value in zip(typical, dims)
    ]
    median_log = _median(log_ratios)
    factor = math.exp(median_log)
    scaled_dims = tuple(value * factor for value in dims)
    scaled = profile_plausibility(profile_id, scaled_dims)

    mean_log = sum(log_ratios) / len(log_ratios)
    variance = sum((value - mean_log) ** 2 for value in log_ratios) / len(log_ratios)
    agreement = math.exp(-7.0 * math.sqrt(max(0.0, variance)))
    fit = max(0.0, min(1.0, scaled / 100.0))
    confidence = max(0.0, min(1.0, 0.62 * agreement + 0.38 * fit))

    correction_magnitude = abs(math.log(max(factor, 1.0e-9)))
    recommended = (
        confidence >= 0.84
        and scaled >= 94.0
        and current <= 80.0
        and correction_magnitude >= math.log(1.12)
        and 0.05 <= factor <= 20.0
    )

    reason = (
        f"Escala uniforme sugerida x{factor:.4f}; confianza {confidence * 100.0:.1f}%; "
        f"plausibilidad {current:.1f} -> {scaled:.1f}."
    )

    return ScaleInference(
        factor=factor,
        confidence=confidence,
        recommended=recommended,
        current_plausibility=current,
        scaled_plausibility=scaled,
        reason=reason,
    )
