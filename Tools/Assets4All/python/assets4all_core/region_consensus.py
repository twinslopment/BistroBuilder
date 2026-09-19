from __future__ import annotations

import math
import random
from dataclasses import dataclass
from typing import Dict, List, Mapping, Sequence

from .models import BoundaryConsensus, BoundaryEvidence


@dataclass(frozen=True)
class RegionConsensusConfig:
    boundary_threshold: float = 0.58
    perturbation_runs: int = 21
    threshold_jitter: float = 0.10
    evidence_jitter: float = 0.035
    min_persistence: float = 0.62
    random_seed: int = 4317


_DEFAULT_VIEW_WEIGHTS: Mapping[str, float] = {
    "topology": 1.00,
    "dihedral": 0.95,
    "curvature": 0.90,
    "thickness": 0.85,
    "normals": 0.75,
    "geodesic": 0.80,
    "level": 0.55,
    "symmetry": 0.45,
    "material_uv": 0.70,
}


def _clamp01(value: float) -> float:
    return max(0.0, min(1.0, float(value)))


def _evidence_items(
    evidence: BoundaryEvidence,
) -> Dict[str, float]:
    return {
        "topology": _clamp01(evidence.topology),
        "dihedral": _clamp01(evidence.dihedral),
        "curvature": _clamp01(evidence.curvature),
        "thickness": _clamp01(evidence.thickness),
        "normals": _clamp01(evidence.normals),
        "geodesic": _clamp01(evidence.geodesic),
        "level": _clamp01(evidence.level),
        "symmetry": _clamp01(evidence.symmetry),
        "material_uv": _clamp01(evidence.material_uv),
    }


def consensus_strength(
    evidence: BoundaryEvidence,
    view_weights: Mapping[str, float] = _DEFAULT_VIEW_WEIGHTS,
) -> float:
    """Robust ensemble score for one potential boundary.

    Agreement matters: one extreme channel should not create a strong boundary
    by itself. This is intentionally different from a single threshold split.
    """

    items = _evidence_items(evidence)
    weighted = []

    for name, value in items.items():
        weight = max(0.0, float(view_weights.get(name, 0.0)))
        if weight > 0.0:
            weighted.append((value, weight))

    if not weighted:
        return 0.0

    total_weight = sum(weight for _, weight in weighted)
    mean = sum(
        value * weight
        for value, weight in weighted
    ) / total_weight

    variance = sum(
        weight * (value - mean) ** 2
        for value, weight in weighted
    ) / total_weight

    agreement = math.exp(-2.6 * variance)
    quorum = (
        sum(1 for value, _ in weighted if value >= 0.58)
        / len(weighted)
    )

    score = mean * (
        0.62
        + 0.23 * agreement
        + 0.15 * quorum
    )

    return _clamp01(score)


def boundary_persistence(
    evidence: BoundaryEvidence,
    config: RegionConsensusConfig = RegionConsensusConfig(),
    view_weights: Mapping[str, float] = _DEFAULT_VIEW_WEIGHTS,
) -> float:
    """Measure whether a boundary survives controlled perturbations.

    Stable boundaries are more likely to describe real structural transitions;
    unstable threshold artifacts are downgraded and become review candidates.
    """

    rng = random.Random(
        config.random_seed
        + int(evidence.edge_id) * 1009
    )

    base_items = _evidence_items(evidence)
    survived = 0
    runs = max(1, int(config.perturbation_runs))

    for _ in range(runs):
        perturbed = {
            name: _clamp01(
                value
                + rng.gauss(0.0, config.evidence_jitter)
            )
            for name, value in base_items.items()
        }

        synthetic = BoundaryEvidence(
            edge_id=evidence.edge_id,
            topology=perturbed["topology"],
            dihedral=perturbed["dihedral"],
            curvature=perturbed["curvature"],
            thickness=perturbed["thickness"],
            normals=perturbed["normals"],
            geodesic=perturbed["geodesic"],
            level=perturbed["level"],
            symmetry=perturbed["symmetry"],
            material_uv=perturbed["material_uv"],
        )

        threshold = (
            config.boundary_threshold
            + rng.uniform(
                -config.threshold_jitter,
                config.threshold_jitter,
            )
        )

        if consensus_strength(
            synthetic,
            view_weights,
        ) >= threshold:
            survived += 1

    return survived / runs


def classify_boundary(
    evidence: BoundaryEvidence,
    config: RegionConsensusConfig = RegionConsensusConfig(),
    view_weights: Mapping[str, float] = _DEFAULT_VIEW_WEIGHTS,
) -> BoundaryConsensus:
    strength = consensus_strength(evidence, view_weights)
    persistence = boundary_persistence(
        evidence,
        config,
        view_weights,
    )
    confidence = _clamp01(
        0.55 * persistence
        + 0.45 * strength
    )

    is_boundary = (
        strength >= config.boundary_threshold
        and persistence >= config.min_persistence
    )

    return BoundaryConsensus(
        edge_id=evidence.edge_id,
        consensus=round(strength, 4),
        persistence=round(persistence, 4),
        confidence=round(confidence, 4),
        is_boundary=is_boundary,
    )


def classify_boundaries(
    evidences: Sequence[BoundaryEvidence],
    config: RegionConsensusConfig = RegionConsensusConfig(),
    view_weights: Mapping[str, float] = _DEFAULT_VIEW_WEIGHTS,
) -> List[BoundaryConsensus]:
    return [
        classify_boundary(
            item,
            config,
            view_weights,
        )
        for item in evidences
    ]
