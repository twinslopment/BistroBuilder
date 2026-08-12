from assets4all_core.grounding import analyse_grounding
from assets4all_core.models import (
    BoundaryEvidence,
    ConversionRiskInputs,
    GroundingInputs,
    GroundingSample,
    Metric,
    ViabilityInputs,
)
from assets4all_core.region_consensus import classify_boundary
from assets4all_core.scoring import (
    conversion_success_estimate,
    processing_viability_score,
    resolve_dual_decision,
)


def metric(name, value, confidence=1.0):
    return Metric(
        name=name,
        value=value,
        confidence=confidence,
    )


def run_smoke_test():
    pvs = processing_viability_score(
        ViabilityInputs(
            geometry_integrity=metric("geometry", 92),
            topology=metric("topology", 84),
            uv_readiness=metric("uv", 95),
            transform_orientation=metric("transform", 98),
            scale_plausibility=metric("scale", 91),
            artifact_severity_inverse=metric("artifact", 80),
            region_separability=metric("regions", 71),
            symmetry_repetition=metric("symmetry", 90),
            optimization_headroom=metric("optimization", 83),
            profile_plausibility=metric("profile", 96),
        )
    )

    cse = conversion_success_estimate(
        ConversionRiskInputs(
            repair_success_probability=0.96,
            segmentation_success_probability=0.84,
            semantic_assignment_probability=0.86,
            grounding_success_probability=0.99,
            optimization_success_probability=0.97,
            export_success_probability=0.995,
            ambiguous_decisions=1,
            predicted_review_seconds=12,
        )
    )

    dual = resolve_dual_decision(pvs, cse)

    assert 0.0 <= pvs.score <= 100.0
    assert 0.0 <= cse.score <= 100.0
    assert dual.final_decision.value in {
        "AUTO",
        "REVIEW",
        "REGENERATE",
    }

    ground = analyse_grounding(
        GroundingInputs(
            samples=[
                GroundingSample(0.0001, 1, 2.0),
                GroundingSample(0.0002, 2, 2.0),
                GroundingSample(0.0001, 3, 2.0),
                GroundingSample(0.0002, 4, 2.0),
                GroundingSample(0.45, None, 8.0),
                GroundingSample(0.86, None, 4.0),
            ]
        )
    )

    assert ground.state.value in {"PASS", "REVIEW"}

    boundary = classify_boundary(
        BoundaryEvidence(
            edge_id=12,
            topology=0.85,
            dihedral=0.92,
            curvature=0.88,
            thickness=0.74,
            normals=0.80,
            geodesic=0.78,
            level=0.62,
            symmetry=0.55,
            material_uv=0.90,
        )
    )

    assert 0.0 <= boundary.persistence <= 1.0
    assert 0.0 <= boundary.confidence <= 1.0


if __name__ == "__main__":
    run_smoke_test()
    print("Assets4All foundation smoke test passed.")
