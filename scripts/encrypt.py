#!/usr/bin/env python3
"""生成与 ASP.NET BCrypt.Net 兼容的 BCrypt 密码摘要。"""

from __future__ import annotations

import argparse
import getpass
import sys

try:
    import bcrypt
except ImportError as exc:  # pragma: no cover - 仅用于命令行依赖提示
    raise SystemExit(
        "缺少 bcrypt。请运行: uv run --with bcrypt scripts/encrypt.py"
    ) from exc


def encrypt_password(password: str, rounds: int = 11) -> str:
    if not password:
        raise ValueError("密码不能为空")
    return bcrypt.hashpw(password.encode("utf-8"), bcrypt.gensalt(rounds)).decode("utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "password",
        nargs="?",
        help="可选；省略时安全交互输入，避免密码进入 shell 历史",
    )
    parser.add_argument("--rounds", type=int, default=11, choices=range(4, 32), metavar="4-31")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    password = args.password if args.password is not None else getpass.getpass("密码: ")
    try:
        print(encrypt_password(password, args.rounds))
    except ValueError as exc:
        print(f"错误: {exc}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
