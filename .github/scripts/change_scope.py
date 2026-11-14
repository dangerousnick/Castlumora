"""Conservative change-range classification shared by CI and the normal push gate."""
import argparse
from pathlib import Path, PurePosixPath
import subprocess
import re

ROOT_DOCS = {"README.md", "DEVELOPMENT_PROGRESS.md", "FEATURE_PARITY.md", "PRODUCT_ROADMAP.md", "INSTALL_TEST_BUILD.md"}


def documentation(path):
    p = PurePosixPath(path)
    return path in ROOT_DOCS or (p.parts[0] == "docs" and p.suffix == ".md")


def changed(base, head):
    if not re.fullmatch(r"[0-9a-fA-F]{40}|[0-9a-fA-F]{64}", base or ""):
        return None
    if head != "HEAD" and not re.fullmatch(r"[0-9a-fA-F]{40}|[0-9a-fA-F]{64}", head):
        return None
    if subprocess.run(["git", "merge-base", "--is-ancestor", base, head], capture_output=True).returncode:
        return None
    result = subprocess.run(["git", "diff", "--name-only", "-z", base, head], capture_output=True, check=True)
    return [p.decode("utf-8") for p in result.stdout.split(b"\0") if p]


def classify(base, head, target_base=""):
    if target_base and changed(target_base, head) is None:
        return "application"
    try:
        paths = changed(base, head)
        if not paths or not all(documentation(p) for p in paths):
            return "application"
        # A symlink masquerading as documentation is not a documentation-only edit.
        for p in paths:
            entry = subprocess.check_output(["git", "ls-tree", head, "--", p]).decode()
            if not entry.startswith("100644 blob "):
                return "application"
        return "docs"
    except (subprocess.SubprocessError, UnicodeError):
        return "application"


def check_docs(base, head, target_base=""):
    paths = changed(base, head)
    if classify(base, head, target_base) != "docs":
        raise ValueError("Not a verified documentation-only range")
    subprocess.run(["git", "diff", "--check", base, head], check=True)
    for p in paths:
        entry = subprocess.check_output(["git", "ls-tree", head, "--", p])
        if not entry:  # Deleted documentation has no contents to validate.
            continue
        text = subprocess.check_output(["git", "show", f"{head}:{p}"]).decode("utf-8")
        if "\0" in text or any(line.startswith(("<<<<<<< ", ">>>>>>> ")) for line in text.splitlines()):
            raise ValueError("Invalid documentation or unresolved conflict markers")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", default="")
    parser.add_argument("--head", default="HEAD")
    parser.add_argument("--target-base", default="")
    parser.add_argument("--check-docs", action="store_true")
    args = parser.parse_args()
    if args.check_docs:
        check_docs(args.base, args.head, args.target_base)
        print("PASS: documentation UTF-8, conflict-marker and whitespace validation")
    else:
        print(classify(args.base, args.head, args.target_base))
