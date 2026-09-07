#!/usr/bin/env python3
"""Создать tool_bridge/references из DLL Robur с помощью Refasmer.

Запуск: python scripts/generate_references.py
Refasmer: dotnet tool install --global JetBrains.Refasmer.CliTool
"""

import os
from pathlib import Path, PureWindowsPath
import shutil
import subprocess
import sys
from xml.etree import ElementTree


ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIRECTORY = ROOT / "Out" / "Bin"
PROJECT_DIRECTORY = ROOT / "tool_bridge"
PROJECT_FILE = PROJECT_DIRECTORY / "Topomatic.ToolBridge.csproj"
OUTPUT_DIRECTORY = PROJECT_DIRECTORY / "references"


def configure_console_encoding() -> None:
    """В Git Bash включить UTF-8 для сообщений самого скрипта."""
    if not os.environ.get("MSYSTEM"):
        return
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="backslashreplace")


def generate() -> None:
    source_directory = SOURCE_DIRECTORY.resolve()
    print(f"Каталог исходных DLL: {source_directory}", flush=True)
    print(f"Чтение проекта: {PROJECT_FILE}", flush=True)
    project = ElementTree.parse(PROJECT_FILE)
    names = set()
    for hint in project.findall(".//{*}Reference/{*}HintPath"):
        text = (hint.text or "").strip().replace("/", "\\")
        if text.startswith("$(RoburReferencesPath)\\"):
            names.add(PureWindowsPath(text).name)
    if not names:
        raise RuntimeError("В проекте не найдены ссылки через RoburReferencesPath.")

    sources = [source_directory / name for name in sorted(names, key=str.casefold)]
    print(f"Проверка исходных DLL: {len(sources)}", flush=True)
    for source in sources:
        print(f"  Проверка: {source}", flush=True)
        if not source.is_file():
            raise FileNotFoundError(f"Не найдена исходная DLL: {source}")

    print("Поиск Refasmer в PATH", flush=True)
    refasmer = shutil.which("refasmer")
    if not refasmer:
        raise RuntimeError(
            "Установите Refasmer: dotnet tool install --global JetBrains.Refasmer.CliTool"
        )

    # Не допускаем удаления исходных DLL или каталога вне tool_bridge/references.
    output = OUTPUT_DIRECTORY.resolve()
    if output != PROJECT_DIRECTORY.resolve() / "references":
        raise RuntimeError("Каталог references перенаправлен в другое место.")
    if source_directory == output or output in source_directory.parents:
        raise RuntimeError("Исходные DLL не должны находиться внутри references.")
    if OUTPUT_DIRECTORY.exists():
        print(f"Очистка каталога: {OUTPUT_DIRECTORY}", flush=True)
        shutil.rmtree(OUTPUT_DIRECTORY)
    print(f"Создание каталога: {OUTPUT_DIRECTORY}", flush=True)
    OUTPUT_DIRECTORY.mkdir(parents=True)

    command = [
        refasmer, "--refasm", "--public", "--omit-non-api-members", "true", "-v",
        "-O", str(OUTPUT_DIRECTORY), *map(str, sources),
    ]
    print(f"Запуск Refasmer: {subprocess.list2cmdline(command)}", flush=True)
    subprocess.run(command, check=True)
    print(f"Готово: сгенерировано {len(sources)} заглушек в {OUTPUT_DIRECTORY}", flush=True)


def main() -> int:
    try:
        generate()
    except (RuntimeError, OSError, ElementTree.ParseError, subprocess.CalledProcessError) as error:
        print(f"Ошибка: {error}", file=sys.stderr, flush=True)
        return 1
    return 0


if __name__ == "__main__":
    configure_console_encoding()
    raise SystemExit(main())
