-- ============================================================
-- 06_seed_scenarios.sql
-- 收藏、媒体、钱包、订单和仲裁等跨模块演示场景
-- ============================================================

ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;
SET DEFINE OFF;

-- 用户资料与钱包
UPDATE "User" SET "nickname" = '同济小管家', "bio" = '校园论坛管理员，负责社区与交易秩序。' WHERE "email" = '1@tongji.edu.cn';
UPDATE "User" SET "nickname" = '闲置循环站', "bio" = '让闲置物品在校园里继续发挥价值。' WHERE "email" = '2@tongji.edu.cn';
UPDATE "User" SET "nickname" = '技术版小助手', "bio" = '热爱前端、后端与开源协作。' WHERE "email" = '3@tongji.edu.cn';
UPDATE "User" SET "nickname" = '嘉定校区同学', "bio" = '分享学习经验和校园生活。' WHERE "email" = '4@tongji.edu.cn';

INSERT INTO "Wallet" ("balance", "payPassword", "userId")
SELECT 628.50, NULL, "userId" FROM "User" u WHERE u."email" = '1@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "Wallet" w WHERE w."userId" = u."userId");
INSERT INTO "Wallet" ("balance", "payPassword", "userId")
SELECT 356.00, NULL, "userId" FROM "User" u WHERE u."email" = '2@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "Wallet" w WHERE w."userId" = u."userId");
INSERT INTO "Wallet" ("balance", "payPassword", "userId")
SELECT 188.80, NULL, "userId" FROM "User" u WHERE u."email" = '3@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "Wallet" w WHERE w."userId" = u."userId");
INSERT INTO "Wallet" ("balance", "payPassword", "userId")
SELECT 92.50, NULL, "userId" FROM "User" u WHERE u."email" = '4@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "Wallet" w WHERE w."userId" = u."userId");

-- 更多帖子，覆盖活动、学习、求职和公告板块
INSERT INTO "Post" ("title", "content", "heatScore", "likeCount", "viewCount", "createTime", "updateTime", "status", "userId", "forumId")
SELECT '本周五四平路校区夜跑活动报名', '周五晚七点在一二九操场集合，按五公里和十公里分组，欢迎第一次参加夜跑的同学。请自备饮用水。', 36, 0, 96, SYSTIMESTAMP - INTERVAL '18' HOUR, SYSTIMESTAMP - INTERVAL '18' HOUR, 'Active', u."userId", f."forumId"
FROM "User" u, "Forum" f WHERE u."email" = '3@tongji.edu.cn' AND f."forumName" = '校园活动'
AND NOT EXISTS (SELECT 1 FROM "Post" WHERE "title" = '本周五四平路校区夜跑活动报名');

INSERT INTO "Post" ("title", "content", "heatScore", "likeCount", "viewCount", "createTime", "updateTime", "status", "userId", "forumId")
SELECT '高等数学期末复习资料索引', '整理了历年题、知识点清单和常见易错题。建议先按章节查漏补缺，再用两套模拟卷控制答题时间。资料链接请在评论区按需交流。', 58, 0, 183, SYSTIMESTAMP - INTERVAL '2' DAY, SYSTIMESTAMP - INTERVAL '1' DAY, 'Active', u."userId", f."forumId"
FROM "User" u, "Forum" f WHERE u."email" = '4@tongji.edu.cn' AND f."forumName" = '学习交流'
AND NOT EXISTS (SELECT 1 FROM "Post" WHERE "title" = '高等数学期末复习资料索引');

INSERT INTO "Post" ("title", "content", "heatScore", "likeCount", "viewCount", "createTime", "updateTime", "status", "userId", "forumId")
SELECT '暑期实习简历互助修改', '准备互联网和制造业暑期实习的同学可以在楼内说明目标岗位，大家互相检查项目描述、量化结果和排版。请勿公开手机号等敏感信息。', 44, 0, 127, SYSTIMESTAMP - INTERVAL '3' DAY, SYSTIMESTAMP - INTERVAL '2' DAY, 'Active', u."userId", f."forumId"
FROM "User" u, "Forum" f WHERE u."email" = '2@tongji.edu.cn' AND f."forumName" = '求职求助'
AND NOT EXISTS (SELECT 1 FROM "Post" WHERE "title" = '暑期实习简历互助修改');

