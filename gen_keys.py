#!/usr/bin/env python3
"""TRS-398 Pro — License Key Generator
Run: python3 gen_keys.py [count]
"""
import hmac
import hashlib
import secrets
import sys

CHARS  = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'   # 32 chars, no 0/O/I/1
SECRET = b'TRS398PRO!2026'


def gen_key() -> str:
    g1 = ''.join(secrets.choice(CHARS) for _ in range(5))
    g2 = ''.join(secrets.choice(CHARS) for _ in range(5))
    g3 = ''.join(secrets.choice(CHARS) for _ in range(5))
    data = (g1 + g2 + g3).encode()
    h = hmac.new(SECRET, data, hashlib.sha256).hexdigest()[:5].upper()
    return f'TRS-{g1}-{g2}-{g3}-{h}'


def validate_key(key: str) -> bool:
    k = key.strip().upper().replace(' ', '').replace('\t', '')
    if not k.startswith('TRS-'):
        return False
    parts = k[4:].split('-')
    if len(parts) != 4 or any(len(p) != 5 for p in parts):
        return False
    data = (parts[0] + parts[1] + parts[2]).encode()
    h = hmac.new(SECRET, data, hashlib.sha256).hexdigest()[:5].upper()
    return h == parts[3]


if __name__ == '__main__':
    count = int(sys.argv[1]) if len(sys.argv) > 1 else 5
    print(f'Generating {count} TRS-398 Pro license key(s):\n')
    keys = [gen_key() for _ in range(count)]
    for k in keys:
        ok = validate_key(k)
        print(f'  {k}   {"✓" if ok else "✗"}')
    print()
