#!/usr/bin/env python3
"""Собрать плагин MCP для Topomatic Robur в установочный пакет .tpm.

Запуск из корня репозитория:

    python scripts/build_tpm.py

Зависимости сборки перечислены в scripts/requirements.txt.
"""

from __future__ import annotations

import argparse
from contextlib import contextmanager
import json
import os
import re
import shutil
import subprocess
import sys
import time
import zipfile
from dataclasses import dataclass
from pathlib import Path
from xml.etree import ElementTree


ROOT = Path(__file__).resolve().parent.parent
DEFAULT_OUTPUT_DIR = ROOT / "build"
DEFAULT_PACKAGE_NAME = "robur_mcp"
DEFAULT_VERSION = "0.1"
DEFAULT_AUTHOR = "Topomatic"
SERVER_EXECUTABLE_NAME = "robur_mcp_server.exe"
BRIDGE_ASSEMBLY_NAME = "Topomatic.ToolBridge.dll"
VERSION_PATTERN = re.compile(r"\d+\.\d+")
ASSEMBLY_VERSION_PATTERNS = (
    re.compile(r'(\[assembly:\s*AssemblyVersion\(")[^"]+("\)\])'),
    re.compile(r'(\[assembly:\s*AssemblyFileVersion\(")[^"]+("\)\])'),
)
REQUIRED_ARCHIVE_FILES = frozenset(
    {
        "package.json",
        "plugins/tool_bridge.plugin",
        f"bin/{BRIDGE_ASSEMBLY_NAME}",
        f"bin/mcp_server/{SERVER_EXECUTABLE_NAME}",
    }
)
REQUIRED_ARCHIVE_DIRECTORIES = frozenset(
    {
        "bin/",
        "bin/mcp_server/",
        "plugins/",
    }
)
YELLOW = "\033[93m"
RED = "\033[91m"
RESET = "\033[0m"


class BuildError(RuntimeError):
    """Ошибка, которую следует показать пользователю как ошибку сборки."""


def required_archive_files(external_assemblies: tuple[str, ...]) -> frozenset[str]:
    """Вернуть обязательные файлы TPM с учётом внешних зависимостей моста."""
    return REQUIRED_ARCHIVE_FILES | frozenset(
        f"bin/{assembly_name}" for assembly_name in external_assemblies
    )


@dataclass(frozen=True)
class BuildToolchain:
    """Проверенные внешние инструменты, необходимые для TPM-сборки."""

    msbuild_command: tuple[str, ...]


@dataclass(frozen=True)
class ProjectFiles:
    """Пути к исходным файлам, необходимым для сборки пакета."""

    root: Path
    project_file: Path
    plugin_file: Path
    mcp_entry_point: Path

    @classmethod
    def from_root(cls, root: Path) -> "ProjectFiles":
        return cls(
            root=root,
            project_file=root / "tool_bridge" / "Topomatic.ToolBridge.csproj",
            plugin_file=root / "tool_bridge" / "tool_bridge.plugin",
            mcp_entry_point=root / "mcp_server" / "main.py",
        )

    def validate(self) -> None:
        missing_files = [
            path
            for path in (self.project_file, self.plugin_file, self.mcp_entry_point)
            if not path.is_file()
        ]
        if missing_files:
            formatted_paths = ", ".join(str(path) for path in missing_files)
            raise BuildError(f"Не найдены необходимые файлы: {formatted_paths}")

    def get_copy_local_reference_assemblies(self) -> tuple[str, ...]:
        """Получить из csproj имена внешних DLL, копируемых рядом с плагином.

        В TPM добавляются только ссылки с ``HintPath`` без ``Private=False``.
        Это соответствует семантике Copy Local в MSBuild и не включает DLL Robur,
        которые предоставляет сама установленная платформа.
        """
        try:
            project = ElementTree.parse(self.project_file)
        except (OSError, ElementTree.ParseError) as error:
            raise BuildError(f"Не удалось прочитать проект C#: {self.project_file}") from error

        assemblies_by_name = {}
        for reference in project.getroot().iter():
            if reference.tag.rsplit("}", 1)[-1] != "Reference":
                continue

            hint_path = None
            copy_local = True
            for child in reference:
                tag_name = child.tag.rsplit("}", 1)[-1]
                if tag_name == "HintPath":
                    hint_path = (child.text or "").strip()
                elif tag_name == "Private" and (child.text or "").strip().casefold() == "false":
                    copy_local = False

            if not hint_path or not copy_local:
                continue

            assembly_name = Path(hint_path).name
            if not assembly_name.lower().endswith(".dll"):
                raise BuildError(
                    "HintPath внешней ссылки должен указывать на DLL: "
                    f"{hint_path} в {self.project_file}"
                )
            normalized_name = assembly_name.casefold()
            if normalized_name == BRIDGE_ASSEMBLY_NAME.casefold():
                raise BuildError(
                    "Внешняя ссылка не может иметь имя основной сборки плагина: "
                    f"{assembly_name}"
                )
            existing_name = assemblies_by_name.get(normalized_name)
            if existing_name and existing_name != assembly_name:
                raise BuildError(
                    "Внешние ссылки содержат конфликтующие имена DLL: "
                    f"{existing_name} и {assembly_name}"
                )
            assemblies_by_name[normalized_name] = assembly_name

        return tuple(sorted(assemblies_by_name.values(), key=str.casefold))