INSERT INTO "Post" ("title", "content", "heatScore", "likeCount", "viewCount", "createTime", "updateTime", "status", "userId", "forumId")
SELECT '校园二手交易安全提醒', '请优先选择校内当面验货，不要脱离平台沟通付款；贵重物品应核对序列号和购买凭证，发现异常及时发起举报或纠纷。', 72, 0, 268, SYSTIMESTAMP - INTERVAL '7' DAY, SYSTIMESTAMP - INTERVAL '7' DAY, 'Active', u."userId", f."forumId"
FROM "User" u, "Forum" f WHERE u."email" = '1@tongji.edu.cn' AND f."forumName" = '校园公告'
AND NOT EXISTS (SELECT 1 FROM "Post" WHERE "title" = '校园二手交易安全提醒');

INSERT INTO "TagPost" ("postId", "tagId") SELECT p."postId", t."tagId" FROM "Post" p, "PostTag" t WHERE p."title" = '本周五四平路校区夜跑活动报名' AND t."tagName" = '日常';
INSERT INTO "TagPost" ("postId", "tagId") SELECT p."postId", t."tagId" FROM "Post" p, "PostTag" t WHERE p."title" = '高等数学期末复习资料索引' AND t."tagName" = '分享';
INSERT INTO "TagPost" ("postId", "tagId") SELECT p."postId", t."tagId" FROM "Post" p, "PostTag" t WHERE p."title" = '暑期实习简历互助修改' AND t."tagName" = '经验';
INSERT INTO "TagPost" ("postId", "tagId") SELECT p."postId", t."tagId" FROM "Post" p, "PostTag" t WHERE p."title" = '校园二手交易安全提醒' AND t."tagName" = '推荐';

-- 帖子图片：使用外部演示图片，并通过 MediaFile/PostMedia 维护顺序与归属。
INSERT INTO "MediaFile" ("storageProvider", "url", "uploadTime", "uploadedByUserId")
SELECT 'external', 'https://images.unsplash.com/photo-1521587760476-6c12a4b040da?auto=format&fit=crop&w=1200&q=80', SYSTIMESTAMP, p."userId"
FROM "Post" p WHERE p."title" = '图书馆自习攻略：哪个楼层人最少？'
AND NOT EXISTS (SELECT 1 FROM "MediaFile" WHERE "url" = 'https://images.unsplash.com/photo-1521587760476-6c12a4b040da?auto=format&fit=crop&w=1200&q=80');
INSERT INTO "PostMedia" ("postId", "mediaId", "displayOrder")
SELECT p."postId", m."mediaId", 0 FROM "Post" p, "MediaFile" m
WHERE p."title" = '图书馆自习攻略：哪个楼层人最少？'
  AND m."url" = 'https://images.unsplash.com/photo-1521587760476-6c12a4b040da?auto=format&fit=crop&w=1200&q=80'
  AND NOT EXISTS (SELECT 1 FROM "PostMedia" pm WHERE pm."postId" = p."postId" AND (pm."mediaId" = m."mediaId" OR pm."displayOrder" = 0));

