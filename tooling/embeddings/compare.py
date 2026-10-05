"""Reproducible dense-retrieval evaluation using only local Ollama and Python stdlib.

Reports contain IDs/metrics/model digests, never corpus text, prompts or vectors.
No models are installed, deleted or unloaded by this tool.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import hashlib
import math
from pathlib import Path
import statistics
import sys
import time
import urllib.error
import urllib.parse
import urllib.request


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def request(base: str, path: str, body=None, timeout=120):
    url = urllib.parse.urljoin(base.rstrip("/") + "/", path)
    req = urllib.request.Request(url, None if body is None else json.dumps(body).encode(), headers={"Content-Type": "application/json"})
    try:
        with urllib.request.build_opener(NoRedirect()).open(req, timeout=timeout) as response:
            payload = response.read(4 * 1024 * 1024 + 1)
            if len(payload) > 4 * 1024 * 1024:
                raise ValueError("Ollama response exceeded limit")
            return json.loads(payload)
    except (urllib.error.URLError, TimeoutError, json.JSONDecodeError):
        raise RuntimeError("Ollama request failed; check endpoint and installed model. Corpus contents are not logged.") from None


def validate(corpus, profiles):
    docs, queries = corpus["documents"], corpus["queries"]
    if not (1 <= len(docs) <= 200 and 1 <= len(queries) <= 100):
        raise ValueError("Use 1-200 documents and 1-100 queries")
    ids = {x["id"] for x in docs}
    if len(ids) != len(docs) or any(not x["text"] or len(x["text"]) > 1600 for x in docs):
        raise ValueError("Document IDs must be unique; texts must be non-empty and <=1600 characters")
    if len({x["id"] for x in queries}) != len(queries):
        raise ValueError("Query IDs must be unique")
    if any(not x["text"] or len(x["text"]) > 2000 or not x["relevantDocumentIds"] or not set(x["relevantDocumentIds"]) <= ids for x in queries):
        raise ValueError("Each query needs text and valid relevantDocumentIds")
    if not profiles or len({p["model"] for p in profiles}) != len(profiles) or any(p["dimensions"] not in (768, 1024) or p["inputFormat"] not in ("plain", "qwen-query") for p in profiles):
        raise ValueError("Model profiles must be unique and use supported dimensions/input formats")


def embed(base, profile, text, query=False):
    if query and profile["inputFormat"] == "qwen-query":
        text = "Instruct: " + profile["queryInstruction"] + "\nQuery: " + text
    started = time.perf_counter()
    result = request(base, "api/embed", {"model": profile["model"], "input": text, "dimensions": profile["dimensions"], "truncate": False, "keep_alive": "5m"})
    elapsed = (time.perf_counter() - started) * 1000
    vector = result["embeddings"][0]
    if len(vector) != profile["dimensions"] or not all(math.isfinite(x) for x in vector):
        raise ValueError("Unexpected embedding dimensions or non-finite output")
    norm = math.sqrt(sum(x*x for x in vector))
    if norm < 1e-12:
        raise ValueError("Cannot normalize a zero vector")
    return [x/norm for x in vector], elapsed, result.get("load_duration", 0) / 1_000_000


def evaluate(base, profile, corpus, digest, k):
    vectors, times, load_times = [], [], []
    for doc in corpus["documents"]:
        vector, elapsed, loading = embed(base, profile, doc["text"])
        vectors.append((doc["id"], vector)); times.append(elapsed); load_times.append(loading)
    rows = []
    for query in corpus["queries"]:
        vector, elapsed, loading = embed(base, profile, query["text"], True)
        times.append(elapsed); load_times.append(loading)
        ranked = sorted(((doc_id, sum(a*b for a, b in zip(vector, value))) for doc_id, value in vectors), key=lambda x: (-x[1], x[0]))
        relevant = set(query["relevantDocumentIds"])
        hits = sum(doc in relevant for doc, _ in ranked[:k])
        reciprocal = next((1/(i+1) for i, (doc, _) in enumerate(ranked) if doc in relevant), 0)
        rows.append({"queryId": query["id"], "recallAtK": hits/len(relevant), "reciprocalRank": reciprocal, "topDocumentIds": [x[0] for x in ranked[:k]], "latencyMs": round(elapsed, 2)})
    return {**profile, "digest": digest, "revision": digest.removeprefix("sha256:"), "recallAtK": statistics.mean(x["recallAtK"] for x in rows),
            "mrr": statistics.mean(x["reciprocalRank"] for x in rows), "medianLatencyMs": statistics.median(times),
            "p95LatencyMs": sorted(times)[math.ceil(len(times)*.95)-1], "totalLoadMs": sum(load_times), "queries": rows}


def main():
    parser = argparse.ArgumentParser()
    here = Path(__file__).resolve().parent
    parser.add_argument("--url", default="http://localhost:11434/")
    parser.add_argument("--corpus", type=Path, default=here/"sample-corpus.json")
    parser.add_argument("--profiles", type=Path, default=here/"profiles.json")
    parser.add_argument("--output", type=Path, default=here.parent.parent/"artifacts"/"embeddings")
    parser.add_argument("--top-k", type=int, default=3)
    parser.add_argument("--validate-only", action="store_true")
    args = parser.parse_args()
    endpoint = urllib.parse.urlparse(args.url)
    if endpoint.scheme not in ("http", "https") or endpoint.username or endpoint.password or endpoint.query or endpoint.fragment or not endpoint.hostname:
        raise ValueError("Use a controlled HTTP endpoint without embedded credentials")
    corpus = json.loads(args.corpus.read_text(encoding="utf-8-sig")); profiles = json.loads(args.profiles.read_text(encoding="utf-8-sig"))
    validate(corpus, profiles)
    if not 1 <= args.top_k <= 20:
        raise ValueError("top-k must be 1-20")
    if args.validate_only:
        print(f"Validated {len(corpus['documents'])} documents, {len(corpus['queries'])} queries and {len(profiles)} model profiles. No requests sent.")
        return
    installed = {x["name"]: x.get("digest", "") for x in request(args.url, "api/tags", timeout=5).get("models", [])}
    missing = [p["model"] for p in profiles if p["model"] not in installed and p["model"] + ":latest" not in installed]
    if missing:
        raise ValueError("Missing models: " + ", ".join(missing) + ". Install explicitly before evaluating.")
    results = []
    for profile in profiles:
        print("Evaluating " + profile["model"] + " (serial requests)", flush=True)
        digest = installed.get(profile["model"], installed.get(profile["model"] + ":latest", ""))
        results.append(evaluate(args.url, profile, corpus, digest, args.top_k))
    corpus_hash = hashlib.sha256(json.dumps(corpus, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()).hexdigest()
    report = {"createdAt": dt.datetime.now(dt.timezone.utc).isoformat(), "corpusFingerprint": corpus_hash, "topK": args.top_k, "documents": len(corpus["documents"]), "queries": len(corpus["queries"]),
              "method": "dense cosine; normalized vectors; same chunks; Qwen query-only instruction; latency includes model load", "results": results}
    args.output.mkdir(parents=True, exist_ok=True)
    destination = args.output/("comparison-" + dt.datetime.now().strftime("%Y%m%d-%H%M%S") + ".json")
    destination.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    for result in results:
        print(f"{result['model']}: Recall@{args.top_k}={result['recallAtK']:.3f}, MRR={result['mrr']:.3f}, median={result['medianLatencyMs']:.1f}ms")
    print("Report: " + str(destination.resolve()))


if __name__ == "__main__":
    try:
        main()
    except (OSError, KeyError, ValueError, RuntimeError) as error:
        print(type(error).__name__ + ": " + str(error), file=sys.stderr)
        sys.exit(1)
