#!/usr/bin/env bash
set -Eeuo pipefail

PROJECT_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
RESET_DATA=false
BUILD=true
WAIT=false

usage() {
  cat <<'EOF'
用法: ./scripts/restart.sh [选项]

重启 Docker Compose 开发环境。默认保留数据库和对象存储数据。

选项:
  --reset-data  删除 Compose 数据卷后重建（不可恢复）
  --no-build    不重新构建镜像
  --wait        等待服务进入 running/healthy 状态
  -h, --help    显示帮助
EOF
}

while (($# > 0)); do
  case "$1" in
    --reset-data) RESET_DATA=true ;;
    --no-build) BUILD=false ;;
    --wait) WAIT=true ;;
    -h|--help) usage; exit 0 ;;
    *) echo "未知参数: $1" >&2; usage >&2; exit 2 ;;
  esac
  shift
done

command -v docker >/dev/null 2>&1 || {
  echo "错误: 未找到 docker。" >&2
  exit 127
}
docker compose version >/dev/null

cd "$PROJECT_ROOT"

down_args=(down --remove-orphans)
if [[ "$RESET_DATA" == true ]]; then
  echo "警告: 将删除数据库、MinIO 和前端依赖数据卷。" >&2
  down_args+=(--volumes)
fi
docker compose "${down_args[@]}"

up_args=(up --detach)
if [[ "$BUILD" == true ]]; then
  up_args+=(--build)
fi
if [[ "$WAIT" == true ]]; then
  up_args+=(--wait)
fi
docker compose "${up_args[@]}"
docker compose ps