INSERT INTO "MediaFile" ("storageProvider", "url", "uploadTime", "uploadedByUserId")
SELECT 'external', 'https://images.unsplash.com/photo-1498243691581-b145c3f54a5a?auto=format&fit=crop&w=1200&q=80', SYSTIMESTAMP, p."userId"
FROM "Post" p WHERE p."title" = '图书馆自习攻略：哪个楼层人最少？'
AND NOT EXISTS (SELECT 1 FROM "MediaFile" WHERE "url" = 'https://images.unsplash.com/photo-1498243691581-b145c3f54a5a?auto=format&fit=crop&w=1200&q=80');
INSERT INTO "PostMedia" ("postId", "mediaId", "displayOrder")
SELECT p."postId", m."mediaId", 1 FROM "Post" p, "MediaFile" m
WHERE p."title" = '图书馆自习攻略：哪个楼层人最少？'
  AND m."url" = 'https://images.unsplash.com/photo-1498243691581-b145c3f54a5a?auto=format&fit=crop&w=1200&q=80'
  AND NOT EXISTS (SELECT 1 FROM "PostMedia" pm WHERE pm."postId" = p."postId" AND (pm."mediaId" = m."mediaId" OR pm."displayOrder" = 1));

INSERT INTO "MediaFile" ("storageProvider", "url", "uploadTime", "uploadedByUserId")
SELECT 'external', 'https://images.unsplash.com/photo-1567521464027-f127ff144326?auto=format&fit=crop&w=1200&q=80', SYSTIMESTAMP, p."userId"
FROM "Post" p WHERE p."title" = '食堂新出的菜品测评来了！'
AND NOT EXISTS (SELECT 1 FROM "MediaFile" WHERE "url" = 'https://images.unsplash.com/photo-1567521464027-f127ff144326?auto=format&fit=crop&w=1200&q=80');
INSERT INTO "PostMedia" ("postId", "mediaId", "displayOrder")
SELECT p."postId", m."mediaId", 0 FROM "Post" p, "MediaFile" m
WHERE p."title" = '食堂新出的菜品测评来了！'
  AND m."url" = 'https://images.unsplash.com/photo-1567521464027-f127ff144326?auto=format&fit=crop&w=1200&q=80'
  AND NOT EXISTS (SELECT 1 FROM "PostMedia" pm WHERE pm."postId" = p."postId" AND (pm."mediaId" = m."mediaId" OR pm."displayOrder" = 0));

INSERT INTO "MediaFile" ("storageProvider", "url", "uploadTime", "uploadedByUserId")
SELECT 'external', 'https://images.unsplash.com/photo-1498050108023-c5249f4df085?auto=format&fit=crop&w=1200&q=80', SYSTIMESTAMP, p."userId"
FROM "Post" p WHERE p."title" = 'Vue 3 + TypeScript 项目搭建教程'
AND NOT EXISTS (SELECT 1 FROM "MediaFile" WHERE "url" = 'https://images.unsplash.com/photo-1498050108023-c5249f4df085?auto=format&fit=crop&w=1200&q=80');
INSERT INTO "PostMedia" ("postId", "mediaId", "displayOrder")
SELECT p."postId", m."mediaId", 0 FROM "Post" p, "MediaFile" m
WHERE p."title" = 'Vue 3 + TypeScript 项目搭建教程'
  AND m."url" = 'https://images.unsplash.com/photo-1498050108023-c5249f4df085?auto=format&fit=crop&w=1200&q=80'
  AND NOT EXISTS (SELECT 1 FROM "PostMedia" pm WHERE pm."postId" = p."postId" AND (pm."mediaId" = m."mediaId" OR pm."displayOrder" = 0));

INSERT INTO "MediaFile" ("storageProvider", "url", "uploadTime", "uploadedByUserId")
SELECT 'external', 'https://images.unsplash.com/photo-1544947950-fa07a98d237f?auto=format&fit=crop&w=1200&q=80', SYSTIMESTAMP, p."userId"
FROM "Post" p WHERE p."title" = '毕业清仓：教材、考研资料、电子设备'
AND NOT EXISTS (SELECT 1 FROM "MediaFile" WHERE "url" = 'https://images.unsplash.com/photo-1544947950-fa07a98d237f?auto=format&fit=crop&w=1200&q=80');
INSERT INTO "PostMedia" ("postId", "mediaId", "displayOrder")
SELECT p."postId", m."mediaId", 0 FROM "Post" p, "MediaFile" m
WHERE p."title" = '毕业清仓：教材、考研资料、电子设备'
  AND m."url" = 'https://images.unsplash.com/photo-1544947950-fa07a98d237f?auto=format&fit=crop&w=1200&q=80'
  AND NOT EXISTS (SELECT 1 FROM "PostMedia" pm WHERE pm."postId" = p."postId" AND (pm."mediaId" = m."mediaId" OR pm."displayOrder" = 0));

