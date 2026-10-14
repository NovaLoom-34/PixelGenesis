"""Validate single-kingdom CivSoak JSON against Phase B acceptance thresholds."""

import argparse
import json
import statistics
import sys
from pathlib import Path


def _number(value, default=0):
    return value if isinstance(value, (int, float)) and not isinstance(value, bool) else default


def _max_consecutive_population_collapse(samples):
    # Running peak: the founding ramp-up is not a collapse, only a fall from an earlier high is.
    if max((_number(row.get("Population")) for row in samples), default=0) <= 0:
        return len(samples)
    peak = longest = current = 0
    for row in samples:
        population = _number(row.get("Population"))
        peak = max(peak, population)
        if population < peak * 0.30:
            current += 1
            longest = max(longest, current)
        else:
            current = 0
    return longest


def evaluate_reports(reports, thresholds):
    errors = []
    required_seeds = [str(seed) for seed in thresholds["seeds"]]
    by_seed = {str(report.get("Seed")): report for report in reports}
    missing = [seed for seed in required_seeds if seed not in by_seed]
    if missing:
        errors.append(f"Missing economy soak seed reports: {', '.join(missing)}")
    if len(by_seed) != len(reports):
        errors.append("Duplicate economy soak seed report detected")
    selected = [by_seed[seed] for seed in required_seeds if seed in by_seed]
    if not selected:
        return errors or ["No economy soak reports provided"]

    city_counts, hall_tiers, human_peaks, famine_counts = [], [], [], []
    for report in selected:
        seed = report.get("Seed", "?")
        if report.get("Scenario") != "single" and not str(report.get("Scenario", "")).startswith("single-"):
            errors.append(f"Seed {seed}: expected a single-kingdom scenario")
        if int(_number(report.get("RequestedYears"))) < int(thresholds["years"]):
            errors.append(f"Seed {seed}: report is shorter than {thresholds['years']} years")
        samples = report.get("Samples") or []
        if len(samples) < int(thresholds["years"]):
            errors.append(f"Seed {seed}: insufficient yearly samples")
            continue
        summary = report.get("Summary") or {}
        city_counts.append(_number(summary.get("FoundedCities")))
        hall_tiers.append(_number(summary.get("MaxHallTier")))
        human_peaks.append(_number(summary.get("MaxHumanPopulation")))
        famine_counts.append(_number(summary.get("FamineEvents")))

        if _number(summary.get("CraftedItems")) < int(thresholds["minimumCraftedItemsPerSeed"]):
            errors.append(f"Seed {seed}: no meaningful crafted-item production")
        crafted = [_number(row.get("CraftedItems")) for row in samples]
        if any(later < earlier for earlier, later in zip(crafted, crafted[1:])) or not any(
            later > earlier for earlier, later in zip(crafted, crafted[1:])
        ):
            errors.append(f"Seed {seed}: crafted-item total is not cumulative and increasing")

        start_year = int(thresholds["maximumUnemploymentRateAfterYear"])
        idle_adults = adult_population = 0
        for row in samples:
            if _number(row.get("Year")) <= start_year:
                continue
            for city in row.get("CitiesById") or []:
                adult_population += _number(city.get("AdultPopulation"))
                idle_adults += _number(city.get("IdleAdults"))
        if adult_population <= 0:
            errors.append(f"Seed {seed}: unemployment could not be measured after year {start_year}")
        elif idle_adults / adult_population > float(thresholds["maximumAverageUnemploymentRate"]):
            errors.append(f"Seed {seed}: weighted unemployment exceeds {thresholds['maximumAverageUnemploymentRate']:.0%} after year {start_year}")

        gold_after_year = int(thresholds["maximumAbsoluteGoldAccumulationRateAfterYear"])
        gold_samples = [row for row in samples if _number(row.get("Year")) > gold_after_year]
        total_gold_income = sum(_number((row.get("GoldFlow") or {}).get("Income")) for row in gold_samples)
        total_gold_change = sum(_number((row.get("GoldFlow") or {}).get("NetChange")) for row in gold_samples)
        missing_gold = [
            row for row in gold_samples
            if not isinstance(row.get("GoldFlow"), dict) or "NetChange" not in row["GoldFlow"]
        ]
        maintenance_fields = ("MaintenanceDue", "MaintenancePaid", "MaintenanceUnpaid")
        missing_maintenance = [
            row for row in gold_samples
            if not isinstance(row.get("GoldFlow"), dict) or any(key not in row["GoldFlow"] for key in maintenance_fields)
        ]
        if missing_gold:
            errors.append(f"Seed {seed}: annual gold flow is missing after year {gold_after_year}")
        elif total_gold_income <= 0:
            errors.append(f"Seed {seed}: no measurable gold income after year {gold_after_year}")
        elif abs(total_gold_change) / total_gold_income > float(thresholds["maximumAbsoluteGoldAccumulationRate"]):
            errors.append(f"Seed {seed}: gold accumulation rate exceeds {thresholds['maximumAbsoluteGoldAccumulationRate']:.0%} of income after year {gold_after_year}")
        if missing_maintenance:
            errors.append(f"Seed {seed}: annual building maintenance is missing after year {gold_after_year}")
        else:
            maintenance_due = sum(_number(row["GoldFlow"]["MaintenanceDue"]) for row in gold_samples)
            maintenance_unpaid = sum(_number(row["GoldFlow"]["MaintenanceUnpaid"]) for row in gold_samples)
            if maintenance_due <= 0:
                errors.append(f"Seed {seed}: no building maintenance was assessed after year {gold_after_year}")
            elif maintenance_unpaid / maintenance_due > float(thresholds["maximumUnpaidMaintenanceRate"]):
                errors.append(f"Seed {seed}: unpaid building maintenance exceeds {thresholds['maximumUnpaidMaintenanceRate']:.0%}")

        for previous, current in zip(samples, samples[1:]):
            if "TotalGold" not in previous or "TotalGold" not in current or not current.get("GoldFlow"):
                errors.append(f"Seed {seed}: gold balance reconciliation fields are missing")
                break
            balance_change = _number(current.get("TotalGold")) - _number(previous.get("TotalGold"))
            ledger_change = _number(current["GoldFlow"].get("NetChange"))
            if balance_change != ledger_change:
                errors.append(f"Seed {seed}: gold ledger does not reconcile with the annual balance change")
                break

        collapse_years = _max_consecutive_population_collapse(samples)
        if collapse_years > int(thresholds["maximumPopulationCollapseYears"]):
            errors.append(f"Seed {seed}: population collapse stayed below 30% of its peak for {collapse_years} consecutive years")

    if len(city_counts) == len(required_seeds) and statistics.mean(city_counts) < float(thresholds["minimumFoundedCitiesAverage"]):
        errors.append("Average founded cities is below the Phase B target")
    if len(hall_tiers) == len(required_seeds) and statistics.mean(hall_tiers) < float(thresholds["minimumMaxHallTierAverage"]):
        errors.append("Average maximum hall tier is below the Phase B target")
    if hall_tiers and max(hall_tiers) < int(thresholds["minimumMaxHallTierInAnySeed"]):
        errors.append(f"No seed reached maximum hall tier {thresholds['minimumMaxHallTierInAnySeed']}")
    if len(human_peaks) == len(required_seeds) and statistics.mean(human_peaks) < float(thresholds["minimumMaxHumanPopulationAverage"]):
        errors.append("Average maximum human population is below the Phase B target")
    if len(famine_counts) == len(required_seeds) and statistics.mean(famine_counts) > float(thresholds["maximumFamineEventsAverage"]):
        errors.append("Average famine events exceeds the Phase B limit")
    return errors


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parent.parent)
    parser.add_argument("--thresholds", type=Path)
    parser.add_argument("--years", type=int, default=100)
    parser.add_argument("--seeds", type=int, nargs="+")
    args = parser.parse_args(argv)
    thresholds_path = args.thresholds or args.root / "Tools" / "gate_thresholds.json"
    with thresholds_path.open(encoding="utf-8") as stream:
        all_thresholds = json.load(stream)
    thresholds = all_thresholds["civilizationEconomy"]
    thresholds = dict(thresholds, years=args.years, seeds=args.seeds or thresholds["seeds"])
    reports = []
    for seed in thresholds["seeds"]:
        path = args.root / "Logs" / f"metrics-civ-single-{args.years}-{seed}.json"
        try:
            with path.open(encoding="utf-8") as stream:
                reports.append(json.load(stream))
        except (OSError, json.JSONDecodeError) as error:
            print(f"FAIL: cannot read {path}: {error}", file=sys.stderr)
    errors = evaluate_reports(reports, thresholds)
    if errors:
        for error in errors:
            print(f"FAIL: {error}", file=sys.stderr)
        return 1
    print(f"[CivEconomy] ALL MEASURED TARGETS OK — {len(reports)} seeds × {args.years} years, including reconciled gold flow")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
