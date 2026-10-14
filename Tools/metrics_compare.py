#!/usr/bin/env python3
"""Compare CivSoak JSON reports by seed: metrics_compare.py <baseline> <candidate>.

Each input may be one report file or a directory containing metrics-*.json files.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path


SUMMARY_METRICS = (
    ("FoundedCities", "cities"),
    ("MaxHallTier", "max hall"),
    ("MaxHumanPopulation", "max humans"),
    ("MaxLivingKingdoms", "max kingdoms"),
    ("FamineEvents", "famines"),
    ("DeclaredWars", "wars"),
    ("PeaceEvents", "peace"),
    ("CraftedItems", "items"),
    ("CivMillisecondsPerTick", "civ ms/tick"),
    ("TotalMillisecondsPerTick", "total ms/tick"),
)

END_STATE_METRICS = (
    ("LastCivPopulation", "final civ population"),
    ("LastWildPopulation", "final wild population"),
    ("LastLivingKingdoms", "final living kingdoms"),
    ("LastCities", "final cities"),
    ("YearsWithoutCivilization", "years without civ population"),
    ("FinalUnaffordableHallCities", "final cities unable to afford hall"),
    ("FinalHallCitiesWithoutSite", "final cities without hall site"),
    ("FinalMarketPriceDeviation", "final market price deviation"),
    ("FinalMarketPriceSamples", "final market price samples"),
)


def reports(path: Path) -> dict[tuple[int, str], tuple[Path, dict]]:
    files = sorted(path.glob("metrics-*.json")) if path.is_dir() else [path]
    result = {}
    for file in files:
        with file.open(encoding="utf-8") as stream:
            report = json.load(stream)
        key = (int(report["Seed"]), str(report["Scenario"]))
        if key in result:
            raise ValueError(f"duplicate seed/scenario {key}: {result[key][0]} and {file}")
        result[key] = (file, report)
    return result


def compare_metrics(report: dict) -> dict[str, object]:
    """Flatten summary and end-of-run diagnostics without inventing old fields."""
    summary = report.get("Summary") or {}
    samples = report.get("Samples") or []
    values: dict[str, object] = {
        field: summary.get(field) for field, _ in SUMMARY_METRICS
    }
    if not samples:
        values.update({field: None for field, _ in END_STATE_METRICS})
        return values

    last = samples[-1]
    values.update({
        "LastCivPopulation": last.get("Population"),
        "LastWildPopulation": last.get("WildPopulation"),
        "LastLivingKingdoms": last.get("LivingKingdoms"),
        "LastCities": last.get("Cities"),
        "YearsWithoutCivilization": sum(
            1 for sample in samples
            if "Population" in sample and sample["Population"] == 0
        ),
    })
    market_sample_count = int(last.get("MarketPriceSampleCount", 0) or 0)
    values["FinalMarketPriceSamples"] = market_sample_count if market_sample_count > 0 else None
    values["FinalMarketPriceDeviation"] = (
        last.get("AverageMarketPriceDeviation") if market_sample_count > 0 else None
    )

    final_cities = last.get("CitiesById") or []
    has_hall_diagnostics = any("NextHallAvailable" in city for city in final_cities)
    if has_hall_diagnostics:
        hall_cities = [city for city in final_cities if city.get("NextHallAvailable")]
        values["FinalUnaffordableHallCities"] = sum(
            not city.get("NextHallAffordable", False) for city in hall_cities
        )
        values["FinalHallCitiesWithoutSite"] = sum(
            not city.get("NextHallHasSite", False) for city in hall_cities
        )
        deficits: dict[str, int] = {}
        for city in hall_cities:
            for resource in city.get("NextHallMissingResources") or []:
                resource_id = str(resource.get("Id", "unknown"))
                deficits[resource_id] = deficits.get(resource_id, 0) + int(resource.get("Count", 0))
        values["FinalHallResourceDeficits"] = deficits
    else:
        values["FinalUnaffordableHallCities"] = None
        values["FinalHallCitiesWithoutSite"] = None
        values["FinalHallResourceDeficits"] = None
    return values


def format_value(value: float | int | None) -> str:
    return "n/a" if value is None else f"{value:.3f}"


def main() -> int:
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    left, right = reports(Path(sys.argv[1])), reports(Path(sys.argv[2]))
    keys = sorted(left.keys() & right.keys())
    missing_left, missing_right = sorted(right.keys() - left.keys()), sorted(left.keys() - right.keys())
    if not keys:
        print("No matching seed/scenario reports.")
        return 1
    print("Seed  Scenario  Metric                             Baseline  Candidate  Delta")
    print("----  --------  ---------------------------------  --------  ---------  -----")
    for key in keys:
        baseline = compare_metrics(left[key][1])
        candidate = compare_metrics(right[key][1])
        metrics = [(field, label) for field, label in SUMMARY_METRICS + END_STATE_METRICS]
        resource_ids = sorted(
            set(baseline.get("FinalHallResourceDeficits") or {})
            | set(candidate.get("FinalHallResourceDeficits") or {})
        )
        metrics.extend((f"HallDeficit:{resource_id}", f"final missing {resource_id}")
                       for resource_id in resource_ids)
        for field, label in metrics:
            if field.startswith("HallDeficit:"):
                resource_id = field.split(":", 1)[1]
                old_map = baseline.get("FinalHallResourceDeficits")
                new_map = candidate.get("FinalHallResourceDeficits")
                old = None if old_map is None else old_map.get(resource_id, 0)
                new = None if new_map is None else new_map.get(resource_id, 0)
            else:
                old, new = baseline.get(field), candidate.get(field)
            delta = "n/a" if old is None or new is None else f"{new-old:+.3f}"
            print(f"{key[0]:>4}  {key[1]:<8}  {label:<33}  {format_value(old):>8}  {format_value(new):>9}  {delta}")
    if missing_left:
        print(f"Missing from baseline: {missing_left}")
    if missing_right:
        print(f"Missing from candidate: {missing_right}")
    return 0 if not missing_left and not missing_right else 1


if __name__ == "__main__":
    raise SystemExit(main())
