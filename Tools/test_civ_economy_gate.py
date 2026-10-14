import unittest

from civ_economy_gate import evaluate_reports


THRESHOLDS = {
    "years": 100,
    "seeds": [11, 22],
    "minimumFoundedCitiesAverage": 5,
    "minimumMaxHallTierAverage": 2,
    "minimumMaxHallTierInAnySeed": 3,
    "minimumMaxHumanPopulationAverage": 200,
    "maximumFamineEventsAverage": 6,
    "minimumCraftedItemsPerSeed": 1,
    "maximumUnemploymentRateAfterYear": 30,
    "maximumAverageUnemploymentRate": 0.1,
    "maximumAbsoluteGoldAccumulationRateAfterYear": 30,
    "maximumAbsoluteGoldAccumulationRate": 0.1,
    "maximumUnpaidMaintenanceRate": 0.1,
    "maximumPopulationCollapseYears": 20,
}


def report(seed, *, cities=5, hall=2, max_hall=3, humans=200, famines=4,
           unemployed=0.05, crafts=1, collapse=False):
    samples = []
    total_gold = 0
    for year in range(1, 101):
        population = 100 if not collapse or year < 10 or year > 31 else 20
        total_gold += 10
        samples.append({
            "Year": year,
            "Population": population,
            "TotalGold": total_gold,
            "GoldFlow": {"Income": 100, "Expenses": 90, "TransfersIn": 0,
                         "TransfersOut": 0, "RouteFees": 0, "NetChange": 10,
                         "MaintenanceDue": 10, "MaintenancePaid": 10, "MaintenanceUnpaid": 0},
            "CraftedItems": crafts if year >= 20 else 0,
            "Cities": cities,
            "MaxHallTier": max_hall if year == 100 else hall,
            "HumanPopulation": humans,
            "CitiesById": [{"AdultPopulation": 20, "IdleAdults": int(20 * unemployed)}],
        })
    return {
        "Seed": str(seed), "Scenario": "single", "RequestedYears": 100,
        "Samples": samples,
        "Summary": {"FoundedCities": cities, "MaxHallTier": max_hall,
                    "MaxHumanPopulation": humans, "FamineEvents": famines,
                    "CraftedItems": crafts},
    }


class CivEconomyGateTests(unittest.TestCase):
    def test_accepts_reports_meeting_all_measured_targets(self):
        self.assertEqual([], evaluate_reports([report(11, max_hall=2), report(22)], THRESHOLDS))

    def test_rejects_missing_configured_seed(self):
        errors = evaluate_reports([report(11)], THRESHOLDS)
        self.assertTrue(any("missing economy soak seed" in error.lower() for error in errors))

    def test_rejects_missing_seed_and_economy_regressions(self):
        bad = report(11, cities=1, hall=1, max_hall=1, humans=30,
                     famines=20, unemployed=0.4, crafts=0, collapse=True)
        errors = evaluate_reports([bad, report(22, cities=1, hall=1, max_hall=1, humans=30,
                                                famines=20, unemployed=0.4, crafts=0, collapse=True)], THRESHOLDS)
        self.assertTrue(any("seed" in error.lower() for error in errors))
        for target in ("cities", "hall", "human", "famine", "craft", "unemployment", "collapse"):
            self.assertTrue(any(target in error.lower() for error in errors), target)

    def test_rejects_non_increasing_crafted_items(self):
        value = report(11)
        value["Samples"][60]["CraftedItems"] = 0
        self.assertTrue(any("craft" in error.lower() for error in evaluate_reports([value, report(22)], THRESHOLDS)))

    def test_founding_ramp_up_is_not_a_population_collapse(self):
        value = report(11)
        for row in value["Samples"]:
            row["Population"] = 10 + row["Year"] * 3  # 13..310, below 30% of the final peak for 90+ years
        errors = evaluate_reports([value, report(22)], THRESHOLDS)
        self.assertFalse(any("collapse" in error.lower() for error in errors), errors)

    def test_requires_at_least_one_seed_to_reach_hall_tier_three(self):
        errors = evaluate_reports([report(11, max_hall=2), report(22, max_hall=2)], THRESHOLDS)
        self.assertTrue(any("no seed reached maximum hall tier 3" in error.lower() for error in errors))

    def test_rejects_unbalanced_gold_accumulation(self):
        value = report(11)
        balance = value["Samples"][29]["TotalGold"]
        for row in value["Samples"][30:]:
            row["GoldFlow"].update({"Expenses": 80, "NetChange": 20})
            balance += 20
            row["TotalGold"] = balance
        errors = evaluate_reports([value, report(22)], THRESHOLDS)
        self.assertTrue(any("gold accumulation rate" in error.lower() for error in errors))

    def test_rejects_gold_ledger_that_does_not_match_balance(self):
        value = report(11)
        value["Samples"][50]["TotalGold"] += 1
        errors = evaluate_reports([value, report(22)], THRESHOLDS)
        self.assertTrue(any("gold ledger does not reconcile" in error.lower() for error in errors))

    def test_rejects_missing_annual_gold_flow(self):
        value = report(11)
        del value["Samples"][50]["GoldFlow"]
        errors = evaluate_reports([value, report(22)], THRESHOLDS)
        self.assertTrue(any("annual gold flow is missing" in error.lower() for error in errors))

    def test_rejects_unpaid_building_maintenance(self):
        value = report(11)
        for row in value["Samples"][30:]:
            row["GoldFlow"].update({"MaintenanceDue": 10, "MaintenancePaid": 7, "MaintenanceUnpaid": 3})
        errors = evaluate_reports([value, report(22)], THRESHOLDS)
        self.assertTrue(any("unpaid building maintenance" in error.lower() for error in errors))

    def test_rejects_malformed_gold_flow_without_crashing(self):
        value = report(11)
        value["Samples"][50]["GoldFlow"] = None
        errors = evaluate_reports([value, report(22)], THRESHOLDS)
        self.assertTrue(any("annual gold flow is missing" in error.lower() for error in errors))


if __name__ == "__main__":
    unittest.main()
