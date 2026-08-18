#!/usr/bin/env python3
"""Host runner for TESTE smoke — loads admin creds from api-test container."""
from __future__ import annotations

import json
import os
import subprocess
import sys

DEPLOY = "/opt/shopflow/app/deploy"
SMOKE = "/tmp/full-site-teste-smoke.py"
OUT = "/tmp/shopflow-full-smoke-result.json"


def env_from_container(name: str) -> str:
    out = subprocess.check_output(
        ["docker", "compose", "exec", "-T", "api-test", "printenv", name],
        cwd=DEPLOY,
    )
    return out.decode().strip().replace("\r", "")


def main() -> int:
    admin_email = env_from_container("SHOPFLOW_ADMIN_EMAIL")
    admin_password = env_from_container("SHOPFLOW_ADMIN_PASSWORD")
    print(
        f"admin_ready={bool(admin_email and admin_password)} "
        f"email_len={len(admin_email)} pass_len={len(admin_password)}",
        flush=True,
    )
    if not admin_email or not admin_password:
        print("missing admin credentials", file=sys.stderr)
        return 2

    env = os.environ.copy()
    env["ADMIN_EMAIL"] = admin_email
    env["ADMIN_PASSWORD"] = admin_password
    env["QA_EMAIL"] = os.environ.get("QA_EMAIL", "victor.a.santanna@gmail.com")
    env["SHOPFLOW_API_BASE"] = os.environ.get(
        "SHOPFLOW_API_BASE", "https://api-teste.vipassessoriadigital.com.br"
    )
    env["SMOKE_OUT"] = OUT

    print("starting_smoke", flush=True)
    proc = subprocess.run([sys.executable, SMOKE], env=env)
    print(f"smoke_exit={proc.returncode}", flush=True)

    if not os.path.exists(OUT):
        print("missing result file", file=sys.stderr)
        return proc.returncode or 2

    with open(OUT, encoding="utf-8") as f:
        r = json.load(f)
    print("decision", r.get("decision"), flush=True)
    print("failed", r.get("failedFlows"), flush=True)
    print("orders", r.get("orders"), flush=True)
    print("products", r.get("products"), flush=True)
    print("bugs", r.get("bugs"), flush=True)
    print("notRun", r.get("notRun"), flush=True)
    for k, v in sorted(r.get("flows", {}).items()):
        print(f"  {v.get('status'):8} {k}", flush=True)
    return proc.returncode


if __name__ == "__main__":
    raise SystemExit(main())
