"""Configure a fresh clone of One Valley on a machine that has never built it.

Everything here is a per-machine setting that git cannot carry for you. Run it once,
straight after cloning:

    python Tools/setup_new_machine.py

It is safe to run again; every step checks before it changes anything.
"""

import os
import subprocess
import sys

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Where Unity Hub puts editors, in the order worth trying.
UNITY_HUB_ROOTS = [
    r"C:\Program Files\Unity\Hub\Editor",
    r"C:\Program Files (x86)\Unity\Hub\Editor",
    r"D:\Program Files\Unity\Hub\Editor",
    os.path.expandvars(r"%LOCALAPPDATA%\Unity\Hub\Editor"),
]


def run(args, **kwargs):
    """Run a command and hand back (ok, combined output)."""
    finished = subprocess.run(
        args, capture_output=True, text=True, cwd=PROJECT_ROOT, **kwargs
    )
    output = (finished.stdout or "") + (finished.stderr or "")
    return finished.returncode == 0, output.strip()


def required_unity_version():
    """The exact editor this project is serialised against."""
    path = os.path.join(PROJECT_ROOT, "ProjectSettings", "ProjectVersion.txt")
    with open(path, "r", encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("m_EditorVersion:"):
                return line.split(":", 1)[1].strip()
    return None


def step_git_lfs():
    """Without this, every binary clones as a short text pointer stub."""
    ok, _ = run(["git", "lfs", "version"])
    if not ok:
        return False, "git-lfs is not installed. Install it from https://git-lfs.com first."

    run(["git", "lfs", "install"])

    # A pointer stub is a couple of hundred bytes of text starting with a version line.
    sample = os.path.join(PROJECT_ROOT, "Assets", "Resources", "Models", "Sword.fbx")
    if os.path.isfile(sample):
        with open(sample, "rb") as handle:
            head = handle.read(64)
        if head.startswith(b"version https://git-lfs"):
            ok, output = run(["git", "lfs", "pull"])
            if not ok:
                return False, "LFS files are pointer stubs and 'git lfs pull' failed: " + output[:120]
            return True, "git-lfs installed; pulled real binaries over the pointer stubs"
    return True, "git-lfs installed and binaries are real files"


def find_unity_yaml_merge(version):
    """Locate UnityYAMLMerge.exe for the exact version, then for any version."""
    tail = os.path.join("Editor", "Data", "Tools", "UnityYAMLMerge.exe")

    for root in UNITY_HUB_ROOTS:
        candidate = os.path.join(root, version, tail)
        if os.path.isfile(candidate):
            return candidate, True

    for root in UNITY_HUB_ROOTS:
        if not os.path.isdir(root):
            continue
        for name in sorted(os.listdir(root), reverse=True):
            candidate = os.path.join(root, name, tail)
            if os.path.isfile(candidate):
                return candidate, False

    return None, False


def step_merge_driver():
    """.gitattributes asks for this driver, but the path lives in .git/config and never syncs.

    Without it git silently falls back to a plain text merge on .unity and .prefab files
    and mangles them.
    """
    version = required_unity_version()
    if version is None:
        return False, "could not read ProjectSettings/ProjectVersion.txt"

    exe, exact = find_unity_yaml_merge(version)
    if exe is None:
        return False, "Unity " + version + " not found. Install it through Unity Hub, then re-run."

    driver = '"' + exe.replace("\\", "/") + '" merge -p %O %B %A %A'
    ok, output = run(["git", "config", "merge.unityyamlmerge.driver", driver])
    if not ok:
        return False, "git config failed: " + output[:120]

    if not exact:
        return True, "registered, but using a DIFFERENT Unity build - install " + version
    return True, "registered against Unity " + version


def step_clear_read_only():
    """Older clones carry a read-only bit from the LFS 'lockable' attribute.

    That attribute has been removed, but a working tree checked out while it was still in
    place keeps the bit, and Blender's FBX export then dies with PermissionError.
    """
    if os.name != "nt":
        return True, "not Windows; nothing to clear"

    cleared = 0
    for folder in ("Assets", "Docs"):
        base = os.path.join(PROJECT_ROOT, folder)
        for current, _dirs, files in os.walk(base):
            for name in files:
                path = os.path.join(current, name)
                if not os.access(path, os.W_OK):
                    try:
                        os.chmod(path, 0o666)
                        cleared += 1
                    except OSError:
                        pass
    if cleared:
        return True, "cleared the read-only bit on " + str(cleared) + " file(s)"
    return True, "no read-only files found"


def step_blender():
    """Blender is optional - only needed to regenerate the art from Tools/build_*.py."""
    ok, output = run(["blender", "--version"])
    if not ok:
        return True, "Blender not on PATH (optional; only needed to rebuild the art)"
    first_line = output.split("\n")[0].strip()
    return True, first_line + " found"


def main():
    steps = [
        ("git-lfs", step_git_lfs),
        ("merge driver", step_merge_driver),
        ("read-only bits", step_clear_read_only),
        ("blender", step_blender),
    ]

    print("Setting up One Valley in " + PROJECT_ROOT)
    print()

    failed = False
    for label, step in steps:
        passed, detail = step()
        print(("  OK   " if passed else "  FAIL ") + label.ljust(16) + detail)
        if not passed:
            failed = True

    print()
    if failed:
        print("Fix the FAIL lines above, then run this again.")
        return 1

    print("Ready. Open the project in Unity, then:")
    print("    python Tools/remote_preflight.py")
    return 0


if __name__ == "__main__":
    sys.exit(main())