@dataclass(frozen=True)
class BuildPaths:
    """Каталоги временной сборки и итоговый путь пакета."""

    output_dir: Path
    work_dir: Path
    staging_dir: Path
    package_path: Path
    temporary_package_path: Path

    @classmethod
    def create(cls, output_dir: Path, name: str, version: str) -> "BuildPaths":
        file_version = version.replace(".", "_")
        package_path = output_dir / f"{name}-{file_version}.tpm"
        return cls(
            output_dir=output_dir,
            work_dir=output_dir / ".tpm-work",
            staging_dir=output_dir / ".tpm-work" / "package",
            package_path=package_path,
            temporary_package_path=output_dir / f".{name}-{file_version}.tpm.tmp",
        )


def configure_console_encoding() -> None:
    """В Git Bash включить UTF-8 для сообщений самого сборочного скрипта."""
    if not os.environ.get("MSYSTEM"):
        return
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="backslashreplace")


def print_colored(message: str, color: str, *, error: bool = False) -> None:
    """Вывести важное сообщение с цветом, если поток подключён к терминалу."""
    stream = sys.stderr if error else sys.stdout
    colors_enabled = (
        not os.environ.get("NO_COLOR")
        and (
            bool(os.environ.get("FORCE_COLOR"))
            or bool(os.environ.get("MSYSTEM"))
            or stream.isatty()
        )
    )
    if colors_enabled:
        print(f"{color}{message}{RESET}", file=stream)
    else:
        print(message, file=stream)


def print_build_status(message: str) -> None:
    """Вывести акцентное сообщение о состоянии сборки."""
    print_colored(message, YELLOW)


def print_build_error(message: str) -> None:
    """Вывести сообщение об ошибке сборки."""
    print_colored(message, RED, error=True)


def run_command(title: str, command: list[str], *, cwd: Path = ROOT) -> None:
    """Выполнить этап сборки и добавить его название к диагностике."""
    print(f"[{title}]")
    print("+", subprocess.list2cmdline(command))
    try:
        subprocess.run(command, cwd=cwd, check=True)
    except FileNotFoundError as error:
        raise BuildError(f"Не найдена программа для этапа «{title}»: {command[0]}") from error
    except subprocess.CalledProcessError as error:
        raise BuildError(f"Этап «{title}» завершился с кодом {error.returncode}.") from error


def find_msbuild() -> list[str]:
    """Найти MSBuild, отдавая приоритет переменной MSBUILD_PATH."""
    configured_path = os.environ.get("MSBUILD_PATH")
    if configured_path:
        msbuild_path = Path(configured_path)
        if not msbuild_path.is_file():
            raise BuildError(f"MSBUILD_PATH указывает не на файл: {msbuild_path}")
        return [str(msbuild_path)]

    msbuild = shutil.which("msbuild")
    if msbuild:
        return [msbuild]

    dotnet = shutil.which("dotnet")
    if dotnet:
        return [dotnet, "msbuild"]

    raise BuildError(
        "MSBuild не найден. Установите Visual Studio Build Tools или задайте "
        "в MSBUILD_PATH полный путь к MSBuild.exe."
    )


