#!/usr/bin/env bash
# End-to-end test of the Garage404 API against a LOCAL, THROWAWAY stack (it promotes the user to admin directly
# in MongoDB and creates/deletes data). Never run it against production.
#
# Usage: scripts/e2e.sh <base-url> <email> <password> <mongo-container>
#
# Example:
#   docker network create g404-test
#   docker run -d --name g404-test-db --network g404-test mongo
#   docker build -t garage404-backend:dev garage404-backend
#   docker run -d --name g404-test-api --network g404-test -p 18080:8080 --env-file .env \
#     -e BackendDatabase__ConnectionString=mongodb://g404-test-db:27017 -e Discord__WebhookUrl= \
#     -e RateLimiting__PublicWrite__PermitLimit=1000 -e RateLimiting__Auth__PermitLimit=1000 \
#     garage404-backend:dev
#   scripts/e2e.sh http://localhost:18080 test@example.com password g404-test-db
#
# Requires curl, python3 and docker. The Firebase account must exist; Discord, Google Calendar and email are not
# needed (accepting a reservation, which uses them, is not covered).
set -u
B=$1; USER_EMAIL=$2; USER_PASS=$3; DB=$4
PASS=0; FAIL=0; FAILED=()
J='Content-Type: application/json'
TMP=$(mktemp -d)
# Git Bash on Windows: give native programs (python, curl) a Windows path.
if command -v cygpath >/dev/null; then TMP=$(cygpath -m "$TMP"); fi

# check <name> <expected-code> <curl args...>   -> body in $TMP/body
check() {
  local name=$1 expected=$2; shift 2
  local code
  code=$(curl -s -m 30 -o "$TMP/body" -w '%{http_code}' "$@")
  if [ "$code" = "$expected" ]; then PASS=$((PASS+1)); printf '  ok   %-62s %s\n' "$name" "$code"
  else FAIL=$((FAIL+1)); FAILED+=("$name"); printf '  FAIL %-62s got %s, expected %s: %s\n' "$name" "$code" "$expected" "$(head -c 300 "$TMP/body")"; fi
}
body() { cat "$TMP/body"; }
jget() { python3 -c "import sys,json;d=json.load(open(sys.argv[1],encoding='utf-8'));print(eval(sys.argv[2]))" "$TMP/body" "$1"; }
assert() { if python3 -c "import sys,json;d=json.load(open(sys.argv[1],encoding='utf-8'));sys.exit(0 if eval(sys.argv[2]) else 1)" "$TMP/body" "$2"; then PASS=$((PASS+1)); printf '  ok   %s\n' "$1"; else FAIL=$((FAIL+1)); FAILED+=("$1"); printf '  FAIL %s: %s\n' "$1" "$(head -c 300 "$TMP/body")"; fi; }

echo "== Auth"
check "login (form)" 200 -X POST $B/v1/Auth -d "username=$USER_EMAIL&password=$USER_PASS"
T=$(jget "d['access_token']"); A="Authorization: Bearer $T"
assert "login returns a refresh token" "bool(d.get('refresh_token'))"
check "wrong password -> 401" 401 -X POST $B/v1/Auth -d "username=$USER_EMAIL&password=wrong-password"
check "unknown account -> 401" 401 -X POST $B/v1/Auth -d "username=nobody-g404-test@example.com&password=whatever"
check "missing password -> 400" 400 -X POST $B/v1/Auth -d "username=$USER_EMAIL"
check "JSON body -> 415" 415 -X POST $B/v1/Auth -H "$J" -d "{\"Username\":\"$USER_EMAIL\",\"Password\":\"$USER_PASS\"}"
check "forged token -> 401" 401 -H "Authorization: Bearer abc.def.ghi" $B/v1/User

echo "== Non-admin user"
check "GET /User" 200 -H "$A" $B/v1/User
assert "user is not admin" "d['isAdmin'] is False"
check "GET /ServiceType anonymous -> 200" 200 $B/v1/ServiceType
check "POST /ServiceType -> 403" 403 -X POST $B/v1/ServiceType -H "$A" -H "$J" -d '[{"Type":1,"Name":"x","Slug":"x"}]'
check "POST /Bike -> 403" 403 -X POST $B/v1/Bike -H "$A" -H "$J" -d '{}'
check "GET /Reservation -> 403" 403 -H "$A" $B/v1/Reservation
check "GET /User/all -> 403" 403 -H "$A" $B/v1/User/all
check "POST /Bike anonymous -> 401" 401 -X POST $B/v1/Bike -H "$J" -d '{}'

