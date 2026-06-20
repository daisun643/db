#!/usr/bin/env python3
"""
BCrypt密码加密工具
用法: python scripts/encrypt.py <password>
示例: python scripts/encrypt.py "MyPassword123"
"""

import sys
import bcrypt

def encrypt_password(password):
    """使用BCrypt加密密码"""
    salt = bcrypt.gensalt(rounds=11)
    hashed = bcrypt.hashpw(password.encode('utf-8'), salt)
    return hashed.decode('utf-8')

def main():
    if len(sys.argv) != 2:
        print("用法: python scripts/encrypt.py <password>")
        print("示例: python scripts/encrypt.py 'Password1'")
        sys.exit(1)
    
    password = sys.argv[1]
    hashed = encrypt_password(password)
    
    print(f"原始密码: {password}")
    print(f"加密结果: {hashed}")

if __name__ == "__main__":
    main()
