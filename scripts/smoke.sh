#!/usr/bin/env bash
set -euo pipefail
for port in 5000 5002; do
  for attempt in {1..90}; do
    if curl -fsS "http://localhost:$port/health/ready" >/dev/null; then break; fi
    sleep 2
  done
  curl -fsS "http://localhost:$port/health/ready"
done
# Explicit test-only seed; never used by production startup.
docker compose exec -T sql bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -d BankingDB -i /dev/stdin' <<'SQL'
INSERT INTO Accounts (AccountType, AccountBalance) VALUES ('Smoke', 100), ('Smoke', 0);
GO
SQL
accounts=$(curl -fsS http://localhost:5000/api/Banking)
read -r from_id to_id < <(python3 -c 'import json,sys; a=sorted(json.loads(sys.argv[1]),key=lambda x:x["id"]); print(a[-2]["id"],a[-1]["id"])' "$accounts")
status=$(curl -sS -o /tmp/transfer-response -w '%{http_code}' -H 'Content-Type: application/json' -d "{\"fromAccount\":$from_id,\"toAccount\":$to_id,\"transferAmount\":25}" http://localhost:5000/api/Banking)
test "$status" = 202
for attempt in {1..60}; do
  logs=$(curl -fsS http://localhost:5002/api/Transfer)
  if python3 -c 'import json,sys; a=json.loads(sys.argv[1]); assert any(x["fromAccount"]==int(sys.argv[2]) and x["toAccount"]==int(sys.argv[3]) and x["amount"]==25 for x in a)' "$logs" "$from_id" "$to_id"; then break; fi
  sleep 2
done
curl -fsS http://localhost:5000/api/Banking | python3 -c 'import json,sys; a={x["id"]:x["accountBalance"] for x in json.load(sys.stdin)}; assert a[int(sys.argv[1])]==75 and a[int(sys.argv[2])]==25' "$from_id" "$to_id"
python3 -c 'import json,sys; a=json.loads(sys.argv[1]); assert sum(x["fromAccount"]==int(sys.argv[2]) and x["toAccount"]==int(sys.argv[3]) for x in a)==1' "$logs" "$from_id" "$to_id"
