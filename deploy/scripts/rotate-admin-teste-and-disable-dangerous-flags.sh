#!/usr/bin/env bash
# Rotaciona senha admin TESTE e desliga flags perigosas no VPS.
# Uso (na máquina com SSH da VPS):
#   ./deploy/scripts/rotate-admin-teste-and-disable-dangerous-flags.sh
#
# Não imprime a senha. Grava em /root/.shopflow_admin_teste_password_tmp (chmod 600) na VPS.
# Não commitar senhas. Após sucesso: SHOPFLOW_ADMIN_RESET_PASSWORD=false e
# MercadoPago__WebhookRawCaptureEnabled=false no container api-test.
set -euo pipefail

SSH_KEY="${SHOPFLOW_VPS_SSH_KEY:-$HOME/.ssh/shopflow_actions_vps}"
SSH_HOST="${SHOPFLOW_VPS_HOST:-root@78.47.134.103}"
DEPLOY_DIR="${SHOPFLOW_VPS_DEPLOY_DIR:-/opt/shopflow/app/deploy}"

ssh -i "$SSH_KEY" -o BatchMode=yes -o StrictHostKeyChecking=yes "$SSH_HOST" \
  "DEPLOY_DIR='$DEPLOY_DIR' bash -s" <<'REMOTE'
set -euo pipefail
cd "$DEPLOY_DIR"

echo "BEFORE:"
grep -E "^(MercadoPago__WebhookRawCaptureEnabled|SHOPFLOW_ADMIN_RESET_PASSWORD)=" .env.test

NEW_PASS="$(openssl rand -base64 36 | tr -d '/+=' | head -c 32)"
umask 077
printf '%s' "$NEW_PASS" > /root/.shopflow_admin_teste_password_tmp
chmod 600 /root/.shopflow_admin_teste_password_tmp

python3 - <<'PY'
from pathlib import Path
import re
p = Path(".env.test")
text = p.read_text()
new_pass = Path("/root/.shopflow_admin_teste_password_tmp").read_text()

def set_kv(text, key, value):
    pat = re.compile(rf"^{re.escape(key)}=.*$", re.M)
    line = f"{key}={value}"
    if pat.search(text):
        return pat.sub(line, text, count=1)
    return text.rstrip() + "\n" + line + "\n"

text = set_kv(text, "SHOPFLOW_ADMIN_PASSWORD", new_pass)
text = set_kv(text, "SHOPFLOW_ADMIN_RESET_PASSWORD", "true")
text = set_kv(text, "MercadoPago__WebhookRawCaptureEnabled", "false")
p.write_text(text)
print("env updated (password not logged)")
PY

echo "AFTER_EDIT:"
grep -E "^(MercadoPago__WebhookRawCaptureEnabled|SHOPFLOW_ADMIN_RESET_PASSWORD)=" .env.test

docker compose up -d --force-recreate api-test worker-test
for i in $(seq 1 40); do
  curl -fsS https://api-teste.vipassessoriadigital.com.br/health >/dev/null 2>&1 && echo "health $i" && break
  sleep 2
done

python3 - <<'PY'
import json, urllib.request, http.cookiejar, ssl
from pathlib import Path
API = "https://api-teste.vipassessoriadigital.com.br"
email = next(
    l.split("=", 1)[1].strip()
    for l in Path(".env.test").read_text().splitlines()
    if l.startswith("SHOPFLOW_ADMIN_EMAIL=")
)
password = Path("/root/.shopflow_admin_teste_password_tmp").read_text().strip()
cj = http.cookiejar.CookieJar()
ctx = ssl.create_default_context()
opener = urllib.request.build_opener(
    urllib.request.HTTPCookieProcessor(cj),
    urllib.request.HTTPSHandler(context=ctx),
)

def req(method, path, body=None, headers=None):
    data = None
    hdrs = {"Accept": "application/json"}
    if headers:
        hdrs.update(headers)
    if body is not None:
        data = json.dumps(body).encode()
        hdrs["Content-Type"] = "application/json"
    r = urllib.request.Request(API + path, data=data, headers=hdrs, method=method)
    try:
        with opener.open(r, timeout=60) as resp:
            raw = resp.read().decode()
            return resp.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()[:400]

