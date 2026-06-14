import math
import requests
from datetime import datetime
from pathlib import Path

import chromadb
from chromadb import EmbeddingFunction, Documents, Embeddings

OLLAMA_URL = "http://localhost:11434"
EMBED_MODEL = "nomic-embed-text"
CHROMA_PATH = str(Path(__file__).parent / "chroma_db")
COLLECTION_NAME = "kaya_memories"


class OllamaEmbedding(EmbeddingFunction):
    def __call__(self, input: Documents) -> Embeddings:
        embeddings = []
        for text in input:
            r = requests.post(
                f"{OLLAMA_URL}/api/embeddings",
                json={"model": EMBED_MODEL, "prompt": text},
                timeout=30,
            )
            r.raise_for_status()
            embeddings.append(r.json()["embedding"])
        return embeddings


def _collection():
    client = chromadb.PersistentClient(path=CHROMA_PATH)
    return client.get_or_create_collection(
        name=COLLECTION_NAME,
        embedding_function=OllamaEmbedding(),
        metadata={"hnsw:space": "cosine"},
    )


def remember(text: str) -> str:
    col = _collection()
    now = datetime.now()
    memory_id = f"mem_{now.timestamp()}"
    col.add(
        ids=[memory_id],
        documents=[text],
        metadatas=[{
            "timestamp": now.isoformat(),
            "unix_ts": now.timestamp(),
        }],
    )
    return f"Remembered: {text}"


def forget(query: str) -> str:
    col = _collection()
    if col.count() == 0:
        return "No memories stored."
    results = col.query(query_texts=[query], n_results=1, include=["documents"])
    if not results["ids"][0]:
        return "No matching memory found."
    memory_text = results["documents"][0][0]
    col.delete(ids=[results["ids"][0][0]])
    return f"Forgot: {memory_text}"


def search(query: str, n: int = 5) -> list[dict]:
    col = _collection()
    count = col.count()
    if count == 0:
        return []
    results = col.query(
        query_texts=[query],
        n_results=min(n, count),
        include=["documents", "metadatas", "distances"],
    )
    now_ts = datetime.now().timestamp()
    memories = []
    for doc, meta, dist in zip(
        results["documents"][0],
        results["metadatas"][0],
        results["distances"][0],
    ):
        age_days = (now_ts - meta["unix_ts"]) / 86400
        recency = math.exp(-age_days / 30)       # decays over ~30 days
        semantic = max(0.0, 1.0 - dist)          # cosine distance → similarity
        score = round(0.7 * semantic + 0.3 * recency, 4)
        memories.append({
            "text": doc,
            "timestamp": meta["timestamp"],
            "score": score,
        })
    return sorted(memories, key=lambda m: m["score"], reverse=True)


def list_all() -> list[dict]:
    col = _collection()
    if col.count() == 0:
        return []
    results = col.get(include=["documents", "metadatas"])
    pairs = zip(results["documents"], results["metadatas"])
    return sorted(
        [{"text": d, "timestamp": m["timestamp"]} for d, m in pairs],
        key=lambda x: x["timestamp"],
        reverse=True,
    )


def count() -> int:
    return _collection().count()
