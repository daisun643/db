-- ============================================================
-- 07_seed_user_avatars.sql
-- Oracle 18c 演示用户头像种子数据
-- ============================================================
-- 头像图片位于 database/avatars/user-avatar-{1..4}.jpg（git-lfs 追踪），
-- 需先上传到 MinIO（forum-media/avatars/），再执行本脚本写入
-- MediaFile / UserAvatar 关联记录。一键导入见:
--   bash scripts/import_user_avatars.sh
-- 后端通过 /uploads/avatars/<objectKey> 代理读取对象存储中的图片。
-- 本脚本幂等，可重复执行。
-- ============================================================
-- 切换到 appuser 用户执行
ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;

-- 用户1 - Admin (1@tongji.edu.cn)
INSERT INTO "MediaFile" ("storageProvider", "objectKey", "fileName", "url", "mimeType", "uploadTime", "uploadedByUserId")
SELECT 's3', 'avatars/user-avatar-1.png', 'user-avatar-1.png', '/uploads/avatars/user-avatar-1.png', 'image/png', SYSTIMESTAMP, u."userId"
FROM "User" u WHERE u."email" = '1@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "MediaFile" WHERE "url" = '/uploads/avatars/user-avatar-1.png');
INSERT INTO "UserAvatar" ("userId", "mediaId")
SELECT u."userId", m."mediaId" FROM "User" u, "MediaFile" m
WHERE u."email" = '1@tongji.edu.cn'
  AND m."url" = '/uploads/avatars/user-avatar-1.png'
  AND NOT EXISTS (SELECT 1 FROM "UserAvatar" ua WHERE ua."userId" = u."userId");

-- 用户2 - Manager (2@tongji.edu.cn)
INSERT INTO "MediaFile" ("storageProvider", "objectKey", "fileName", "url", "mimeType", "uploadTime", "uploadedByUserId")
SELECT 's3', 'avatars/user-avatar-2.png', 'user-avatar-2.png', '/uploads/avatars/user-avatar-2.png', 'image/png', SYSTIMESTAMP, u."userId"
FROM "User" u WHERE u."email" = '2@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "MediaFile" WHERE "url" = '/uploads/avatars/user-avatar-2.png');
INSERT INTO "UserAvatar" ("userId", "mediaId")
SELECT u."userId", m."mediaId" FROM "User" u, "MediaFile" m
WHERE u."email" = '2@tongji.edu.cn'
  AND m."url" = '/uploads/avatars/user-avatar-2.png'
  AND NOT EXISTS (SELECT 1 FROM "UserAvatar" ua WHERE ua."userId" = u."userId");

-- 用户3 - Moderator (3@tongji.edu.cn)
INSERT INTO "MediaFile" ("storageProvider", "objectKey", "fileName", "url", "mimeType", "uploadTime", "uploadedByUserId")
SELECT 's3', 'avatars/user-avatar-3.png', 'user-avatar-3.png', '/uploads/avatars/user-avatar-3.png', 'image/png', SYSTIMESTAMP, u."userId"
FROM "User" u WHERE u."email" = '3@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "MediaFile" WHERE "url" = '/uploads/avatars/user-avatar-3.png');
INSERT INTO "UserAvatar" ("userId", "mediaId")
SELECT u."userId", m."mediaId" FROM "User" u, "MediaFile" m
WHERE u."email" = '3@tongji.edu.cn'
  AND m."url" = '/uploads/avatars/user-avatar-3.png'
  AND NOT EXISTS (SELECT 1 FROM "UserAvatar" ua WHERE ua."userId" = u."userId");

-- 用户4 - User (4@tongji.edu.cn)
INSERT INTO "MediaFile" ("storageProvider", "objectKey", "fileName", "url", "mimeType", "uploadTime", "uploadedByUserId")
SELECT 's3', 'avatars/user-avatar-4.png', 'user-avatar-4.png', '/uploads/avatars/user-avatar-4.png', 'image/png', SYSTIMESTAMP, u."userId"
FROM "User" u WHERE u."email" = '4@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "MediaFile" WHERE "url" = '/uploads/avatars/user-avatar-4.png');
INSERT INTO "UserAvatar" ("userId", "mediaId")
SELECT u."userId", m."mediaId" FROM "User" u, "MediaFile" m
WHERE u."email" = '4@tongji.edu.cn'
  AND m."url" = '/uploads/avatars/user-avatar-4.png'
  AND NOT EXISTS (SELECT 1 FROM "UserAvatar" ua WHERE ua."userId" = u."userId");

COMMIT;
