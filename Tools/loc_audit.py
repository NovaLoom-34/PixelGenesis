"""Audit the bundled Turkish and English UI localization catalog."""
import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / "Data" / "ui_strings.json"
SCENARIOS = ROOT / "Data" / "scenarios.json"
SOURCE = ROOT / "Assets" / "_Project"
DATA = ROOT / "Data"
FORMATTED_CALL = re.compile(r'Loc\.(?:T|F)\(\s*"((?:\\.|[^"\\])*)"')
FORMAT_TOKEN = re.compile(r"\{(\d+)(?:,[^}:]+)?(?::[^}]+)?\}")
CS_STRING_OR_COMMENT = re.compile(r'//[^\r\n]*|/\*.*?\*/|"(?:\\.|[^"\\])*"', re.DOTALL)


def string_values(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, list):
        for item in value:
            yield from string_values(item)
    elif isinstance(value, dict):
        for item in value.values():
            yield from string_values(item)


def source_literal_keys(keys):
    """Return catalog keys spelled literally in production C# or content JSON."""
    code = set()
    for path in SOURCE.rglob("*.cs"):
        if "/Tests/" in path.as_posix():
            continue
        source = path.read_text(encoding="utf-8-sig")
        for match in CS_STRING_OR_COMMENT.finditer(source):
            token = match.group(0)
            if not token.startswith('"'):
                continue
            value = token[1:-1].replace(r'\"', '"').replace(r"\\", "\\")
            if value in keys:
                code.add(value)

    content = set()
    for path in DATA.rglob("*.json"):
        if path.name == "ui_strings.json" or "_schemas" in path.parts:
            continue
        try:
            payload = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue
        content.update(value for value in string_values(payload) if value in keys)
    return code, content


def source_dynamic_prefixes(keys):
    """Return catalog key families built from a literal prefix in production C#.

    UI helpers such as Small/Heading and key-valued properties can consume a
    constructed key without putting the prefix inside Loc.T/F itself. Only
    recognize specific ``ui.*.`` prefixes with catalog descendants; a broad
    ``ui.`` prefix would hide genuinely unused catalog entries.
    """
    prefixes = {}
    for path in SOURCE.rglob("*.cs"):
        if "/Tests/" in path.as_posix():
            continue
        source = path.read_text(encoding="utf-8-sig")
        for match in CS_STRING_OR_COMMENT.finditer(source):
            token = match.group(0)
            if not token.startswith('"') or not re.match(r"\s*\+", source[match.end():]):
                continue
            prefix = token[1:-1].replace(r'\"', '"').replace(r"\\", "\\")
            if not prefix.startswith("ui.") or not prefix.endswith(".") or prefix.count(".") < 2:
                continue
            prefixes.setdefault(prefix, set()).add(path.relative_to(ROOT).as_posix())
    return prefixes


