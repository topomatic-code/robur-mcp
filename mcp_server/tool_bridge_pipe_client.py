import json
import threading
import uuid
from typing import Any

import pywintypes
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
    _FILE_READ_DATA = 0x0001
    _FILE_WRITE_DATA = 0x0002
    _FILE_READ_ATTRIBUTES = 0x0080
    _FILE_WRITE_ATTRIBUTES = 0x0100
    _SYNCHRONIZE = 0x00100000
    _SECURITY_IDENTIFICATION = 0x00010000
    _SECURITY_SQOS_PRESENT = 0x00100000

    def __init__(self, pipe_name: str):
        self.pipe_name = pipe_name
        self._request_lock = threading.Lock()
        self._handle = None
        self._receive_buffer = b""
        self._connection_error: BridgeProtocolError | None = None
        self._connection_attempted = False

    def connect(self) -> None:
        with self._request_lock:
            if self._handle is not None:
                return
            if self._connection_error is not None:
                raise self._connection_error
            if self._connection_attempted:
                raise BridgeProtocolError("Tool bridge connection cannot be reopened.")
            self._connection_attempted = True

            desired_access = (
                self._FILE_READ_DATA
                | self._FILE_WRITE_DATA
                | self._FILE_READ_ATTRIBUTES
                | self._FILE_WRITE_ATTRIBUTES
                | self._SYNCHRONIZE
            )
            flags = self._SECURITY_SQOS_PRESENT | self._SECURITY_IDENTIFICATION
            handle = None
            try:
                handle = win32file.CreateFile(
                    self.pipe_name,
                    desired_access,
                    0,
                    None,
                    win32file.OPEN_EXISTING,
                    flags,
                    None,
                )
                win32pipe.SetNamedPipeHandleState(
                    handle,
                    win32pipe.PIPE_READMODE_BYTE,
                    None,
                    None,
                )
            except pywintypes.error as exc:
                if handle is not None:
                    win32file.CloseHandle(handle)
                error = BridgeProtocolError("Could not connect to the tool bridge.")
                self._connection_error = error
                raise error from exc

            self._handle = handle

    def close(self) -> None:
        with self._request_lock:
            self._close_handle()

    def request(self, method: str, params: dict[str, Any] | None = None) -> Any:
        request_id = str(uuid.uuid4())
        request = {
            "id": request_id,
            "method": method,
            "params": params if params is not None else {},
        }
        payload = (json.dumps(request, ensure_ascii=False) + "\n").encode("utf-8")

        with self._request_lock:
            if self._handle is None:
                if self._connection_error is not None:
                    raise self._connection_error
                raise BridgeProtocolError("Tool bridge is not connected.")
            try:
                win32file.WriteFile(self._handle, payload)
                response_bytes = self._read_response()
            except (pywintypes.error, BridgeProtocolError) as exc:
                error = BridgeProtocolError("Tool bridge connection was lost.")
                self._connection_error = error
                self._close_handle()
                raise error from exc

            try:
                return self._parse_response(response_bytes, request_id)
            except BridgeProtocolError as exc:
                self._connection_error = exc
                self._close_handle()
                raise

    def _read_response(self) -> bytes:
        while b"\n" not in self._receive_buffer:
            _, chunk = win32file.ReadFile(self._handle, 4096)
            if not chunk:
                raise BridgeProtocolError("Tool bridge closed the connection.")
            self._receive_buffer += chunk
        line, _, self._receive_buffer = self._receive_buffer.partition(b"\n")
        return line

    def _close_handle(self) -> None:
        handle = self._handle
        self._handle = None
        self._receive_buffer = b""
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
