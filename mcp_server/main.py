import argparse
import asyncio
import contextlib
import ipaddress
import json
import logging
import uuid
from typing import Any

import jsonschema
import uvicorn
from mcp.server import Server
from mcp.server.streamable_http_manager import StreamableHTTPSessionManager
from mcp.shared.exceptions import McpError
from mcp.types import (
    INTERNAL_ERROR,
    INVALID_PARAMS,
    CallToolRequest,
    CallToolResult,
    ErrorData,
    ServerResult,
    TextContent,
    Tool,
)
from starlette.applications import Starlette
from starlette.requests import Request
from starlette.responses import JSONResponse
from starlette.routing import Mount, Route
from starlette.types import Scope, Receive, Send

from tool_bridge_pipe_client import BridgeError, ToolBridgePipeClient

PIPE_NAME = r"\\.\pipe\robur_tool_bridge"
DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 8000

TOOL_ERROR_CODES: frozenset[str] = frozenset(
    {
        "bad_request",
        "precondition_failed",
        "tool_execution_failed",
    }
)

logger = logging.getLogger(__name__)
server = Server("robur-mcp-http")
tool_bridge = ToolBridgePipeClient(PIPE_NAME)
_tool_cache: dict[str, Tool] = {}

session_manager = StreamableHTTPSessionManager(
    app=server,
    event_store=None,
    json_response=True,
    stateless=True,
)


def _json_schema_to_tool(tool_def: dict[str, Any]) -> Tool:
    return Tool(
        name=tool_def["name"],
        description=tool_def.get("description", ""),
        inputSchema=tool_def.get("inputSchema", {"type": "object", "properties": {}}),
        annotations=tool_def.get("annotations"),
    )


async def _refresh_tools() -> list[Tool]:
    result = await asyncio.to_thread(tool_bridge.request, "list_tools", {})
    tools = result.get("tools", [])
    loaded_tools = [_json_schema_to_tool(tool_def) for tool_def in tools]
    global _tool_cache
    _tool_cache = {tool.name: tool for tool in loaded_tools}
    return loaded_tools


def _tool_error_result(
    code: str,
    message: str,
    details: Any = None,
) -> CallToolResult:
    error = {
        "code": code,
        "message": message,
    }
    if details is not None:
        error["details"] = details
    return CallToolResult(
        content=[
            TextContent(
                type="text",
                text=json.dumps(error, ensure_ascii=False, indent=2),
            )
        ],
        isError=True,
    )


def _get_trace_id(exc: Exception) -> str:
    if isinstance(exc, BridgeError) and isinstance(exc.details, dict):
        trace_id = exc.details.get("trace_id")
        if isinstance(trace_id, str) and trace_id:
            return trace_id
    return uuid.uuid4().hex


def _log_internal_error(exc: Exception, message: str) -> str:
    trace_id = _get_trace_id(exc)
    logger.exception("%s [%s]", message, trace_id)
    return trace_id


def _internal_protocol_error(exc: Exception) -> McpError:
    trace_id = _log_internal_error(exc, "Внутренняя ошибка MCP")
    return McpError(
        ErrorData(
            code=INTERNAL_ERROR,
            message="Internal server error.",
            data={"trace_id": trace_id},
        )
    )


async def _call_tool(
    name: str,
    arguments: dict[str, Any],
) -> list[TextContent]:
    result = await asyncio.to_thread(
        tool_bridge.request,
        "call_tool",
        {"tool_name": name, "arguments": arguments},
    )
    return [TextContent(type="text", text=json.dumps(result, ensure_ascii=False, indent=2))]


@server.list_tools()
async def list_tools() -> list[Tool]:
    try:
        return await _refresh_tools()
    except Exception as exc:
        raise _internal_protocol_error(exc) from exc


async def _handle_call_tool_request(request: CallToolRequest) -> ServerResult:
    name = request.params.name
    arguments = request.params.arguments or {}

    tool = _tool_cache.get(name)
    if tool is None:
        try:
            await _refresh_tools()
        except Exception as exc:
            raise _internal_protocol_error(exc) from exc
        tool = _tool_cache.get(name)
        if tool is None:
            raise McpError(
                ErrorData(
                    code=INVALID_PARAMS,
                    message=f"Unknown tool: {name}",
                )
            )

    try:
        jsonschema.validate(instance=arguments, schema=tool.inputSchema)
    except jsonschema.ValidationError as exc:
        return ServerResult(
            _tool_error_result(
                code="bad_request",
                message=f"Input validation error: {exc.message}",
                details={"path": list(exc.absolute_path)},
            )
        )
    except jsonschema.SchemaError as exc:
        raise _internal_protocol_error(exc) from exc

    try:
        result = await _call_tool(name, arguments)
    except BridgeError as exc:
        # Структура MCP-запроса и inputSchema уже проверены, поэтому bad_request
        # на этом этапе означает ошибку прикладной валидации аргументов.
        if exc.code in TOOL_ERROR_CODES:
            return ServerResult(
                _tool_error_result(
                    code=exc.code,
                    message=exc.message,
                    details=exc.details,
                )
            )
        if exc.code == "tool_not_found":
            raise McpError(
                ErrorData(
                    code=INVALID_PARAMS,
                    message=f"Unknown tool: {name}",
                )
            ) from exc
        raise _internal_protocol_error(exc) from exc
    except Exception as exc:
        raise _internal_protocol_error(exc) from exc

    return ServerResult(CallToolResult(content=result, isError=False))


# Прямая регистрация сохраняет разделение между ошибками инструмента и JSON-RPC.
server.request_handlers[CallToolRequest] = _handle_call_tool_request


async def health(_: Request) -> JSONResponse:
    try:
        result = await asyncio.to_thread(tool_bridge.request, "ping", {})
        return JSONResponse({"ok": True, "robur_bridge": result})
    except Exception as exc:
        trace_id = _log_internal_error(exc, "Проверка tool bridge завершилась ошибкой")
        return JSONResponse(
            {
                "ok": False,
                "error": "Tool bridge is unavailable.",
                "trace_id": trace_id,
            },
            status_code=503,
        )


async def mcp_app(scope: Scope, receive: Receive, send: Send) -> None:
    await session_manager.handle_request(scope, receive, send)


@contextlib.asynccontextmanager
async def lifespan(_: Starlette):
    async with session_manager.run():
        yield


app = Starlette(
    routes=[
        Route("/health", endpoint=health, methods=["GET"]),
        Mount("/mcp", app=mcp_app),
    ],
    lifespan=lifespan,
)


def _port_number(value: str) -> int:
    try:
        port = int(value)
    except ValueError as exc:
        raise argparse.ArgumentTypeError("port must be an integer") from exc
    if not 1 <= port <= 65535:
        raise argparse.ArgumentTypeError("port must be between 1 and 65535")
    return port


def _host_address(value: str) -> str:
    try:
        ipaddress.IPv4Address(value)
    except ipaddress.AddressValueError as exc:
        raise argparse.ArgumentTypeError("host must be a valid IPv4 address") from exc
    return value


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Robur MCP HTTP server")
    parser.add_argument(
        "--host",
        type=_host_address,
        default=DEFAULT_HOST,
        help="HTTP server IPv4 address",
    )
    parser.add_argument(
        "--port",
        type=_port_number,
        default=DEFAULT_PORT,
        help="HTTP server port",
    )
    return parser.parse_args()


if __name__ == "__main__":
    args = _parse_args()
    uvicorn.run(app, host=args.host, port=args.port, ws="none")
