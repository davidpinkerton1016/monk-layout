"""Prepare the standard Dalamud ZIP and public repository index."""

from __future__ import annotations

import argparse
import json
import re
import time
import zipfile
from pathlib import Path
from urllib.parse import urlsplit


def package(root: Path, output: Path, base_url: str | None, repo_url: str | None = None) -> dict:
    manifest = json.loads((root / "Plugin/MonkLayout.json").read_text(encoding="utf-8"))
    if manifest["InternalName"] != "MonkLayout" or manifest["DalamudApiLevel"] != 15:
        raise ValueError("The plugin manifest does not match this repository.")
    if not re.fullmatch(r"\d+\.\d+\.\d+\.\d+", manifest["AssemblyVersion"]):
        raise ValueError("AssemblyVersion must have four numeric components.")
    binary = (root / "Plugin/MonkLayout.dll").read_bytes()
    if not binary.startswith(b"MZ"):
        raise ValueError("The compiled plugin is missing or invalid.")
    output.mkdir(parents=True, exist_ok=True)
    if base_url:
        base_url = base_url.rstrip("/")
        parsed = urlsplit(base_url)
        if parsed.scheme != "https" and not (parsed.scheme == "http" and parsed.hostname in {"localhost", "127.0.0.1"}):
            raise ValueError("Use a public HTTPS base URL, or localhost for validation.")
        if not parsed.hostname or parsed.query or parsed.fragment:
            raise ValueError("The base URL must be a directory URL without a query or fragment.")
    if repo_url:
        manifest["RepoUrl"] = repo_url.rstrip("/")
    payload = json.dumps(manifest, indent=2).encode("utf-8") + b"\n"
    archive = output / "latest.zip"
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as zipped:
        for name, contents in [("MonkLayout.dll", binary), ("MonkLayout.json", payload)]:
            item = zipfile.ZipInfo(name, date_time=(2026, 10, 29, 0, 0, 0))
            item.compress_type = zipfile.ZIP_DEFLATED
            item.external_attr = 0o644 << 16
            zipped.writestr(item, contents)
    result = {"archive": str(archive), "version": manifest["AssemblyVersion"], "published": False}
    if base_url:
        entry = dict(manifest)
        entry.update(
            IsHide=False,
            IsTestingExclusive=False,
            LastUpdate=int(time.time()),
            DownloadLinkInstall=f"{base_url}/latest.zip?v={manifest['AssemblyVersion']}",
            DownloadLinkUpdate=f"{base_url}/latest.zip?v={manifest['AssemblyVersion']}",
            DownloadLinkTesting=f"{base_url}/latest.zip?v={manifest['AssemblyVersion']}",
        )
        index = output / "repo.json"
        index.write_text(json.dumps([entry], indent=2) + "\n", encoding="utf-8")
        result.update(index=str(index), repository_url=f"{base_url}/repo.json")
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--output", type=Path)
    parser.add_argument("--base-url", help="Public directory URL, without repo.json.")
    parser.add_argument("--repo-url", help="GitHub project URL shown in the plugin installer.")
    args = parser.parse_args()
    print(json.dumps(package(args.root, args.output or args.root, args.base_url, args.repo_url), indent=2))


if __name__ == "__main__":
    main()