echo "== Promote the test user in the LOCAL test database"
docker exec "$DB" mongosh --quiet Garage404Db --eval "db.Users.updateOne({Email:'$USER_EMAIL'},{\$set:{IsAdmin:true}}).modifiedCount" | sed 's/^/  modified: /'
check "GET /User" 200 -H "$A" $B/v1/User
assert "user is admin" "d['isAdmin'] is True"

echo "== ServiceType"
check "reservation before any service type -> 400" 400 -X POST $B/v1/Reservation -H "$J" -d '{"Name":"Test","Email":"test@example.com","Phone":"079 000 00 00","PreferredDate":"2030-01-15T09:00:00Z","ServiceType":1,"Vehicle":"Test moto"}'
assert "  ... with a ServiceType field error" "'ServiceType' in d['errors']"
check "POST service types" 200 -X POST $B/v1/ServiceType -H "$A" -H "$J" -d '[{"Type":0,"Name":"Small Service","Slug":"small-service"},{"Type":1,"Name":"Big Service","Slug":"big-service"},{"Type":2,"Name":"Tyre Change","Slug":"tyre-change"}]'
check "POST existing type -> 409" 409 -X POST $B/v1/ServiceType -H "$A" -H "$J" -d '[{"Type":1,"Name":"Again","Slug":"again"}]'
check "POST duplicate in payload -> 400" 400 -X POST $B/v1/ServiceType -H "$A" -H "$J" -d '[{"Type":7,"Name":"A","Slug":"a"},{"Type":7,"Name":"B","Slug":"b"}]'
check "POST bad slug -> 400" 400 -X POST $B/v1/ServiceType -H "$A" -H "$J" -d '[{"Type":8,"Name":"A","Slug":"Not A Slug"}]'
check "POST empty list -> 400" 400 -X POST $B/v1/ServiceType -H "$A" -H "$J" -d '[]'
check "GET service types (anonymous)" 200 $B/v1/ServiceType
assert "3 types sorted by Type" "[x['type'] for x in d]==[0,1,2]"
check "DELETE type 2" 204 -X DELETE -H "$A" $B/v1/ServiceType/2
check "DELETE type 2 again -> 404" 404 -X DELETE -H "$A" $B/v1/ServiceType/2

