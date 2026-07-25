#!/usr/bin/env python3
"""Create a macOS app ZIP on Windows while preserving Unix permissions."""

from __future__ import annotations

import argparse
import hashlib
import os
import plistlib
import shutil
import stat
import struct
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path


CPU_NAMES = {
    0x01000007: "x64",
    0x0100000C: "arm64",
}

MACOS_ARM64_XRAY_SHA256 = (
    "672590b1c35b1d8cd7ba3e786ab53189001f32e46369e18c0d81d099a4d66c52"
)

README = """v2rayN macOS installation

1. Extract this ZIP with Finder.
2. Drag v2rayN.app into the Applications folder.
3. On first launch, Control-click v2rayN.app, choose Open, then confirm Open.
4. If macOS still blocks the unsigned local build, run:

   xattr -cr "/Applications/v2rayN.app"

The xattr command only works after v2rayN.app has been copied to Applications.
This package preserves executable permissions for v2rayN and all bundled cores.
"""


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("app", type=Path, help="Path to v2rayN.app")
    parser.add_argument("output", type=Path, help="Output ZIP path")
    parser.add_argument("--arch", choices=("arm64", "x64"), required=True)
    parser.add_argument(
        "--rcodesign",
        type=Path,
        help="Optional rcodesign executable used to ad-hoc sign the bundle first",
    )
    return parser.parse_args()


def macho_arch(path: Path) -> str | None:
    with path.open("rb") as stream:
        header = stream.read(8)

    if len(header) < 8:
        return None
    if header[:4] in (b"\xcf\xfa\xed\xfe", b"\xce\xfa\xed\xfe"):
        cpu_type = struct.unpack("<I", header[4:8])[0]
        return CPU_NAMES.get(cpu_type)
    if header[:4] in (b"\xfe\xed\xfa\xcf", b"\xfe\xed\xfa\xce"):
        cpu_type = struct.unpack(">I", header[4:8])[0]
        return CPU_NAMES.get(cpu_type)
    return None


def validate_app(
    app: Path,
    expected_arch: str,
    require_bundle_signature: bool = True,
) -> list[Path]:
    if not app.is_dir() or app.suffix != ".app":
        raise ValueError(f"Not a macOS app bundle: {app}")

    plist_path = app / "Contents" / "Info.plist"
    if not plist_path.is_file():
        raise ValueError(f"Missing Info.plist: {plist_path}")

    with plist_path.open("rb") as stream:
        plist = plistlib.load(stream)
    executable_name = plist.get("CFBundleExecutable")
    if not executable_name:
        raise ValueError("CFBundleExecutable is missing from Info.plist")

    main_executable = app / "Contents" / "MacOS" / executable_name
    if not main_executable.is_file():
        raise ValueError(f"Missing bundle executable: {main_executable}")

    required_runtime_files = (
        app / "Contents" / "MacOS" / "bin" / "Country.mmdb",
        app / "Contents" / "MacOS" / "bin" / "geoip.dat",
        app / "Contents" / "MacOS" / "bin" / "geoip.metadb",
        app / "Contents" / "MacOS" / "bin" / "geoip-only-cn-private.dat",
        app / "Contents" / "MacOS" / "bin" / "geosite.dat",
        app / "Contents" / "MacOS" / "bin" / "mihomo" / "mihomo",
        app / "Contents" / "MacOS" / "bin" / "sing_box" / "sing-box",
        app / "Contents" / "MacOS" / "bin" / "xray" / "xray",
        app / "Contents" / "MacOS" / "bin" / "cfst" / "cfst",
        app / "Contents" / "MacOS" / "bin" / "cfst" / "ip.txt",
        app / "Contents" / "MacOS" / "bin" / "srss" / "geoip-cn.srs",
        app / "Contents" / "MacOS" / "bin" / "srss" / "geosite-cn.srs",
        app / "Contents" / "MacOS" / "bin" / "srss" / "geosite-gfw.srs",
        app / "Contents" / "MacOS" / "bin" / "srss" / "geosite-google.srs",
        app
        / "Contents"
        / "MacOS"
        / "guiConfigs"
        / "default-workspace-background.gif",
    )
    for required_file in required_runtime_files:
        if not required_file.is_file():
            raise ValueError(f"Missing required runtime file: {required_file}")

    if expected_arch == "arm64":
        # Xray v26.7.11 added Darwin process lookup required by Xray TUN routing.
        xray_path = app / "Contents" / "MacOS" / "bin" / "xray" / "xray"
        xray_sha256 = hashlib.sha256(xray_path.read_bytes()).hexdigest()
        if xray_sha256 != MACOS_ARM64_XRAY_SHA256:
            raise ValueError(
                "Unsupported macOS ARM64 Xray binary. "
                f"Expected v26.7.11 SHA256 {MACOS_ARM64_XRAY_SHA256}, "
                f"got {xray_sha256}. Replace bin/xray/xray before packaging."
            )

    code_resources = app / "Contents" / "_CodeSignature" / "CodeResources"
    if require_bundle_signature and not code_resources.is_file():
        raise ValueError(
            "Bundle is not signed. Pass --rcodesign PATH to create an ad-hoc signature."
        )

    machos: list[Path] = []
    for path in app.rglob("*"):
        if not path.is_file():
            continue
        arch = macho_arch(path)
        if arch is None:
            continue
        if arch != expected_arch:
            raise ValueError(
                f"Architecture mismatch: {path} is {arch}, expected {expected_arch}"
            )
        machos.append(path)

    if main_executable not in machos:
        raise ValueError(f"Bundle executable is not a supported Mach-O file: {main_executable}")
    return machos


