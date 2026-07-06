-- 社交关系与私信模块约束
-- 本脚本在基础表创建完成后执行，用于补充业务约束和常用查询索引。
ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;


ALTER TABLE "FriendShip" MODIFY (
    "userId"     NOT NULL,
    "friendId"   NOT NULL,
    "status"     DEFAULT 'Pending' NOT NULL,
    "createTime" DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "updateTime" DEFAULT CURRENT_TIMESTAMP NOT NULL
);

ALTER TABLE "FriendShip" ADD CONSTRAINT "CK_FriendShip_NotSelf"
    CHECK ("userId" <> "friendId");

ALTER TABLE "FriendShip" ADD CONSTRAINT "CK_FriendShip_Status"
    CHECK ("status" IN ('Pending', 'Accepted', 'Rejected'));

-- LEAST/GREATEST 将 (A, B) 与 (B, A) 视为同一对用户，阻止重复或反向重复申请。
CREATE UNIQUE INDEX "UX_FriendShip_UserPair"
    ON "FriendShip" (LEAST("userId", "friendId"), GREATEST("userId", "friendId"));

ALTER TABLE "PrivateMessage" MODIFY (
    "content"    NOT NULL,
    "sendTime"   DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "isRead"     DEFAULT '0' NOT NULL,
    "receiverId" NOT NULL,
    "senderId"   NOT NULL
);

ALTER TABLE "PrivateMessage" ADD CONSTRAINT "CK_PrivateMessage_NotSelf"
    CHECK ("senderId" <> "receiverId");

ALTER TABLE "PrivateMessage" ADD CONSTRAINT "CK_PrivateMessage_IsRead"
    CHECK ("isRead" IN ('0', '1'));

CREATE INDEX "IX_PrivateMessage_Conversation"
    ON "PrivateMessage" ("senderId", "receiverId", "sendTime");

CREATE INDEX "IX_PrivateMessage_Unread"
    ON "PrivateMessage" ("receiverId", "isRead", "sendTime");

COMMIT;
