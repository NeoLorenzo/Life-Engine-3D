"""
Machine-readable asset-pack specification loading and validation.

Uses PyYAML when available. A deliberately small fallback parser supports the
subset of YAML used by asset_pipeline/templates/asset_pack_spec.yaml so Blender
runs do not depend on a separately installed package.
"""

from __future__ import annotations

import ast
import os
from copy import deepcopy


class SpecError(ValueError):
    pass


def _strip_comment(line: str) -> str:
    quote = None
    escaped = False
    for i, ch in enumerate(line):
        if escaped:
            escaped = False
            continue
        if ch == "\\" and quote:
            escaped = True
            continue
        if ch in ("'", '"'):
            if quote == ch:
                quote = None
            elif quote is None:
                quote = ch
        elif ch == "#" and quote is None:
            if i == 0 or line[i - 1].isspace():
                return line[:i]
    return line


def _parse_scalar(value: str):
    value = value.strip()
    if not value:
        return ""
    lowered = value.lower()
    if lowered in ("null", "~"):
        return None
    if lowered == "true":
        return True
    if lowered == "false":
        return False

    if value[0:1] in ("[", "{", "'", '"'):
        try:
            return ast.literal_eval(value)
        except (ValueError, SyntaxError):
            pass

    try:
        if any(c in value.lower() for c in (".", "e")):
            return float(value)
        return int(value)
    except ValueError:
        return value


def _fallback_yaml_load(text: str):
    tokens = []
    for raw in text.splitlines():
        cleaned = _strip_comment(raw).rstrip()
        if not cleaned.strip():
            continue
        indent = len(cleaned) - len(cleaned.lstrip(" "))
        if "\t" in cleaned[:indent]:
            raise SpecError("Tabs are not supported in asset-pack YAML indentation.")
        tokens.append((indent, cleaned.lstrip(" ")))

    if not tokens:
        return {}

    def parse_block(index: int, indent: int):
        if index >= len(tokens):
            return None, index
        if tokens[index][0] != indent:
            raise SpecError(
                f"Invalid indentation near '{tokens[index][1]}': "
                f"expected {indent} spaces, got {tokens[index][0]}."
            )
        if tokens[index][1].startswith("-"):
            return parse_list(index, indent)
        return parse_mapping(index, indent)

    def parse_mapping(index: int, indent: int):
        result = {}
        while index < len(tokens):
            current_indent, content = tokens[index]
            if current_indent < indent:
                break
            if current_indent > indent:
                raise SpecError(f"Unexpected indentation near '{content}'.")
            if content.startswith("-"):
                break
            if ":" not in content:
                raise SpecError(f"Expected 'key: value' near '{content}'.")
            key, raw_value = content.split(":", 1)
            key = key.strip()
            raw_value = raw_value.strip()
            index += 1
            if raw_value:
                result[key] = _parse_scalar(raw_value)
            elif index < len(tokens) and tokens[index][0] > indent:
                child_indent = tokens[index][0]
                child, index = parse_block(index, child_indent)
                result[key] = child
            else:
                result[key] = {}
        return result, index

    def parse_list(index: int, indent: int):
        result = []
        while index < len(tokens):
            current_indent, content = tokens[index]
            if current_indent < indent:
                break
            if current_indent != indent or not content.startswith("-"):
                break

            rest = content[1:].strip()
            index += 1

            if not rest:
                if index >= len(tokens) or tokens[index][0] <= indent:
                    result.append(None)
                else:
                    child, index = parse_block(index, tokens[index][0])
                    result.append(child)
                continue

            if ":" not in rest:
                result.append(_parse_scalar(rest))
                continue

            key, raw_value = rest.split(":", 1)
            item = {key.strip(): _parse_scalar(raw_value) if raw_value.strip() else {}}

            if not raw_value.strip() and index < len(tokens) and tokens[index][0] > indent:
                child, index = parse_block(index, tokens[index][0])
                item[key.strip()] = child

            if index < len(tokens) and tokens[index][0] > indent:
                continuation_indent = tokens[index][0]
                continuation, index = parse_mapping(index, continuation_indent)
                item.update(continuation)

            result.append(item)
        return result, index

    root, next_index = parse_block(0, tokens[0][0])
    if next_index != len(tokens):
        raise SpecError(f"Could not parse asset-pack YAML near '{tokens[next_index][1]}'.")
    return root


