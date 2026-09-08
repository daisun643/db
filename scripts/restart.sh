#!/usr/bin/env bash
set -Eeuo pipefail

PROJECT_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
RESET_DATA=false
BUILD=true
WAIT=false

usage() {
  cat <<'EOF'
用法: ./scripts/restart.sh [选项]

重启 Docker Compose 生产环境。默认保留数据库和对象存储数据。

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

if [[ ! -f "$PROJECT_ROOT/.env" ]]; then
  echo "错误: 未找到 .env，请先执行 cp .env.example .env 并填写生产配置。" >&2
  exit 1
fi

# Compose reads .env itself; exporting it also lets the optional avatar import
# step use the same credentials after an explicit data reset.
set -a
# shellcheck disable=SC1091
. "$PROJECT_ROOT/.env"
set +a

compose=(docker compose --env-file "$PROJECT_ROOT/.env" -f "$PROJECT_ROOT/docker-compose.yml")

down_args=(down --remove-orphans)
if [[ "$RESET_DATA" == true ]]; then
  echo "警告: 将删除数据库、MinIO 和 ASP.NET Data Protection 数据卷。" >&2
  down_args+=(--volumes)
fi
"${compose[@]}" "${down_args[@]}"

up_args=(up --detach)
if [[ "$BUILD" == true ]]; then
  up_args+=(--build)
fi
if [[ "$WAIT" == true ]]; then
  up_args+=(--wait)
fi
"${compose[@]}" "${up_args[@]}"

# 重置数据后，数据库种子脚本会由 Oracle 容器自动执行（含 07_seed_user_avatars.sql），
# 但 MinIO 中的头像图片随数据卷被删除，需重新导入，否则 /uploads/avatars/* 返回 404。
if [[ "$RESET_DATA" == true ]]; then
  echo "==> 等待 MinIO 和 Oracle XEPDB1 就绪后重新导入用户头像..."

  ready=false

  for i in $(seq 1 120); do
    minio_ok=false
    oracle_ok=false

    # 1. MinIO 必须真正可访问
    if docker exec \
      -e "MC_HOST_local=http://${MINIO_ROOT_USER}:${MINIO_ROOT_PASSWORD}@127.0.0.1:9000" \
      minio \
      mc ready local --quiet >/dev/null 2>&1; then
      minio_ok=true
    fi

    # 2. Oracle 容器必须 healthy
    if [[ "$(docker inspect -f '{{.State.Health.Status}}' oracle-db 2>/dev/null)" == "healthy" ]]; then

      # 3. APP_USER 必须真正能连接 XEPDB1
      if docker exec \
        -e "APP_USER=$APP_USER" \
        -e "APP_USER_PASSWORD=$APP_USER_PASSWORD" \
        oracle-db \
        sh -c '
          echo "SELECT 1 FROM DUAL;" |
          sqlplus -L -s "$APP_USER/$APP_USER_PASSWORD@//127.0.0.1:1521/XEPDB1"
        ' 2>/dev/null | grep -q "1"; then
        oracle_ok=true
      fi
    fi

    if [[ "$minio_ok" == true && "$oracle_ok" == true ]]; then
      ready=true
      break
    fi

    sleep 3
  done

  if [[ "$ready" != true ]]; then
    echo "错误: MinIO 或 Oracle XEPDB1 未能正常就绪。" >&2
    echo "==> Oracle Listener 状态：" >&2
    docker exec oracle-db lsnrctl status || true
    exit 1
  fi

  bash "$PROJECT_ROOT/scripts/import_user_avatars.sh"
fi

"${compose[@]}" ps