def ensure_pyinstaller_available() -> None:
    """Проверить наличие PyInstaller в текущем интерпретаторе Python."""
    try:
        __import__("PyInstaller")
    except ImportError as error:
        raise BuildError(
            "PyInstaller не установлен для текущего интерпретатора Python. "
            "Установите зависимости командой: "
            f"{sys.executable} -m pip install -r scripts/requirements.txt"
        ) from error


def resolve_build_toolchain() -> BuildToolchain:
    """Проверить все внешние инструменты до изменения каталога результата."""
    ensure_pyinstaller_available()
    return BuildToolchain(msbuild_command=tuple(find_msbuild()))


def make_assembly_version(package_version: str) -> str:
    """Преобразовать версию TPM вида 0.1 в четырёхчастную версию DLL."""
    major, minor = (int(part) for part in package_version.split("."))
    if major > 65534 or minor > 65534:
        raise BuildError("Компоненты версии DLL должны быть не больше 65534.")
    return f"{major}.{minor}.0.0"


@contextmanager
def temporary_assembly_version(files: ProjectFiles, package_version: str):
    """Временно записать версию DLL в AssemblyInfo.cs и восстановить файл после сборки."""
    assembly_info = files.root / "tool_bridge" / "Properties" / "AssemblyInfo.cs"
    if not assembly_info.is_file():
        raise BuildError(f"Не найден файл с версией DLL: {assembly_info}")

    assembly_version = make_assembly_version(package_version)
    original_bytes = assembly_info.read_bytes()
    has_utf8_bom = original_bytes.startswith(b"\xef\xbb\xbf")
    content = original_bytes.decode("utf-8-sig")
    for pattern in ASSEMBLY_VERSION_PATTERNS:
        content, substitutions = pattern.subn(
            rf"\g<1>{assembly_version}\g<2>", content, count=1
        )
        if substitutions != 1:
            raise BuildError(
                f"Не удалось обновить версию DLL в файле: {assembly_info}"
            )
    modified_bytes = content.encode("utf-8")
    if has_utf8_bom:
        modified_bytes = b"\xef\xbb\xbf" + modified_bytes

    assembly_info.write_bytes(modified_bytes)
    try:
        yield assembly_version
    finally:
        assembly_info.write_bytes(original_bytes)


def build_tool_bridge(
    files: ProjectFiles, package_version: str, toolchain: BuildToolchain
) -> Path:
    """Собрать C#-мост и вернуть каталог Release с DLL."""
    with temporary_assembly_version(files, package_version) as assembly_version:
        print(f"Версия DLL: {assembly_version}")
        run_command(
            "Сборка C#-моста",
            [
                *toolchain.msbuild_command,
                str(files.project_file),
                "/target:Build",
                "/property:Configuration=Release",
                "/verbosity:minimal",
            ],
            cwd=files.root,
        )
    output_dir = files.root / "tool_bridge" / "bin" / "Release"
    assembly = output_dir / BRIDGE_ASSEMBLY_NAME
    if not assembly.is_file():
        raise BuildError(f"Сборка C# завершилась, но не создала файл: {assembly}")
    return output_dir


def build_mcp_server(files: ProjectFiles, paths: BuildPaths) -> None:
    """Собрать MCP-сервер PyInstaller в папку bin/mcp_server."""
    pyinstaller_dir = paths.work_dir / "pyinstaller"
    staging_bin = paths.staging_dir / "bin"
    run_command(
        "Сборка MCP-сервера",
        [
            sys.executable,
            "-m",
            "PyInstaller",
            "--noconfirm",
            "--clean",
            "--log-level",
            "WARN",
            "--onedir",
            "--name",
            SERVER_EXECUTABLE_NAME.removesuffix(".exe"),
            "--distpath",
            str(staging_bin),
            "--workpath",
            str(pyinstaller_dir / "work"),
            "--specpath",
            str(pyinstaller_dir / "spec"),
            str(files.mcp_entry_point),
        ],
        cwd=files.root,
    )

    generated_dir = staging_bin / SERVER_EXECUTABLE_NAME.removesuffix(".exe")
    executable = generated_dir / SERVER_EXECUTABLE_NAME
    if not executable.is_file():
        raise BuildError(f"PyInstaller не создал файл: {executable}")

    destination = staging_bin / "mcp_server"
    generated_dir.replace(destination)