echo "== Reservation"
RES='{"Name":"Test Client","Email":"test@example.com","Phone":"+41 79 000 00 00","PreferredDate":"2030-01-15T09:00:00Z","ServiceType":1,"Vehicle":"Test moto","Details":"Réservation de test"}'
# Written by Python: Git Bash would re-encode non-ASCII command-line arguments passed to curl.exe.
python3 -c "import sys;open(sys.argv[1],'w',encoding='utf-8').write(sys.argv[2].replace('de test','é test'))" "$TMP/res.json" "${RES/Réservation de test/Reservation de test}"
check "create reservation (anonymous) -> 201" 201 -X POST $B/v1/Reservation -H "$J; charset=utf-8" --data-binary "@$TMP/res.json"
RID=$(jget "d['id']")
assert "accents preserved" "d['details']=='Reservation é test'"
check "Discord down does not fail the request (see above)" 201 -X POST $B/v1/Reservation -H "$J" -d '{"Name":"Second","Email":"second@example.com","Phone":"0790000000","PreferredDate":"2030-02-01T09:00:00Z","ServiceType":0,"Vehicle":"Test"}'
RID2=$(jget "d['id']")
check "client cannot set ActualDate/CalEventId" 201 -X POST $B/v1/Reservation -H "$J" -d '{"Name":"Third","Email":"third@example.com","Phone":"0790000000","PreferredDate":"2030-03-01T09:00:00Z","ServiceType":0,"Vehicle":"Test","ActualDate":"2030-03-01T09:00:00Z","CalEventId":"hacked"}'
assert "  ... both are cleared" "d['actualDate'] is None and d['calEventId'] is None"
RID3=$(jget "d['id']")
check "unknown service type -> 400" 400 -X POST $B/v1/Reservation -H "$J" -d '{"Name":"x","Email":"x@example.com","Phone":"0790000000","PreferredDate":"2030-01-15T09:00:00Z","ServiceType":42,"Vehicle":"x"}'
check "past date -> 400" 400 -X POST $B/v1/Reservation -H "$J" -d '{"Name":"x","Email":"x@example.com","Phone":"0790000000","PreferredDate":"2020-01-15T09:00:00Z","ServiceType":1,"Vehicle":"x"}'
check "invalid email -> 400" 400 -X POST $B/v1/Reservation -H "$J" -d '{"Name":"x","Email":"not-an-email","Phone":"0790000000","PreferredDate":"2030-01-15T09:00:00Z","ServiceType":1,"Vehicle":"x"}'
check "invalid phone -> 400" 400 -X POST $B/v1/Reservation -H "$J" -d '{"Name":"x","Email":"x@example.com","Phone":"call me maybe","PreferredDate":"2030-01-15T09:00:00Z","ServiceType":1,"Vehicle":"x"}'
check "missing fields -> 400" 400 -X POST $B/v1/Reservation -H "$J" -d '{}'
check "GET reservations (admin)" 200 -H "$A" $B/v1/Reservation
assert "3 reservations sorted by preferred date" "len(d)==3 and [x['preferredDate'] for x in d]==sorted(x['preferredDate'] for x in d)"
check "GET reservation by id" 200 -H "$A" $B/v1/Reservation/$RID
check "GET reservation bad id -> 400 problem" 400 -H "$A" $B/v1/Reservation/abc
assert "  ... as problem+json" "d['status']==400"
check "GET reservation unknown id -> 404" 404 -H "$A" $B/v1/Reservation/000000000000000000000000
check "PUT reservation" 200 -X PUT -H "$A" -H "$J" $B/v1/Reservation/$RID -d '{"Name":"Test Client","Email":"test@example.com","Phone":"0790000000","PreferredDate":"2030-01-16T10:00:00Z","ServiceType":0,"Vehicle":"Test moto"}'
check "PUT reservation unknown id -> 404" 404 -X PUT -H "$A" -H "$J" $B/v1/Reservation/000000000000000000000000 -d '{"Name":"x","Email":"x@example.com","Phone":"0790000000","PreferredDate":"2030-01-16T10:00:00Z","ServiceType":0,"Vehicle":"x"}'
check "PUT reservation unknown type -> 400" 400 -X PUT -H "$A" -H "$J" $B/v1/Reservation/$RID -d '{"Name":"x","Email":"x@example.com","Phone":"0790000000","PreferredDate":"2030-01-16T10:00:00Z","ServiceType":42,"Vehicle":"x"}'
check "accept unknown id -> 404" 404 -X PUT -H "$A" -H "$J" $B/v1/Reservation/000000000000000000000000/accept -d '"2030-01-16T10:00:00"'
check "accept ambiguous DST time -> 400" 400 -X PUT -H "$A" -H "$J" $B/v1/Reservation/$RID/accept -d '"2030-10-27T02:30:00"'
check "DELETE reservation" 204 -X DELETE -H "$A" $B/v1/Reservation/$RID
check "DELETE reservation again -> 404" 404 -X DELETE -H "$A" $B/v1/Reservation/$RID
check "DELETE reservation 2" 204 -X DELETE -H "$A" $B/v1/Reservation/$RID2
check "DELETE reservation 3" 204 -X DELETE -H "$A" $B/v1/Reservation/$RID3

