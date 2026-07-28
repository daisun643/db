-- ============================================================
-- 02_indexes.sql
-- Oracle 18c 数据库索引创建脚本
-- ============================================================

-- 切换到 appuser 用户执行
ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;

-- 唯一索引
-- ============================================================

CREATE UNIQUE INDEX "idx_user_email" ON "User"("email");
CREATE UNIQUE INDEX "idx_user_username" ON "User"("username");
CREATE UNIQUE INDEX "idx_user_code" ON "User"("userCode");
CREATE UNIQUE INDEX "idx_role_name" ON "Role"("roleName");
CREATE UNIQUE INDEX "idx_permission_name" ON "Permission"("permissionName");
CREATE UNIQUE INDEX "idx_postlike_post_user" ON "PostLike"("postId", "userId");
CREATE UNIQUE INDEX "idx_wallet_user" ON "Wallet"("userId");
CREATE UNIQUE INDEX "idx_forum_name" ON "Forum"("forumName");
CREATE UNIQUE INDEX "idx_posttag_name" ON "PostTag"("tagName");
CREATE UNIQUE INDEX "idx_favoritefolder_user_name" ON "FavoriteFolder"("userId", "folderName");

-- 普通索引
-- ============================================================

CREATE INDEX "idx_emailcode_email" ON "EmailCode"("email");
CREATE INDEX "idx_emailcode_email_purpose" ON "EmailCode"("email", "purpose");
CREATE INDEX "idx_emailcode_sendtime" ON "EmailCode"("sendTime");
CREATE INDEX "idx_product_status_stock" ON "Product"("status", "stock");
CREATE INDEX "idx_product_category_condition" ON "Product"("categoryId", "conditionId");
CREATE INDEX "idx_media_uploadedby" ON "MediaFile"("uploadedByUserId");
CREATE INDEX "idx_media_upload_time" ON "MediaFile"("uploadTime");
CREATE UNIQUE INDEX "idx_friendship_pair" ON "FriendShip"(LEAST("userId", "friendId"), GREATEST("userId", "friendId"));
CREATE INDEX "idx_private_message_conversation" ON "PrivateMessage"("senderId", "receiverId", "sendTime");
CREATE INDEX "idx_private_message_unread" ON "PrivateMessage"("receiverId", "isRead", "sendTime");
CREATE INDEX "idx_order_message_transaction" ON "OrderMessage"("transactionId", "sendTime");
CREATE INDEX "idx_credit_adjustment_user" ON "CreditAdjustment"("userId", "adjustTime");
CREATE INDEX "idx_report_status" ON "ReportTicket"("status", "createTime");
CREATE INDEX "idx_report_target" ON "ReportTicket"("targetType", "targetId");
CREATE INDEX "idx_notification_event_key" ON "Notification"("eventKey");
COMMIT;
