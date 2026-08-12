from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import Optional, Sequence, Tuple

class Decision(str, Enum):
    AUTO = "AUTO"
    STANDARD_REPAIR = "STANDARD_REPAIR"
    DEEP_REPAIR = "DEEP_REPAIR"
    REVIEW = "REVIEW"
    REGENERATE = "REGENERATE"

class GateState(str, Enum):
    PASS = "PASS"
    REVIEW = "REVIEW"
    FAIL = "FAIL"
    NA = "N/A"

@dataclass(frozen=True)
class Metric:
    name: str
    value: float
    confidence: float = 1.0
    note: str = ""
    def clamped(self) -> "Metric":
        return Metric(self.name, max(0.0, min(100.0, float(self.value))), max(0.0, min(1.0, float(self.confidence))), self.note)

@dataclass
class ViabilityInputs:
    geometry_integrity: Metric
    topology: Metric
    uv_readiness: Metric
    transform_orientation: Metric
    scale_plausibility: Metric
    artifact_severity_inverse: Metric
    region_separability: Metric
    symmetry_repetition: Metric
    optimization_headroom: Metric
    profile_plausibility: Metric

@dataclass
class ConversionRiskInputs:
    repair_success_probability: float
    segmentation_success_probability: float
    semantic_assignment_probability: float
    grounding_success_probability: float
    optimization_success_probability: float
    export_success_probability: float
    ambiguous_decisions: int = 0
    predicted_review_seconds: float = 0.0
    review_budget_seconds: float = 30.0
    severe_failure_flags: Sequence[str] = field(default_factory=tuple)

@dataclass(frozen=True)
class ScoreResult:
    score: float
    decision: Decision
    confidence: float
    reasons: Tuple[str, ...] = ()

@dataclass(frozen=True)
class DualDecision:
    pvs: ScoreResult
    cse: ScoreResult
    final_decision: Decision
    disagreement: float
    reasons: Tuple[str, ...] = ()

@dataclass(frozen=True)
class GroundingSample:
    z: float
    cluster_id: Optional[int] = None
    area_weight: float = 1.0

@dataclass
class GroundingInputs:
    samples: Sequence[GroundingSample]
    penetration_tolerance_m: float = 0.001
    float_tolerance_m: float = 0.0015
    support_band_m: float = 0.003
    robust_percentile: float = 0.01
    min_support_fraction: float = 0.002

@dataclass(frozen=True)
class GroundingResult:
    state: GateState
    translation_z: float
    absolute_min_z: float
    robust_support_z: float
    support_weight: float
    support_fraction: float
    below_ground_weight: float
    message: str

@dataclass(frozen=True)
class RegionDescriptor:
    region_id: int
    centroid: Tuple[float, float, float]
    area: float
    volume_estimate: float
    aspect_ratios: Tuple[float, float, float]
    mean_curvature: float
    thickness_mean: float
    ground_distance: float
    symmetry_group: Optional[int]
    repetition_group: Optional[int]
    neighbors: Tuple[int, ...]
    source_object_ids: Tuple[str, ...] = ()
    material_slots: Tuple[str, ...] = ()
    uv_islands: Tuple[int, ...] = ()

@dataclass(frozen=True)
class BoundaryEvidence:
    edge_id: int
    topology: float
    dihedral: float
    curvature: float
    thickness: float
    normals: float
    geodesic: float
    level: float
    symmetry: float
    material_uv: float

@dataclass(frozen=True)
class BoundaryConsensus:
    edge_id: int
    consensus: float
    persistence: float
    confidence: float
    is_boundary: bool