INSERT INTO "MediaFile" ("storageProvider", "url", "uploadTime", "uploadedByUserId")
SELECT 'external', 'https://images.unsplash.com/photo-1552674605-db6ffd4facb5?auto=format&fit=crop&w=1200&q=80', SYSTIMESTAMP, p."userId"
FROM "Post" p WHERE p."title" = '本周五四平路校区夜跑活动报名'
AND NOT EXISTS (SELECT 1 FROM "MediaFile" WHERE "url" = 'https://images.unsplash.com/photo-1552674605-db6ffd4facb5?auto=format&fit=crop&w=1200&q=80');
INSERT INTO "PostMedia" ("postId", "mediaId", "displayOrder")
SELECT p."postId", m."mediaId", 0 FROM "Post" p, "MediaFile" m
WHERE p."title" = '本周五四平路校区夜跑活动报名'
  AND m."url" = 'https://images.unsplash.com/photo-1552674605-db6ffd4facb5?auto=format&fit=crop&w=1200&q=80'
  AND NOT EXISTS (SELECT 1 FROM "PostMedia" pm WHERE pm."postId" = p."postId" AND (pm."mediaId" = m."mediaId" OR pm."displayOrder" = 0));

-- 收藏夹和收藏内容
INSERT INTO "FavoriteFolder" ("folderName", "createTime", "userId")
SELECT '学习资料', SYSTIMESTAMP - INTERVAL '10' DAY, u."userId" FROM "User" u WHERE u."email" = '4@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "FavoriteFolder" f WHERE f."userId" = u."userId" AND f."folderName" = '学习资料');
INSERT INTO "FavoriteFolder" ("folderName", "createTime", "userId")
SELECT '校园生活', SYSTIMESTAMP - INTERVAL '8' DAY, u."userId" FROM "User" u WHERE u."email" = '4@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "FavoriteFolder" f WHERE f."userId" = u."userId" AND f."folderName" = '校园生活');
INSERT INTO "FolderPost" ("folderId", "postId")
SELECT f."folderId", p."postId" FROM "FavoriteFolder" f, "Post" p, "User" u
WHERE f."userId" = u."userId" AND u."email" = '4@tongji.edu.cn' AND f."folderName" = '学习资料' AND p."title" = 'C# 异步编程踩坑记录';
INSERT INTO "FolderPost" ("folderId", "postId")
SELECT f."folderId", p."postId" FROM "FavoriteFolder" f, "Post" p, "User" u
WHERE f."userId" = u."userId" AND u."email" = '4@tongji.edu.cn' AND f."folderName" = '学习资料' AND p."title" = '高等数学期末复习资料索引';

