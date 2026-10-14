import json
import io
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path

from metrics_compare import compare_metrics, main


class MetricsCompareTests(unittest.TestCase):
    def test_reports_final_civilized_and_wild_populations_separately(self):
        report = {
            "Summary": {"FoundedCities": 1, "MaxHallTier": 2},
            "Samples": [
                {"Year": 1, "Population": 6, "WildPopulation": 10, "LivingKingdoms": 1,
                 "Cities": 1, "CitiesById": []},
                {"Year": 2, "Population": 0, "WildPopulation": 170, "LivingKingdoms": 0,
                 "Cities": 0, "CitiesById": []},
            ],
        }

        metrics = compare_metrics(report)

        self.assertEqual(0, metrics["LastCivPopulation"])
        self.assertEqual(170, metrics["LastWildPopulation"])
        self.assertEqual(1, metrics["YearsWithoutCivilization"])
        self.assertIsNone(metrics["FinalUnaffordableHallCities"])

    def test_reports_final_hall_blockers_and_resource_deficits(self):
        report = {
            "Summary": {},
            "Samples": [{
                "Year": 5, "Population": 12, "WildPopulation": 30, "LivingKingdoms": 1,
                "Cities": 2,
                "CitiesById": [
                    {"NextHallAvailable": True, "NextHallAffordable": False,
                     "NextHallHasSite": False,
                     "NextHallMissingResources": [{"Id": "res.stone", "Count": 5}]},
                    {"NextHallAvailable": True, "NextHallAffordable": True,
                     "NextHallHasSite": False, "NextHallMissingResources": []},
                    {"NextHallAvailable": False, "NextHallAffordable": False,
                     "NextHallHasSite": False, "NextHallMissingResources": []},
                ],
            }],
        }

        metrics = compare_metrics(report)

        self.assertEqual(1, metrics["FinalUnaffordableHallCities"])
        self.assertEqual(2, metrics["FinalHallCitiesWithoutSite"])
        self.assertEqual({"res.stone": 5}, metrics["FinalHallResourceDeficits"])

    def test_reports_market_price_deviation_only_when_market_samples_exist(self):
        report = {"Summary": {}, "Samples": [
            {"Year": 4, "MarketPriceSampleCount": 8,
             "AverageMarketPriceDeviation": 0.125, "CitiesById": []},
        ]}
        metrics = compare_metrics(report)
        self.assertEqual(8, metrics["FinalMarketPriceSamples"])
        self.assertAlmostEqual(0.125, metrics["FinalMarketPriceDeviation"])

        report["Samples"][0]["MarketPriceSampleCount"] = 0
        metrics = compare_metrics(report)
        self.assertIsNone(metrics["FinalMarketPriceSamples"])
        self.assertIsNone(metrics["FinalMarketPriceDeviation"])

    def test_cli_compares_matching_reports_and_marks_old_fields_unavailable(self):
        baseline = {"Seed": "7", "Scenario": "single", "Summary": {}, "Samples": [
            {"Year": 1, "Population": 0, "WildPopulation": 20, "LivingKingdoms": 0,
             "Cities": 0, "CitiesById": []},
        ]}
        candidate = {"Seed": "7", "Scenario": "single", "Summary": {}, "Samples": [
            {"Year": 1, "Population": 5, "WildPopulation": 15, "LivingKingdoms": 1,
             "Cities": 1, "CitiesById": [{"NextHallAvailable": True,
                                          "NextHallAffordable": True,
                                          "NextHallHasSite": True,
                                          "NextHallMissingResources": []}]},
        ]}
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            before, after = root / "before.json", root / "after.json"
            before.write_text(json.dumps(baseline), encoding="utf-8")
            after.write_text(json.dumps(candidate), encoding="utf-8")
            import sys
            old_argv = sys.argv
            try:
                sys.argv = ["metrics_compare.py", str(before), str(after)]
                output = io.StringIO()
                with redirect_stdout(output):
                    self.assertEqual(0, main())
                rendered = output.getvalue()
                self.assertIn("final civ population", rendered)
                self.assertIn("0.000", rendered)
                self.assertIn("final cities unable to afford hall", rendered)
                self.assertIn("n/a", rendered)
            finally:
                sys.argv = old_argv


if __name__ == "__main__":
    unittest.main()
