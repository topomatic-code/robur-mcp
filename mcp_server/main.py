from tool_bridge_pipe_client import ToolBridgePipeClient
from mcp.server import Server
from mcp.server.streamable_http_manager import StreamableHTTPSessionManager
from mcp.types import EmbeddedResource, TextContent, Tool
from starlette.applications import Starlette
from starlette.requests import Request
from starlette.responses import JSONResponse
from starlette.routing import Mount, Route
from starlette.types import Scope, Receive, Send
from typing import Any
import uvicorn
import asyncio
import json
import contextlib

PIPE_NAME = r"\\.\pipe\robur_tool_bridge"
HOST = "127.0.0.1"
PORT = 8000

server = Server("robur-mcp-http")
tool_bridge = ToolBridgePipeClient(PIPE_NAME)

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


@server.list_tools()
async def list_tools() -> list[Tool]:
    result = await asyncio.to_thread(tool_bridge.request, "list_tools", {})
    tools = result.get("tools", [])
    return [_json_schema_to_tool(tool_def) for tool_def in tools]


@server.call_tool()
async def call_tool(name: str, arguments: dict[str, Any]) -> list[TextContent | EmbeddedResource]:
    result = await asyncio.to_thread(
        tool_bridge.request,
        "call_tool",
        {"tool_name": name, "arguments": arguments or {}},
    )
    return [TextContent(type="text", text=json.dumps(result, ensure_ascii=False, indent=2))]


async def health(_: Request) -> JSONResponse:
    try:
        result = await asyncio.to_thread(tool_bridge.request, "ping", {})
        return JSONResponse({"ok": True, "robur_bridge": result})
    except Exception as exc:
        return JSONResponse({"ok": False, "error": str(exc)}, status_code=503)


async def mcp_app(scope: Scope, receive: Receive, send: Send) -> None:
    await session_manager.handle_request(scope, receive, send)


@contextlib.asynccontextmanager
async def lifespan(app: Starlette):
    async with session_manager.run():
        yield


app = Starlette(
    routes=[
        Route("/health", endpoint=health, methods=["GET"]),
        Mount("/mcp", app=mcp_app),
    ],
    lifespan=lifespan,
)

if __name__ == "__main__":
    uvicorn.run(app, host=HOST, port=PORT)