echo "== Bike"
BIKE='{"Make":"Royal Enfield","Model":"Himalayan 450","Year":"2024-01-01T00:00:00Z","Kilometers":1200,"EngineSize":452,"KW":29,"LastMFKDate":"2025-05-01T00:00:00Z","Price":6500}'
check "negative price -> 400" 400 -X POST -H "$A" -H "$J" $B/v1/Bike -d '{"Make":"x","Model":"x","Year":"2024-01-01T00:00:00Z","Kilometers":1,"EngineSize":1,"KW":1,"LastMFKDate":"2025-05-01T00:00:00Z","Price":-1}'
check "create bike -> 201" 201 -X POST -H "$A" -H "$J" $B/v1/Bike -d "$BIKE"
BID=$(jget "d['id']")
check "GET bike" 200 $B/v1/Bike/$BID
check "PUT bike" 200 -X PUT -H "$A" -H "$J" $B/v1/Bike/$BID -d "${BIKE/6500/5900}"
check "GET bike after PUT" 200 $B/v1/Bike/$BID
assert "price updated" "d['price']==5900"
check "PUT unknown bike -> 404" 404 -X PUT -H "$A" -H "$J" $B/v1/Bike/000000000000000000000000 -d "$BIKE"
python3 -c "import base64;open(r'$TMP/p.png','wb').write(base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=='))"
echo "not an image" > "$TMP/x.txt"
check "upload image" 200 -X POST -H "$A" $B/v1/Bike/$BID/images -F "images=@$TMP/p.png;type=image/png"
IMG=$(jget "d[0]")
check "image is served" 200 $B$IMG
check "upload text file -> 400" 400 -X POST -H "$A" $B/v1/Bike/$BID/images -F "images=@$TMP/x.txt;type=text/plain"
check "PUT bike keeps images" 200 -X PUT -H "$A" -H "$J" $B/v1/Bike/$BID -d "$BIKE"
assert "  ... images kept" "len(d['images'])==1"
check "DELETE image" 204 -X DELETE -H "$A" $B/v1/Bike/$BID/images/${IMG#/images/}
check "image file removed" 404 $B$IMG
check "upload image again" 200 -X POST -H "$A" $B/v1/Bike/$BID/images -F "images=@$TMP/p.png;type=image/png"
IMG=$(jget "d[0]")
check "DELETE bike" 204 -X DELETE -H "$A" $B/v1/Bike/$BID
check "bike gone" 404 $B/v1/Bike/$BID
check "its images are removed too" 404 $B$IMG

echo "== Blog"
check "create draft" 201 -X POST -H "$A" -H "$J" $B/v1/Blog -d '{"Title":"Draft","Slug":"draft","Content":"..."}'
DRAFT=$(jget "d['id']")
check "create published" 201 -X POST -H "$A" -H "$J" $B/v1/Blog -d '{"Title":"Hello","Slug":"hello","Content":"...","PublishedAt":"2026-01-01T00:00:00Z"}'
PUB=$(jget "d['id']")
check "duplicate slug -> 409" 409 -X POST -H "$A" -H "$J" $B/v1/Blog -d '{"Title":"Hello 2","Slug":"hello","Content":"..."}'
check "invalid slug -> 400" 400 -X POST -H "$A" -H "$J" $B/v1/Blog -d '{"Title":"x","Slug":"Hello World","Content":"..."}'
check "anonymous list" 200 $B/v1/Blog
assert "  ... only the published article" "[x['slug'] for x in d]==['hello']"
check "admin list" 200 -H "$A" $B/v1/Blog
assert "  ... includes the draft" "sorted(x['slug'] for x in d)==['draft','hello']"
check "anonymous GET draft -> 404" 404 $B/v1/Blog/$DRAFT
check "admin GET draft" 200 -H "$A" $B/v1/Blog/$DRAFT
check "GET by slug" 200 $B/v1/Blog/slug/hello
check "PUT to an existing slug -> 409" 409 -X PUT -H "$A" -H "$J" $B/v1/Blog/$DRAFT -d '{"Title":"Draft","Slug":"hello","Content":"..."}'
check "PUT article (same slug is fine)" 200 -X PUT -H "$A" -H "$J" $B/v1/Blog/$PUB -d '{"Title":"Hello!","Slug":"hello","Content":"...","PublishedAt":"2026-01-01T00:00:00Z"}'
check "DELETE draft" 204 -X DELETE -H "$A" $B/v1/Blog/$DRAFT
check "DELETE published" 204 -X DELETE -H "$A" $B/v1/Blog/$PUB
check "DELETE again -> 404" 404 -X DELETE -H "$A" $B/v1/Blog/$PUB

echo "== User"
check "GET /User/all" 200 -H "$A" $B/v1/User/all
check "set-admin unknown user -> 404" 404 -X POST -H "$A" "$B/v1/User/set-admin?isAdmin=true&userId=000000000000000000000000"
check "admin cannot remove own role -> 400" 400 -X POST -H "$A" "$B/v1/User/set-admin?isAdmin=false&userId=$(curl -s -H "$A" $B/v1/User | python3 -c 'import sys,json;print(json.load(sys.stdin)["mongoId"])')"

echo
echo "RESULT: $PASS passed, $FAIL failed"
for f in "${FAILED[@]}"; do echo "  - $f"; done
rm -rf "$TMP"
[ "$FAIL" = 0 ]