def audit(show_unreferenced=False):
    data = json.loads(CATALOG.read_text(encoding="utf-8"))
    items = data.get("items", [])
    errors = []
    keys = {}
    for index, item in enumerate(items):
        key = item.get("id")
        if not isinstance(key, str) or not key.strip():
            errors.append(f"catalog item {index + 1}: missing id")
            continue
        if key in keys:
            errors.append(f"duplicate key {key} (items {keys[key] + 1} and {index + 1})")
        else:
            keys[key] = index
        for language in ("tr", "en"):
            value = item.get(language)
            if not isinstance(value, str) or not value.strip():
                errors.append(f"{key}: missing {language} translation")
        tr = item.get("tr")
        en = item.get("en")
        if isinstance(tr, str) and isinstance(en, str):
            tr_tokens = sorted(FORMAT_TOKEN.findall(tr))
            en_tokens = sorted(FORMAT_TOKEN.findall(en))
            if tr_tokens != en_tokens:
                errors.append(f"{key}: format placeholders differ (tr={tr_tokens}, en={en_tokens})")

    if data.get("count") != len(items):
        errors.append(f"catalog count says {data.get('count')!r}, contains {len(items)} items")

    # Scenario display names have an explicit TR/EN pair; descriptions and objective
    # labels are localization keys and must resolve through the same catalog.
    scenario_data = json.loads(SCENARIOS.read_text(encoding="utf-8"))
    scenarios = scenario_data.get("items", [])
    if scenario_data.get("count") != len(scenarios):
        errors.append(f"scenario catalog count says {scenario_data.get('count')!r}, contains {len(scenarios)} items")
    for index, scenario in enumerate(scenarios):
        label = scenario.get("id", f"scenario item {index + 1}")
        for field in ("name", "nameEn", "descriptionEn"):
            if not isinstance(scenario.get(field), str) or not scenario[field].strip():
                errors.append(f"{label}: missing player-facing {field}")
        description_key = scenario.get("description")
        if not isinstance(description_key, str) or description_key not in keys:
            errors.append(f"{label}: description localization key is missing from catalog ({description_key!r})")
        for group in ("goals", "failures"):
            for condition_index, condition in enumerate(scenario.get(group) or []):
                text_key = condition.get("text") if isinstance(condition, dict) else None
                if not isinstance(text_key, str) or text_key not in keys:
                    errors.append(f"{label}.{group}[{condition_index}]: text localization key is missing from catalog ({text_key!r})")

    referenced = {}
    dynamic_prefixes = {}
    for path in SOURCE.rglob("*.cs"):
        if "/Tests/" in path.as_posix():
            continue
        source = path.read_text(encoding="utf-8-sig")
        for match in FORMATTED_CALL.finditer(source):
            # The pattern only accepts literal first arguments. Ignore escaped C# sequences
            # after decoding the simple escapes used in localization keys.
            key = match.group(1).replace(r'\"', '"').replace(r"\\", "\\")
            if re.match(r"\s*\+", source[match.end():]):
                dynamic_prefixes.setdefault(key, set()).add(path.relative_to(ROOT).as_posix())
                continue
            referenced.setdefault(key, set()).add(path.relative_to(ROOT).as_posix())

    constructed_prefixes = source_dynamic_prefixes(set(keys))
    missing = sorted(set(referenced) - set(keys))
    for key in missing:
        locations = ", ".join(sorted(referenced[key])[:3])
        errors.append(f"source references missing localization key {key} ({locations})")
    for prefix, paths in sorted(dynamic_prefixes.items()):
        if not any(key.startswith(prefix) for key in keys):
            locations = ", ".join(sorted(paths)[:3])
            errors.append(f"dynamic localization prefix has no catalog entries: {prefix} ({locations})")

    for prefix, paths in sorted(constructed_prefixes.items()):
        if not any(key.startswith(prefix) for key in keys):
            locations = ", ".join(sorted(paths)[:3])
            errors.append(f"constructed localization prefix has no catalog entries: {prefix} ({locations})")

    code_literals, content_literals = source_literal_keys(set(keys))
    likely_used = set(referenced) | code_literals | content_literals
    likely_used.update(key for key in keys if any(key.startswith(prefix) for prefix in dynamic_prefixes))
    likely_used.update(key for key in keys if any(key.startswith(prefix) for prefix in constructed_prefixes))
    unused_candidates = sorted(set(keys) - likely_used)

    if errors:
        for error in errors:
            print(f"FAIL: {error}")
        return 1
    print(
        f"PASS: {len(items)} catalog entries; tr/en values and placeholders, "
        f"{len(referenced)} literal Loc keys, {len(code_literals)} other C# literals, "
        f"{len(content_literals)} content references, {len(constructed_prefixes)} constructed key prefixes, "
        f"and {len(scenarios)} bilingual scenarios are consistent."
    )
    if unused_candidates:
        print(f"NOTE: {len(unused_candidates)} catalog keys have no verified source or content reference; inspect as possible unused entries.")
        if show_unreferenced:
            for key in unused_candidates:
                print(f"UNREFERENCED: {key}")
    return 0


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--show-unreferenced", action="store_true", help="List catalog keys without a source/content reference")
    args = parser.parse_args()
    sys.exit(audit(show_unreferenced=args.show_unreferenced))