-- 更多规范化商品
INSERT INTO "Product" ("title", "description", "categoryId", "conditionId", "price", "stock", "status", "publishTime", "userId")
SELECT '宜家 LERSTA 落地灯', '暖白光落地灯，灯杆和开关完好，适合宿舍阅读角，自提。', c."categoryId", q."conditionId", 45, 1, 'Active', SYSTIMESTAMP - INTERVAL '10' HOUR, u."userId"
FROM "User" u, "ProductCategory" c, "ProductCondition" q WHERE u."email" = '4@tongji.edu.cn' AND c."categoryName" = '生活用品' AND q."conditionName" = '良好'
AND NOT EXISTS (SELECT 1 FROM "Product" WHERE "title" = '宜家 LERSTA 落地灯');
INSERT INTO "Product" ("title", "description", "categoryId", "conditionId", "price", "stock", "status", "publishTime", "userId")
SELECT '羽毛球拍双拍套装', '两支入门羽毛球拍，含拍包和三只训练球，仅使用两次。', c."categoryId", q."conditionId", 90, 1, 'Active', SYSTIMESTAMP - INTERVAL '16' HOUR, u."userId"
FROM "User" u, "ProductCategory" c, "ProductCondition" q WHERE u."email" = '3@tongji.edu.cn' AND c."categoryName" = '运动户外' AND q."conditionName" = '几乎全新'
AND NOT EXISTS (SELECT 1 FROM "Product" WHERE "title" = '羽毛球拍双拍套装');
INSERT INTO "Product" ("title", "description", "categoryId", "conditionId", "price", "stock", "status", "publishTime", "userId")
SELECT 'USB-C 七合一扩展坞', '支持 HDMI、千兆网口、SD 卡和 PD 充电，适合笔记本日常使用。', c."categoryId", q."conditionId", 75, 2, 'Active', SYSTIMESTAMP - INTERVAL '30' HOUR, u."userId"
FROM "User" u, "ProductCategory" c, "ProductCondition" q WHERE u."email" = '2@tongji.edu.cn' AND c."categoryName" = '数码设备' AND q."conditionName" = '良好'
AND NOT EXISTS (SELECT 1 FROM "Product" WHERE "title" = 'USB-C 七合一扩展坞');

-- 商品图片：文件元数据与商品归属分别保存，通过真实外键关联。
INSERT INTO "MediaFile" ("storageProvider", "url", "uploadTime", "uploadedByUserId")
SELECT 'external', 'https://images.unsplash.com/photo-1527864550417-7fd91fc51a46?auto=format&fit=crop&w=900&q=80', SYSTIMESTAMP, u."userId"
FROM "User" u WHERE u."email" = '2@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "MediaFile" WHERE "url" LIKE 'https://images.unsplash.com/photo-1527864550417-7fd91fc51a46%');
INSERT INTO "ProductMedia" ("productId", "mediaId", "displayOrder")
SELECT p."productId", m."mediaId", 0 FROM "Product" p, "MediaFile" m
WHERE p."title" = '机械键盘 Cherry MX 青轴' AND m."url" LIKE 'https://images.unsplash.com/photo-1527864550417-7fd91fc51a46%';

-- 订单留言与仲裁结果，使交易链路可完整演示。
INSERT INTO "OrderMessage" ("content", "sendTime", "isArchived", "transactionId", "senderId")
SELECT '书本可以在四平路校区图书馆门口交付吗？', t."createTime" + INTERVAL '10' MINUTE, '0', t."transactionId", t."userId"
FROM "Transaction" t, "Product" p WHERE t."productId" = p."productId" AND p."title" = '《算法导论》第四版'
AND NOT EXISTS (SELECT 1 FROM "OrderMessage" om WHERE om."transactionId" = t."transactionId");
INSERT INTO "OrderMessage" ("content", "sendTime", "isArchived", "transactionId", "senderId")
SELECT '可以，我会带上购买凭证，验书后再确认收货。', t."createTime" + INTERVAL '20' MINUTE, '0', t."transactionId", p."userId"
FROM "Transaction" t, "Product" p WHERE t."productId" = p."productId" AND p."title" = '《算法导论》第四版';

INSERT INTO "ArbitrationResult" ("decision", "refundAmount", "createTime", "disputeTicketId", "walletId")
SELECT '双方补充凭证后确认不影响使用，买家接受卖家说明，纠纷关闭。', 0, d."assignTime" + INTERVAL '2' HOUR, d."ticketId", w."walletId"
FROM "DisputeTicket" d, "Wallet" w WHERE d."userId" = w."userId" AND d."status" = 'Resolved'
AND NOT EXISTS (SELECT 1 FROM "ArbitrationResult" a WHERE a."disputeTicketId" = d."ticketId");

-- 点赞数由明细事实回算，避免演示数据中的汇总值与 PostLike 不一致。
UPDATE "Post" p
SET p."likeCount" = (SELECT COUNT(*) FROM "PostLike" l WHERE l."postId" = p."postId");

COMMIT;
