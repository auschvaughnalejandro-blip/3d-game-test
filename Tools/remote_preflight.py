"""Check that this machine is ready to be driven remotely, before you walk away from it.

Remote Control runs a Claude session ON THIS MACHINE and lets another device talk to
that session. So the things Claude needs are the LOCAL things: Blender's socket, the
Unity bridge, and a repo that is pushed. This checks all three and says GO or NO-GO.

    python Tools/remote_preflight.py
"""

import json
import os
import socket
import subprocess
import sys

BLENDER_HOST = "127.0.0.1"
BLENDER_PORT = 9876
UNITY_CONNECTIONS = os.path.expandvars(r"%USERPROFILE%\.unity\mcp\connections")
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def check_blender():
    """Blender must be running with a real window; the addon refuses to serve headless."""
    try:
        connection = socket.socket()
        connection.settimeout(10.0)
        connection.connect((BLENDER_HOST, BLENDER_PORT))
    except OSError as error:
        return False, "cannot reach 127.0.0.1:9876 - is Blender open with its real window? " + str(error)

    payload = {"type": "execute_code", "params": {"code": "import bpy\nprint(bpy.app.version_string)"}}
    connection.sendall(json.dumps(payload).encode("utf-8"))

    chunks = b""
    try:
        while True:
            chunk = connection.recv(65536)
            if not chunk:
                break
            chunks += chunk
            try:
                reply = json.loads(chunks.decode("utf-8"))
                break
            except json.JSONDecodeError:
                continue
        else:
            reply = json.loads(chunks.decode("utf-8"))
    except OSError as error:
        return False, "socket opened but never answered: " + str(error)
    finally:
        connection.close()

    if reply.get("status") != "success":
        return False, "addon answered with an error: " + json.dumps(reply)[:200]
    return True, "Blender is serving on 9876"


def process_is_alive(pid):
    """tasklist is the dependency-free way to ask Windows whether a PID still exists."""
    output = subprocess.run(
        ["tasklist", "/FI", "PID eq " + str(pid), "/NH"],
        capture_output=True, text=True,
    ).stdout
    return str(pid) in output


def check_unity():
    """The editor drops a descriptor naming the pipe, the project and its own PID."""
    if not os.path.isdir(UNITY_CONNECTIONS):
        return False, "no connections directory - the Unity editor has never published a bridge"

    descriptors = [f for f in os.listdir(UNITY_CONNECTIONS) if f.endswith(".json")]
    if not descriptors:
        return False, "no bridge descriptor - the Unity editor is not running"

    for name in descriptors:
        with open(os.path.join(UNITY_CONNECTIONS, name), "r", encoding="utf-8") as handle:
            descriptor = json.load(handle)

        editor_pid = descriptor.get("editor_pid")
        project_path = descriptor.get("project_path", "")

        # A stale descriptor from a closed editor looks identical to a live one, so
        # confirm the PID before trusting it.
        if not process_is_alive(editor_pid):
            continue
        if os.path.normcase(os.path.normpath(project_path)) != os.path.normcase(PROJECT_ROOT):
            return False, "a Unity editor is running but on a different project: " + project_path
        return True, "Unity bridge live (pid " + str(editor_pid) + ")"

    return False, "descriptors exist but every editor PID is dead - stale files, Unity is closed"


def check_repo():
    """Anything uncommitted is invisible from the other device."""
    dirty = subprocess.run(
        ["git", "-C", PROJECT_ROOT, "status", "--porcelain"],
        capture_output=True, text=True,
    ).stdout.strip()
    if dirty:
        return False, str(len(dirty.splitlines())) + " uncommitted file(s) - push before you leave"

    unpushed = subprocess.run(
        ["git", "-C", PROJECT_ROOT, "log", "@{u}..HEAD", "--oneline"],
        capture_output=True, text=True,
    ).stdout.strip()
    if unpushed:
        return False, str(len(unpushed.splitlines())) + " commit(s) not pushed"

    return True, "repo clean and pushed"


def main():
    checks = [
        ("Blender", check_blender),
        ("Unity", check_unity),
        ("Repo", check_repo),
    ]

    all_passed = True
    for label, check in checks:
        passed, detail = check()
        print(("  OK   " if passed else "  FAIL ") + label.ljust(9) + detail)
        if not passed:
            all_passed = False

    print()
    if all_passed:
        print("GO - start the session with:  claude --remote-control onevalley")
        return 0
    print("NO-GO - fix the FAIL lines above, then re-run.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
