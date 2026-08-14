from __future__ import annotations

import unittest

from assets4all_core.grounding import analyse_grounding
from assets4all_core.models import (
    ConversionRiskInputs,
    Decision,
    GateState,
    GroundingInputs,
    GroundingSample,
    Metric,
    ViabilityInputs,
)
from assets4all_core.scoring import (
    conversion_success_estimate,
    processing_viability_score,
    resolve_dual_decision,
)


class Assets4AllFoundationTests(unittest.TestCase):
    def test_high_quality_can_be_auto(self):
        pvs = processing_viability_score(
            ViabilityInputs(*[Metric("m", 95.0) for _ in range(10)])
        )
        cse = conversion_success_estimate(
            ConversionRiskInputs(0.98, 0.97, 0.97, 0.99, 0.98, 0.99)
        )
        decision = resolve_dual_decision(pvs, cse)
        self.assertEqual(decision.final_decision, Decision.AUTO)

    def test_low_score_requests_deep_repair_not_immediate_regeneration(self):
        pvs = processing_viability_score(
            ViabilityInputs(*[Metric("m", 40.0) for _ in range(10)])
        )
        self.assertEqual(pvs.decision, Decision.DEEP_REPAIR)

    def test_estimator_disagreement_selects_repair_path(self):
        pvs = processing_viability_score(
            ViabilityInputs(*[Metric("m", 92.0) for _ in range(10)])
        )
        cse = conversion_success_estimate(
            ConversionRiskInputs(0.75, 0.72, 0.70, 0.95, 0.90, 0.98, 3, 24, 30)
        )
        decision = resolve_dual_decision(pvs, cse)
        self.assertIn(decision.final_decision, {Decision.STANDARD_REPAIR, Decision.DEEP_REPAIR})

    def test_grounded_support_passes(self):
        result = analyse_grounding(
            GroundingInputs(
                samples=[
                    GroundingSample(0.0000, area_weight=1.0),
                    GroundingSample(0.0001, area_weight=1.0),
                    GroundingSample(0.0002, area_weight=1.0),
                    GroundingSample(0.0000, area_weight=1.0),
                ]
            )
        )
        self.assertEqual(result.state, GateState.PASS)

    def test_localized_downward_spike_is_not_silently_grounded(self):
        result = analyse_grounding(
            GroundingInputs(
                samples=[
                    GroundingSample(-0.020, area_weight=0.001),
                    GroundingSample(0.0000, area_weight=1.0),
                    GroundingSample(0.0001, area_weight=1.0),
                    GroundingSample(0.0002, area_weight=1.0),
                    GroundingSample(0.0000, area_weight=1.0),
                ]
            )
        )
        self.assertEqual(result.state, GateState.FAIL)


if __name__ == "__main__":
    unittest.main()
