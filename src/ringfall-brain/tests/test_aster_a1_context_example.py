import json
import unittest
from pathlib import Path


EXAMPLE_PATH = (
    Path(__file__).resolve().parents[1]
    / "examples"
    / "aster-a1-context.example.json"
)


class AsterA1ContextExampleTests(unittest.TestCase):
    def test_example_is_bounded_actor_context_evidence(self) -> None:
        context = json.loads(EXAMPLE_PATH.read_text(encoding="utf-8"))

        self.assertEqual("A1", context["actorId"])
        self.assertEqual("L1", context["layer"])
        self.assertEqual(
            {
                "actorId",
                "displayName",
                "role",
                "layer",
                "observations",
                "crewRefs",
                "toolRefs",
            },
            set(context),
        )
        self.assertEqual(1, len(context["observations"]))
        self.assertEqual(
            {"observationId", "kind", "signal", "sourceRef"},
            set(context["observations"][0]),
        )
        self.assertEqual(
            "event:event-t000-aster-r2-heat-alarm",
            context["observations"][0]["sourceRef"],
        )
        self.assertEqual(["crew_aster_repair_02"], context["crewRefs"])
        self.assertEqual(
            ["local_grid_panel", "maintenance_console"], context["toolRefs"]
        )
        self.assertTrue(all(isinstance(ref, str) for ref in context["crewRefs"]))
        self.assertTrue(all(isinstance(ref, str) for ref in context["toolRefs"]))

        serialized = json.dumps(context)
        for forbidden in (
            "thermalDebt",
            "debtLevel",
            "systemRefs",
            "availability",
            "capability",
            "authority",
            "supportedActions",
            "packetType",
            "executionResult",
        ):
            self.assertNotIn(forbidden, serialized)


if __name__ == "__main__":
    unittest.main()
