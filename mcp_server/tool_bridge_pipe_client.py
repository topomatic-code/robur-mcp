import json
import uuid
import win32file
import win32pipe
from typing import Any


class BridgeError(RuntimeError):
    pass


class ToolBridgePipeClient:
    def __init__(self, pipe_name: str):
        self.pipe_name = pipe_name
    
    def request(self, method: str, params: dict[str, Any] | None = None) -> dict[str, Any]:
        request = {
            "id": str(uuid.uuid4()),
            "method": method,
            "params": params or {},
        }
        payload = (json.dumps(request, ensure_ascii=False) + "\n").encode("utf-8")
        handle = None
        try:
            handle = win32file.CreateFile(
                self.pipe_name,
                win32file.GENERIC_READ | win32file.GENERIC_WRITE,
                0,
                None,
                win32file.OPEN_EXISTING,
                0,
                None,
            )
            win32pipe.SetNamedPipeHandleState(handle, win32pipe.PIPE_READMODE_BYTE, None, None)
            win32file.WriteFile(handle, payload)
            response_bytes = b""
            while True:
                _, chunk = win32file.ReadFile(handle, 4096)
                response_bytes += chunk
                if response_bytes.endswith(b"\n"):
                    break
            response = json.loads(response_bytes.decode("utf-8").strip())
            if not response.get("ok", False):
                error = response.get("error") or {}
                raise BridgeError(f"{error.get('code', 'bridge_error')}: {error.get('message', 'Unknown error')}")
            return response["result"]
        finally:
            if handle is not None:
                win32file.CloseHandle(handle)
