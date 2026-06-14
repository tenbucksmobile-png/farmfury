"""
Kaya Ollama Proxy — runs on :11435, forwards to Ollama :11434.
Intercepts /api/chat to inject memories into the system prompt
and to detect 'remember X' / 'forget X' commands from the user.
"""

import json
import re
import sys

import httpx
from fastapi import FastAPI, Request
from fastapi.responses import StreamingResponse, JSONResponse

sys.path.insert(0, str(__import__("pathlib").Path(__file__).parent))
import kaya_memory as km

OLLAMA_URL = "http://localhost:11434"
REMEMBER_RE = re.compile(r"\bremember\b[:\s]+([^.?!\n]{3,120})", re.IGNORECASE)
FORGET_RE    = re.compile(r"\bforget\b[:\s]+([^.?!\n]{3,120})",   re.IGNORECASE)

app = FastAPI(title="Kaya Ollama Proxy")


def _last_user_text(messages: list[dict]) -> str:
    for msg in reversed(messages):
        if msg.get("role") == "user":
            content = msg.get("content", "")
            if isinstance(content, list):           # multimodal format
                return " ".join(p.get("text", "") for p in content if isinstance(p, dict))
            return content
    return ""


def _inject_memories(data: dict) -> dict:
    messages = data.get("messages", [])
    user_text = _last_user_text(messages)
    if not user_text:
        return data

    # Handle explicit remember / forget
    rem = REMEMBER_RE.search(user_text)
    fgt = FORGET_RE.search(user_text)
    if rem:
        km.remember(rem.group(1).strip())
    if fgt:
        km.forget(fgt.group(1).strip())

    # Retrieve relevant memories
    memories = km.search(user_text, n=5)
    if not memories:
        return data

    lines = [f"- {m['text']}  [{m['timestamp'][:10]}]" for m in memories]
    block = "\n[Kaya's memories about this person:\n" + "\n".join(lines) + "\n]"

    # Append to existing system message or prepend a new one
    for msg in messages:
        if msg.get("role") == "system":
            msg["content"] = msg["content"].rstrip() + block
            return data
    data["messages"] = [{"role": "system", "content": block.lstrip()}] + messages
    return data


@app.api_route("/{path:path}", methods=["GET", "POST", "PUT", "DELETE", "PATCH"])
async def proxy(path: str, request: Request):
    body = await request.body()

    if path == "api/chat" and body:
        try:
            data = json.loads(body)
            data = _inject_memories(data)
            body = json.dumps(data).encode()
        except Exception:
            pass  # if parsing fails just forward unchanged

    headers = {
        k: v for k, v in request.headers.items()
        if k.lower() not in ("host", "content-length")
    }

    async with httpx.AsyncClient(timeout=300.0) as client:
        upstream = await client.request(
            method=request.method,
            url=f"{OLLAMA_URL}/{path}",
            content=body,
            headers=headers,
            params=dict(request.query_params),
        )

    # Stream or return depending on content type
    ct = upstream.headers.get("content-type", "")
    if "text/event-stream" in ct or "application/x-ndjson" in ct:
        return StreamingResponse(
            iter([upstream.content]),
            status_code=upstream.status_code,
            media_type=ct,
        )
    return JSONResponse(
        content=upstream.json() if "application/json" in ct else {},
        status_code=upstream.status_code,
    )
