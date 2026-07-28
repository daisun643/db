#!/usr/bin/env bash
set -Eeuo pipefail

PROJECT_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
RESTART=false
RESET_DATA=false
PYTEST_ARGS=()

usage() {
  cat <<'EOF'
用法: ./scripts/test.sh [选项] [-- pytest参数...]

默认直接测试 BASE_URL（默认 http://localhost:8080），不会重启服务或删除数据。

选项:
  --restart       测试前重启 Compose 服务并等待健康状态
  --reset-data    测试前删除数据卷并重建；隐含 --restart（不可恢复）
  --collect-only  只收集测试，不发送 API 请求
  -h, --help      显示帮助

示例:
  ./scripts/test.sh
  ./scripts/test.sh --restart -- tests/test_forum.py -q
  BASE_URL=http://localhost:8080 ./scripts/test.sh -- -k pagination
EOF
}

while (($# > 0)); do
  case "$1" in
    --restart) RESTART=true; shift ;;
    --reset-data) RESET_DATA=true; RESTART=true; shift ;;
    --collect-only) PYTEST_ARGS+=(--collect-only); shift ;;
    -h|--help) usage; exit 0 ;;
    --) shift; PYTEST_ARGS+=("$@"); break ;;
    *) PYTEST_ARGS+=("$1"); shift ;;
  esac
done

command -v uv >/dev/null 2>&1 || {
  echo "错误: 未找到 uv，请先安装 https://docs.astral.sh/uv/。" >&2
  exit 127
}

if [[ "$RESTART" == true ]]; then
  restart_args=(--wait)
  if [[ "$RESET_DATA" == true ]]; then
    restart_args+=(--reset-data)
  fi
  "$PROJECT_ROOT/scripts/restart.sh" "${restart_args[@]}"
fi

cd "$PROJECT_ROOT/test"
export UV_CACHE_DIR="${UV_CACHE_DIR:-/tmp/tongji-forum-uv-cache}"
uv run pytest "${PYTEST_ARGS[@]}"
