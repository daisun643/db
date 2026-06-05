#!/usr/bin/env python3
"""
SHA-256密码摘要工具
用法: python encrypt.py <password>
示例: python encrypt.py "MyPassword123"
"""

import hashlib
import sys

PASSWORD_PREFIX = "tjuer"

def encrypt_password(password):
    """生成前端一致的密码摘要"""
    return hashlib.sha256(f"{PASSWORD_PREFIX}{password}".encode("utf-8")).hexdigest()

def main():
    if len(sys.argv) != 2:
        print("用法: python encrypt.py <password>")
        sys.exit(1)
    
    password = sys.argv[1]
    hashed = encrypt_password(password)
    
    print(f"SHA-256摘要: {hashed}")

if __name__ == "__main__":
    main()