def load_pack_spec(path: str):
    with open(path, "r", encoding="utf-8") as handle:
        text = handle.read()

    try:
        import yaml  # type: ignore
    except ImportError:
        data = _fallback_yaml_load(text)
    else:
        data = yaml.safe_load(text)

    if not isinstance(data, dict):
        raise SpecError("Asset-pack specification root must be a mapping.")

    validate_pack_spec(data)
    return data


def merge_asset_defaults(pack_spec: dict, asset_spec: dict):
    merged = deepcopy(pack_spec.get("defaults") or {})
    merged.update(deepcopy(asset_spec))
    return merged


def validate_pack_spec(spec: dict):
    pack = spec.get("pack")
    source = spec.get("source")
    export = spec.get("export")
    validation = spec.get("validation")
    assets = spec.get("assets")

    if not isinstance(pack, dict) or not pack.get("id"):
        raise SpecError("pack.id is required.")
    if not isinstance(source, dict) or not source.get("blend_path") or not source.get("collection"):
        raise SpecError("source.blend_path and source.collection are required.")
    if not isinstance(export, dict) or not export.get("output_directory"):
        raise SpecError("export.output_directory is required.")
    if str(export.get("format", "")).lower() != "fbx":
        raise SpecError("The Life Engine runtime asset pipeline currently requires export.format: fbx.")
    if not isinstance(validation, dict):
        raise SpecError("validation configuration is required.")
    for key in ("json_report", "markdown_report", "unity_manifest", "unity_report"):
        if not validation.get(key):
            raise SpecError(f"validation.{key} is required.")

    if not isinstance(assets, list) or not assets:
        raise SpecError("assets must contain at least one asset.")

    ids = []
    for raw_asset in assets:
        if not isinstance(raw_asset, dict) or not raw_asset.get("id"):
            raise SpecError("Every asset requires an id.")
        asset = merge_asset_defaults(spec, raw_asset)
        asset_id = asset["id"]
        if asset_id in ids:
            raise SpecError(f"Duplicate asset id: {asset_id}")
        ids.append(asset_id)

        dims = asset.get("dimensions_m")
        if not isinstance(dims, (list, tuple)) or len(dims) != 3:
            raise SpecError(f"{asset_id}: dimensions_m must contain exactly three values.")
        if any(float(value) <= 0 for value in dims):
            raise SpecError(f"{asset_id}: all dimensions must be positive.")

        preferred = int(asset.get("preferred_max_triangles", 0))
        hard = int(asset.get("hard_max_triangles", 0))
        if preferred <= 0 or hard <= 0 or preferred > hard:
            raise SpecError(
                f"{asset_id}: triangle budgets must be positive and preferred_max_triangles "
                "must not exceed hard_max_triangles."
            )

        if asset.get("generation_mode") == "generate" and asset.get("seed") is None:
            raise SpecError(f"{asset_id}: generated assets require a deterministic seed.")

    modes = {
        asset.get("generation_mode", spec.get("source_strategy", {}).get("default"))
        for asset in assets
    }
    if "generate" in modes:
        generator = spec.get("generator")
        if not isinstance(generator, dict) or not generator.get("module") or not generator.get("entrypoint"):
            raise SpecError("Generated packs require generator.module and generator.entrypoint.")

    for relative_path in iter_repo_relative_paths(spec):
        if os.path.isabs(str(relative_path)):
            raise SpecError(f"Pipeline paths must be repository-relative: {relative_path}")

    return spec


def iter_repo_relative_paths(spec: dict):
    source = spec.get("source") or {}
    export = spec.get("export") or {}
    validation = spec.get("validation") or {}
    for value in (
        source.get("blend_path"),
        export.get("output_directory"),
        validation.get("json_report"),
        validation.get("markdown_report"),
        validation.get("unity_manifest"),
        validation.get("unity_report"),
    ):
        if value:
            yield value


def resolve_repo_path(repo_root: str, relative_path: str):
    repo_root_abs = os.path.abspath(repo_root)
    resolved = os.path.abspath(os.path.join(repo_root_abs, relative_path))
    try:
        common = os.path.commonpath((repo_root_abs, resolved))
    except ValueError as exc:
        raise SpecError(f"Invalid repository-relative path: {relative_path}") from exc
    if common != repo_root_abs:
        raise SpecError(f"Path escapes repository root: {relative_path}")
    return resolved
