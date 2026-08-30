"""Send a Python file to the running Blender addon over its plain TCP socket.

The blender-mcp MCP wrapper times out often; the addon itself listens on 9876 and is
reliable. This talks to it directly so the workflow survives the wrapper being down.

    python Tools/blender_send.py Tools/build_brazier.py

Sending a file rather than an inline string keeps us out of shell-quoting trouble.
"""

import json
import socket
import sys

HOST = "127.0.0.1"
PORT = 9876


def send(payload, timeout_seconds):
    """Send one JSON command and return the decoded reply."""
    connection = socket.socket()
    connection.settimeout(timeout_seconds)
    connection.connect((HOST, PORT))
    connection.sendall(json.dumps(payload).encode("utf-8"))

    # The addon writes one JSON object and keeps the socket open, so read until the
    # accumulated text parses rather than waiting for the peer to close.
    chunks = b""
    while True:
        chunk = connection.recv(65536)
        if not chunk:
            break
        chunks += chunk
        try:
            return json.loads(chunks.decode("utf-8"))
        except json.JSONDecodeError:
            continue

    connection.close()
    return json.loads(chunks.decode("utf-8"))


def main():
    if len(sys.argv) < 2:
        print("usage: blender_send.py <script.py> [timeout_seconds]")
        return 2

    script_path = sys.argv[1]
    timeout_seconds = float(sys.argv[2]) if len(sys.argv) > 2 else 180.0

    with open(script_path, "r", encoding="utf-8") as handle:
        code = handle.read()

    reply = send({"type": "execute_code", "params": {"code": code}}, timeout_seconds)

    if reply.get("status") != "success":
        print("BLENDER ERROR:")
        print(json.dumps(reply, indent=2)[:4000])
        return 1

    result = reply.get("result", {})
    # The addon returns the script's stdout under one of a couple of keys depending on
    # its version, so print whatever is actually there.
    for key in ("result", "output", "message"):
        if key in result and result[key]:
            print(result[key])
            break
    else:
        print(json.dumps(result, indent=2)[:4000])
    return 0


if __name__ == "__main__":
    sys.exit(main())
