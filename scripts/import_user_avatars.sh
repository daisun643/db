#!/usr/bin/env bash
# 导入演示用户头像：
#   1. 将 database/avatars/user-avatar-{1..4}.png 上传到 MinIO（forum-media/avatars/）
#   2. 执行 database/07_seed_user_avatars.sql 写入 MediaFile / UserAvatar 记录
# 两步均幂等，可重复执行。
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
AVATAR_DIR="$REPO_ROOT/database/avatars"
SQL_FILE="$REPO_ROOT/database/07_seed_user_avatars.sql"

MINIO_CONTAINER="${MINIO_CONTAINER:-minio}"
ORACLE_CONTAINER="${ORACLE_CONTAINER:-oracle-db}"
MINIO_ROOT_USER="${MINIO_ROOT_USER:?请在 .env 中设置 MINIO_ROOT_USER}"
MINIO_ROOT_PASSWORD="${MINIO_ROOT_PASSWORD:?请在 .env 中设置 MINIO_ROOT_PASSWORD}"
APP_USER="${APP_USER:?请在 .env 中设置 APP_USER}"
APP_USER_PASSWORD="${APP_USER_PASSWORD:?请在 .env 中设置 APP_USER_PASSWORD}"
MINIO_ENDPOINT="http://${MINIO_ROOT_USER}:${MINIO_ROOT_PASSWORD}@127.0.0.1:9000"
MINIO_BUCKET="local/forum-media/avatars"

echo "==> 上传头像图片到 MinIO ($MINIO_BUCKET/)"
for i in 1 2 3 4; do
    file="$AVATAR_DIR/user-avatar-$i.png"
    if [ ! -f "$file" ]; then
        echo "缺少文件: $file（git-lfs 文件请先执行 git lfs pull）" >&2
        exit 1
    fi
    docker cp "$file" "$MINIO_CONTAINER:/tmp/user-avatar-$i.png"
done

# 重置数据卷后 MinIO 是全新实例，后端建桶可能尚未完成，这里幂等地确保桶存在
echo "==> 确保 forum-media 存储桶存在"
docker exec -e "MC_HOST_local=$MINIO_ENDPOINT" "$MINIO_CONTAINER" \
    mc mb --ignore-existing local/forum-media

docker exec -e "MC_HOST_local=$MINIO_ENDPOINT" "$MINIO_CONTAINER" \
    mc cp /tmp/user-avatar-1.png /tmp/user-avatar-2.png /tmp/user-avatar-3.png /tmp/user-avatar-4.png "$MINIO_BUCKET/"
docker exec "$MINIO_CONTAINER" rm -f /tmp/user-avatar-1.png /tmp/user-avatar-2.png /tmp/user-avatar-3.png /tmp/user-avatar-4.png

echo "==> 执行 $SQL_FILE"
docker exec -i -e "APP_USER=$APP_USER" -e "APP_USER_PASSWORD=$APP_USER_PASSWORD" "$ORACLE_CONTAINER" \
    sh -c 'sqlplus -s "$APP_USER/$APP_USER_PASSWORD@localhost:1521/XEPDB1"' \
    < "$SQL_FILE"

echo "==> 校验头像关联记录"
docker exec -e "APP_USER=$APP_USER" -e "APP_USER_PASSWORD=$APP_USER_PASSWORD" "$ORACLE_CONTAINER" sh -c "echo \"
SET LINESIZE 120
SELECT u.\\\"email\\\", m.\\\"url\\\" FROM \\\"UserAvatar\\\" ua
JOIN \\\"User\\\" u ON u.\\\"userId\\\" = ua.\\\"userId\\\"
JOIN \\\"MediaFile\\\" m ON m.\\\"mediaId\\\" = ua.\\\"mediaId\\\"
ORDER BY u.\\\"email\\\";\" | sqlplus -s \"\$APP_USER/\$APP_USER_PASSWORD@localhost:1521/XEPDB1\""

echo "完成。可通过 Nginx 的 /uploads/avatars/user-avatar-1.png 路径验证图片访问。"
