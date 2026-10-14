# Soak metric and historical visual baselines

This directory contains historical logs and PNG screenshots as well as any accepted `metrics-*.json` reports. The old logs and screenshots are diagnostic artifacts; they are not automatically approved metric or visual baselines.

## Soak metrics

Only measured `metrics-*.json` reports from an accepted run count as soak baselines. Do not hand-author or infer a baseline from old text logs.

After the user-authorized final test run, preserve one report per `(Seed, Scenario)` from the accepted build in a named folder, for example `Tools/baselines/accepted-2026-10/`. Keep each seed's report for the same scenario and requested year count. Compare a candidate report set with:

```powershell
python Tools/metrics_compare.py Tools/baselines/accepted-2026-10 Logs
```

The comparer rejects duplicate seed/scenario keys and returns a failing exit code when either side is missing a key. A metric baseline is intentionally absent until the soak suite is actually run and accepted; existing text logs are not machine-readable substitutes.

## Screenshot comparison

Use `python Tools/visual_compare.py <reference.png-or-folder> <candidate.png-or-folder>` for a pixel-difference report. Add `--max-changed-pct 2` to enforce a chosen threshold. Without a threshold, the command is report-only. Historical PNGs here can be used for exploratory comparisons, but select and accept a stable reference set before treating a threshold as release criteria.