def copy_file(source: Path, destination_dir: Path) -> None:
    """Скопировать обязательный файл с понятной диагностикой при его отсутствии."""
    if not source.is_file():
        raise BuildError(f"Не найден файл для упаковки: {source}")
    destination_dir.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination_dir / source.name)


def stage_package_files(
    files: ProjectFiles,
    bridge_output: Path,
    paths: BuildPaths,
    external_assemblies: tuple[str, ...],
) -> None:
    """Разместить DLL, файл плагина и сервер в структуре будущего TPM-пакета."""
    bin_dir = paths.staging_dir / "bin"
    plugins_dir = paths.staging_dir / "plugins"
    copy_file(bridge_output / BRIDGE_ASSEMBLY_NAME, bin_dir)
    copy_file(files.plugin_file, plugins_dir)
    for assembly_name in external_assemblies:
        copy_file(bridge_output / assembly_name, bin_dir)


def write_package_manifest(paths: BuildPaths, args: argparse.Namespace) -> None:
    """Создать обязательный для TPM файл package.json в кодировке UTF-8."""
    manifest = {
        "name": args.name,
        "version": args.version,
        "caption": args.caption,
        "description": args.description,
        "author": args.author,
    }
    manifest_path = paths.staging_dir / "package.json"
    manifest_path.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


def create_and_verify_package(
    paths: BuildPaths, external_assemblies: tuple[str, ...]
) -> None:
    """Создать временный архив, проверить его и только затем заменить итоговый пакет."""
    paths.output_dir.mkdir(parents=True, exist_ok=True)
    paths.temporary_package_path.unlink(missing_ok=True)
    try:
        with zipfile.ZipFile(
            paths.temporary_package_path, "w", compression=zipfile.ZIP_DEFLATED
        ) as archive:
            # Явные записи каталогов нужны для совместимости с пакетными
            # менеджерами, которые определяют структуру TPM по entries архива.
            directories = sorted(
                (path for path in paths.staging_dir.rglob("*") if path.is_dir()),
                key=lambda path: path.relative_to(paths.staging_dir).as_posix(),
            )
            for directory in directories:
                archive.writestr(
                    f"{directory.relative_to(paths.staging_dir).as_posix()}/", b""
                )
            for file_path in sorted(paths.staging_dir.rglob("*")):
                if file_path.is_file():
                    archive.write(file_path, file_path.relative_to(paths.staging_dir).as_posix())

        with zipfile.ZipFile(paths.temporary_package_path) as archive:
            bad_file = archive.testzip()
            if bad_file:
                raise BuildError(f"Проверка архива не пройдена: повреждён файл {bad_file}")
            missing_files = required_archive_files(external_assemblies).difference(
                archive.namelist()
            )
            if missing_files:
                raise BuildError(
                    "В созданном TPM-пакете отсутствуют файлы: "
                    + ", ".join(sorted(missing_files))
                )
            missing_directories = REQUIRED_ARCHIVE_DIRECTORIES.difference(archive.namelist())
            if missing_directories:
                raise BuildError(
                    "В созданном TPM-пакете отсутствуют каталоги: "
                    + ", ".join(sorted(missing_directories))
                )
            try:
                json.loads(archive.read("package.json").decode("utf-8"))
            except (UnicodeDecodeError, json.JSONDecodeError) as error:
                raise BuildError("В архиве содержится некорректный package.json.") from error

        # replace выполняется только после успешной проверки нового архива.
        paths.temporary_package_path.replace(paths.package_path)
    finally:
        paths.temporary_package_path.unlink(missing_ok=True)


