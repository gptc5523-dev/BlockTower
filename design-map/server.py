"""설계 노드 맵 서버. 회사 LAN 전용, 비밀번호 1개 로그인.

실행: python3 server.py            (서버)
      python3 server.py passwd     (비밀번호 설정/변경)
"""
import getpass, hashlib, hmac, ipaddress, json, os, secrets, sys, time
from http.cookies import SimpleCookie
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

HOST, PORT = "192.168.0.167", 5007          # LAN 인터페이스에만 바인딩 (tailscale 제외)
LAN = ipaddress.ip_network("192.168.0.0/24")
DIR = Path(__file__).resolve().parent
HASH_FILE, MAP_FILE, SEED_FILE = DIR / "password.hash", DIR / "map.json", DIR / "seed.json"
SESSION_TTL = 7 * 24 * 3600
MAX_BODY = 2 * 1024 * 1024

sessions = {}   # ponytail: 메모리 세션, 서버 재시작하면 재로그인. 여러 명 쓰게 되면 파일/DB로
fails = {}      # ip -> (실패 횟수, 첫 실패 시각)


def hash_pw(pw, salt=None):
    salt = salt or secrets.token_bytes(16)
    return salt.hex() + ":" + hashlib.scrypt(pw.encode(), salt=salt, n=2**14, r=8, p=1).hex()


def check_pw(pw):
    stored = HASH_FILE.read_text().strip()
    return hmac.compare_digest(hash_pw(pw, bytes.fromhex(stored.split(":")[0])), stored)


def locked(ip):
    n, t = fails.get(ip, (0, 0))
    if time.time() - t > 300:
        fails.pop(ip, None)
        return False
    return n >= 5    # 5번 틀리면 5분 잠금


class H(BaseHTTPRequestHandler):
    def send(self, code, body=b"", ctype="application/json", headers=()):
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Frame-Options", "DENY")
        for k, v in headers:
            self.send_header(k, v)
        self.end_headers()
        self.wfile.write(body)

    def lan_ok(self):
        if ipaddress.ip_address(self.client_address[0]) in LAN:
            return True
        self.send(403, b'{"error":"lan only"}')
        return False

    def authed(self):
        c = SimpleCookie(self.headers.get("Cookie", ""))
        tok = c["sid"].value if "sid" in c else ""
        exp = sessions.get(tok)
        if exp and exp > time.time():
            return True
        sessions.pop(tok, None)
        return False

    def body(self):
        n = int(self.headers.get("Content-Length") or 0)
        if n > MAX_BODY or self.headers.get("Content-Type", "").split(";")[0] != "application/json":
            return None
        try:
            return json.loads(self.rfile.read(n))
        except ValueError:
            return None

    def do_GET(self):
        if not self.lan_ok():
            return
        if self.path in ("/", "/index.html"):
            self.send(200, (DIR / "index.html").read_bytes(), "text/html; charset=utf-8")
        elif self.path == "/api/map":
            if not self.authed():
                return self.send(401, b'{"error":"login"}')
            src = MAP_FILE if MAP_FILE.exists() else SEED_FILE
            self.send(200, src.read_bytes())
        else:
            self.send(404, b'{"error":"not found"}')

    def do_POST(self):
        if not self.lan_ok():
            return
        ip = self.client_address[0]
        if self.path == "/api/login":
            if locked(ip):
                return self.send(429, b'{"error":"locked"}')
            data = self.body()
            if not isinstance(data, dict) or not check_pw(str(data.get("password", ""))):
                n, t = fails.get(ip, (0, time.time()))
                fails[ip] = (n + 1, t)
                return self.send(401, b'{"error":"wrong"}')
            fails.pop(ip, None)
            tok = secrets.token_urlsafe(32)
            sessions[tok] = time.time() + SESSION_TTL
            cookie = f"sid={tok}; HttpOnly; SameSite=Strict; Path=/; Max-Age={SESSION_TTL}"
            self.send(200, b'{"ok":true}', headers=[("Set-Cookie", cookie)])
        elif self.path == "/api/logout":
            c = SimpleCookie(self.headers.get("Cookie", ""))
            if "sid" in c:
                sessions.pop(c["sid"].value, None)
            self.send(200, b'{"ok":true}', headers=[("Set-Cookie", "sid=; Path=/; Max-Age=0")])
        else:
            self.send(404, b'{"error":"not found"}')

    def do_PUT(self):
        if not self.lan_ok():
            return
        if self.path != "/api/map":
            return self.send(404, b'{"error":"not found"}')
        if not self.authed():
            return self.send(401, b'{"error":"login"}')
        data = self.body()
        if not (isinstance(data, dict) and isinstance(data.get("nodes"), list) and isinstance(data.get("edges"), list)):
            return self.send(400, b'{"error":"bad map"}')
        tmp = MAP_FILE.with_suffix(".tmp")
        tmp.write_text(json.dumps(data, ensure_ascii=False, indent=1))
        os.replace(tmp, MAP_FILE)   # 원자적 저장: 쓰다 죽어도 기존 맵 보존
        self.send(200, b'{"ok":true}')

    def log_message(self, fmt, *args):
        sys.stderr.write(f"{self.client_address[0]} {fmt % args}\n")


if __name__ == "__main__":
    if sys.argv[1:] == ["passwd"]:
        pw = getpass.getpass("새 비밀번호: ")
        if len(pw) < 8 or pw != getpass.getpass("한 번 더: "):
            sys.exit("8자 이상, 두 번 똑같이 입력해야 해요.")
        HASH_FILE.write_text(hash_pw(pw))
        HASH_FILE.chmod(0o600)
        sys.exit("저장했어요. 서버를 재시작하면 기존 로그인은 풀려요.")
    if not HASH_FILE.exists():
        sys.exit("비밀번호가 없어요. 먼저: python3 server.py passwd")
    print(f"http://{HOST}:{PORT}")
    ThreadingHTTPServer((HOST, PORT), H).serve_forever()