code, csrf = req("GET", "/api/auth/csrf")
token = csrf["token"]
code, login = req(
    "POST",
    "/api/auth/admin/login",
    {"email": email, "password": password},
    headers={"X-CSRF-TOKEN": token},
)
print("admin_login_after_reset", code)
if code != 200:
    raise SystemExit(login)
PY

python3 - <<'PY'
from pathlib import Path
import re
p = Path(".env.test")
text = p.read_text()
text = re.sub(
    r"^SHOPFLOW_ADMIN_RESET_PASSWORD=.*$",
    "SHOPFLOW_ADMIN_RESET_PASSWORD=false",
    text,
    count=1,
    flags=re.M,
)
text = re.sub(
    r"^MercadoPago__WebhookRawCaptureEnabled=.*$",
    "MercadoPago__WebhookRawCaptureEnabled=false",
    text,
    count=1,
    flags=re.M,
)
p.write_text(text)
print("reset disabled")
PY

docker compose up -d --force-recreate api-test worker-test
for i in $(seq 1 40); do
  curl -fsS https://api-teste.vipassessoriadigital.com.br/health >/dev/null 2>&1 && echo "health2 $i" && break
  sleep 2
done

docker exec shopflow-api-test sh -lc \
  'printenv | sort | grep -E "^(MercadoPago__WebhookRawCaptureEnabled|SHOPFLOW_ADMIN_RESET_PASSWORD|ASPNETCORE_ENVIRONMENT|DataProtection__|AllowedOrigins)"'

python3 - <<'PY'
import json, urllib.request, http.cookiejar, ssl
from pathlib import Path
API = "https://api-teste.vipassessoriadigital.com.br"
email = next(
    l.split("=", 1)[1].strip()
    for l in Path(".env.test").read_text().splitlines()
    if l.startswith("SHOPFLOW_ADMIN_EMAIL=")
)
password = Path("/root/.shopflow_admin_teste_password_tmp").read_text().strip()
cj = http.cookiejar.CookieJar()
ctx = ssl.create_default_context()
opener = urllib.request.build_opener(
    urllib.request.HTTPCookieProcessor(cj),
    urllib.request.HTTPSHandler(context=ctx),
)

def req(method, path, body=None, headers=None):
    data = None
    hdrs = {"Accept": "application/json"}
    if headers:
        hdrs.update(headers)
    if body is not None:
        data = json.dumps(body).encode()
        hdrs["Content-Type"] = "application/json"
    r = urllib.request.Request(API + path, data=data, headers=hdrs, method=method)
    try:
        with opener.open(r, timeout=60) as resp:
            raw = resp.read().decode()
            return resp.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()[:200]

code, csrf = req("GET", "/api/auth/csrf")
token = csrf["token"]
code, _ = req(
    "POST",
    "/api/auth/admin/login",
    {"email": email, "password": password},
    headers={"X-CSRF-TOKEN": token},
)
print("admin_login_final", code)
assert code == 200
print("PASSWORD_ROTATED_OK")
print("password_file=/root/.shopflow_admin_teste_password_tmp")
PY

# HML: only enforce false flags in file (no recreate / no password rotate)
if [[ -f .env.hml ]]; then
  python3 - <<'PY'
from pathlib import Path
import re
p = Path(".env.hml")
text = p.read_text()
for key in ("MercadoPago__WebhookRawCaptureEnabled", "SHOPFLOW_ADMIN_RESET_PASSWORD"):
    pat = re.compile(rf"^{re.escape(key)}=.*$", re.M)
    if pat.search(text):
        text = pat.sub(f"{key}=false", text, count=1)
    else:
        text = text.rstrip() + f"\n{key}=false\n"
p.write_text(text)
print("hml flags set false (file)")
PY
fi

docker compose ps
echo "Done. Retrieve password only via: ssh ... 'cat /root/.shopflow_admin_teste_password_tmp'"
REMOTE