def prepare_output_directory(output_dir: Path) -> None:
    """Очистить build или его вложенную папку перед началом новой сборки."""
    try:
        output_dir.relative_to(DEFAULT_OUTPUT_DIR)
    except ValueError as error:
        raise BuildError(
            "--output-dir должен указывать на папку build или её вложенную папку."
        ) from error

    if output_dir.exists() and not output_dir.is_dir():
        raise BuildError(f"Путь каталога результата занят файлом: {output_dir}")

    if not output_dir.exists():
        output_dir.mkdir(parents=True)
        return

    print(f"Очистка каталога результата: {output_dir}")
    for entry in output_dir.iterdir():
        try:
            if entry.is_symlink() or entry.is_file():
                entry.unlink()
            elif entry.is_dir():
                shutil.rmtree(entry)
        except OSError as error:
            raise BuildError(f"Не удалось удалить {entry}: {error}") from error


def parse_args() -> argparse.Namespace:
    """Разобрать параметры запуска сборочного скрипта."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--name", default=DEFAULT_PACKAGE_NAME, help="Уникальное имя пакета")
    parser.add_argument(
        "--version",
        default=DEFAULT_VERSION,
        help="Версия пакета в формате число.число",
    )
    parser.add_argument("--caption", default="Robur MCP", help="Отображаемое имя")
    parser.add_argument(
        "--description",
        default="MCP server and tool bridge for Topomatic Robur",
        help="Описание пакета",
    )
    parser.add_argument("--author", default=DEFAULT_AUTHOR, help="Автор пакета")
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=DEFAULT_OUTPUT_DIR,
        help="Папка для готового TPM-пакета (по умолчанию: build)",
    )
    parser.add_argument(
        "--keep-work-dir",
        action="store_true",
        help="Не удалять временные файлы после сборки; полезно для диагностики",
    )
    return parser.parse_args()


def validate_arguments(args: argparse.Namespace) -> None:
    """Проверить ограничения формата имени и версии TPM-пакета."""
    sanitized_name = args.name.replace("_", "").replace("-", "")
    if not args.name.isascii() or not sanitized_name.isalnum():
        raise BuildError("--name может содержать только английские буквы, цифры, '-' и '_'.")
    if not VERSION_PATTERN.fullmatch(args.version):
        raise BuildError("--version должна иметь формат число.число, например 0.1.")
    make_assembly_version(args.version)


def main() -> int:
    """Выполнить все этапы сборки и вернуть код её завершения."""
    started_at = time.perf_counter()
    args = parse_args()
    validate_arguments(args)

    files = ProjectFiles.from_root(ROOT)
    files.validate()
    external_assemblies = files.get_copy_local_reference_assemblies()
    paths = BuildPaths.create(args.output_dir.resolve(), args.name, args.version)

    print_build_status("=== Начало сборки TPM-пакета ===")
    print(f"Пакет: {args.name}, версия: {args.version}")
    print(f"Каталог результата: {paths.output_dir}")
    print(
        "Внешние DLL: "
        + (", ".join(external_assemblies) if external_assemblies else "отсутствуют")
    )
    toolchain = resolve_build_toolchain()
    prepare_output_directory(paths.output_dir)

    try:
        bridge_output = build_tool_bridge(files, args.version, toolchain)
        stage_package_files(files, bridge_output, paths, external_assemblies)
        build_mcp_server(files, paths)
        write_package_manifest(paths, args)
        create_and_verify_package(paths, external_assemblies)
    finally:
        if not args.keep_work_dir:
            shutil.rmtree(paths.work_dir, ignore_errors=True)

    elapsed_seconds = time.perf_counter() - started_at
    size_megabytes = paths.package_path.stat().st_size / (1024 * 1024)
    print_build_status("=== Сборка TPM-пакета успешно завершена ===")
    print(f"Файл пакета: {paths.package_path}")
    print(f"Размер: {size_megabytes:.2f} МБ")
    print(f"Время сборки: {elapsed_seconds:.1f} с")
    return 0


if __name__ == "__main__":
    configure_console_encoding()
    try:
        raise SystemExit(main())
    except BuildError as error:
        print_build_error("=== Ошибка сборки TPM-пакета ===")
        print_build_error(f"Причина: {error}")
        raise SystemExit(1)
    except KeyboardInterrupt:
        print_build_error("\n=== Сборка отменена пользователем ===")
        raise SystemExit(130)
    except (OSError, zipfile.BadZipFile) as error:
        print_build_error("=== Ошибка сборки TPM-пакета ===")
        print_build_error(f"Причина: {error}")
        raise SystemExit(1)