def sign_bundle(app: Path, rcodesign: Path, expected_arch: str, root: Path) -> Path:
    rcodesign = rcodesign.resolve()
    if not rcodesign.is_file():
        raise ValueError(f"rcodesign executable not found: {rcodesign}")

    validate_app(app, expected_arch, require_bundle_signature=False)
    signed_app = root / app.name
    shutil.copytree(app, signed_app)
    result = subprocess.run(
        [str(rcodesign), "sign", str(signed_app)],
        check=False,
    )
    if result.returncode != 0:
        raise ValueError(f"rcodesign failed with exit code {result.returncode}")
    return signed_app


def zip_info(path: Path, archive_name: str, mode: int) -> zipfile.ZipInfo:
    info = zipfile.ZipInfo.from_file(path, archive_name)
    info.create_system = 3
    info.external_attr = mode << 16
    return info


def add_directory(archive: zipfile.ZipFile, path: Path, archive_name: str) -> None:
    name = archive_name.rstrip("/") + "/"
    archive.writestr(zip_info(path, name, stat.S_IFDIR | 0o755), b"")


def add_file(
    archive: zipfile.ZipFile,
    path: Path,
    archive_name: str,
    executable: bool,
) -> None:
    mode = stat.S_IFREG | (0o755 if executable else 0o644)
    info = zip_info(path, archive_name, mode)
    info.compress_type = zipfile.ZIP_DEFLATED
    with path.open("rb") as stream:
        archive.writestr(info, stream.read(), compress_type=zipfile.ZIP_DEFLATED)


def create_zip(app: Path, output: Path, machos: list[Path]) -> None:
    executable_paths = {path.resolve() for path in machos}
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_suffix(output.suffix + ".tmp")
    temporary.unlink(missing_ok=True)

    try:
        with zipfile.ZipFile(
            temporary,
            "w",
            compression=zipfile.ZIP_DEFLATED,
            compresslevel=9,
            allowZip64=True,
        ) as archive:
            add_directory(archive, app, app.name)
            for root, directory_names, file_names in os.walk(app):
                root_path = Path(root)
                relative_root = root_path.relative_to(app.parent)
                directory_names.sort()
                file_names.sort()

                for directory_name in directory_names:
                    directory = root_path / directory_name
                    archive_name = (relative_root / directory_name).as_posix()
                    add_directory(archive, directory, archive_name)

                for file_name in file_names:
                    file_path = root_path / file_name
                    archive_name = (relative_root / file_name).as_posix()
                    add_file(
                        archive,
                        file_path,
                        archive_name,
                        file_path.resolve() in executable_paths,
                    )

            readme_info = zipfile.ZipInfo("README-macOS.txt")
            readme_info.create_system = 3
            readme_info.external_attr = (stat.S_IFREG | 0o644) << 16
            readme_info.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(readme_info, README.encode("utf-8"))

        temporary.replace(output)
    finally:
        temporary.unlink(missing_ok=True)


def main() -> int:
    args = parse_args()
    app = args.app.resolve()
    output = args.output.resolve()
    try:
        with tempfile.TemporaryDirectory(prefix="v2rayn-macos-sign-") as temporary:
            if args.rcodesign:
                app = sign_bundle(app, args.rcodesign, args.arch, Path(temporary))
            machos = validate_app(app, args.arch)
            create_zip(app, output, machos)
    except (OSError, ValueError, plistlib.InvalidFileException) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1

    print(f"Created: {output}")
    print(f"Validated {len(machos)} {args.arch} Mach-O executable(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
