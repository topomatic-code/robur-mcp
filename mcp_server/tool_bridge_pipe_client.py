import json
import threading
import uuid
from typing import Any

import win32file
import win32pipe


class BridgeProtocolError(RuntimeError):
    """Нарушение внутреннего протокола обмена с tool bridge."""


class BridgeError(RuntimeError):
    def __init__(
        self,
        code: str,
        message: str,
        details: Any = None,
        request_id: str | None = None,
    ):
        super().__init__(f"{code}: {message}")
        self.code = code
        self.message = message
        self.details = details
        self.request_id = request_id


class ToolBridgePipeClient:
    _MAX_RESPONSE_BYTES = 16 * 1024 * 1024

    def __init__(self, pipe_name: str):
        self.pipe_name = pipe_name
        self._request_lock = threading.Lock()

    def request(self, method: str, params: dict[str, Any] | None = None) -> Any:
        request_id = str(uuid.uuid4())
        request = {
            "id": request_id,
            "method": method,
            "params": params if params is not None else {},
        }
        payload = (json.dumps(request, ensure_ascii=False) + "\n").encode("utf-8")

        # C#-сервер обслуживает одно pipe-соединение за раз.
        with self._request_lock:
            response_bytes = self._exchange(payload)

        return self._parse_response(response_bytes, request_id)

    def _exchange(self, payload: bytes) -> bytes:
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
            while b"\n" not in response_bytes:
                _, chunk = win32file.ReadFile(handle, 4096)
                response_bytes += chunk
                if len(response_bytes) > self._MAX_RESPONSE_BYTES:
                    raise BridgeProtocolError("Tool bridge response is too large.")
            line, _, tail = response_bytes.partition(b"\n")
            if tail.strip():
                raise BridgeProtocolError("Tool bridge returned unexpected data after the response.")
            return line
        finally:
            if handle is not None:
                win32file.CloseHandle(handle)

    @staticmethod
    def _parse_response(payload: bytes, request_id: str) -> Any:
        try:
            response = json.loads(payload.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise BridgeProtocolError("Tool bridge returned invalid JSON.") from exc

        if not isinstance(response, dict):
            raise BridgeProtocolError("Tool bridge response must be an object.")
        if response.get("id") != request_id:
            raise BridgeProtocolError("Tool bridge response id does not match the request.")

        ok = response.get("ok")
        if not isinstance(ok, bool):
            raise BridgeProtocolError("Tool bridge response does not contain a valid ok flag.")
        if ok:
            if "result" not in response:
                raise BridgeProtocolError("Successful tool bridge response has no result.")
            return response["result"]

        error = response.get("error")
        if not isinstance(error, dict):
            raise BridgeProtocolError("Failed tool bridge response has no error object.")
        code = error.get("code")
        message = error.get("message")
        if not isinstance(code, str) or not code:
            raise BridgeProtocolError("Tool bridge error has no valid code.")
        if not isinstance(message, str) or not message:
            raise BridgeProtocolError("Tool bridge error has no valid message.")
        raise BridgeError(
            code=code,
            message=message,
            details=error.get("details"),
            request_id=request_id,
        )
