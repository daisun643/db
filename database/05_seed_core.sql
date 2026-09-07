-- ============================================================
-- 05_seed_core.sql
-- 论坛、帖子、商品等核心演示数据
-- ============================================================

ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;

-- 受控商品字典：商品只保存外键，不重复保存分类和成色文本。
INSERT INTO "ProductCategory" ("categoryName", "description", "displayOrder") VALUES ('数码设备', '电脑、手机及数码配件', 10);
INSERT INTO "ProductCategory" ("categoryName", "description", "displayOrder") VALUES ('教材资料', '教材、参考书与学习资料', 20);
INSERT INTO "ProductCategory" ("categoryName", "description", "displayOrder") VALUES ('交通出行', '自行车及通勤用品', 30);
INSERT INTO "ProductCategory" ("categoryName", "description", "displayOrder") VALUES ('生活用品', '宿舍与日常生活用品', 40);
INSERT INTO "ProductCategory" ("categoryName", "description", "displayOrder") VALUES ('运动户外', '体育与户外用品', 50);
INSERT INTO "ProductCategory" ("categoryName", "description", "displayOrder") VALUES ('其他', '未归入其他分类的物品', 999);

INSERT INTO "ProductCondition" ("conditionName", "description", "displayOrder") VALUES ('全新', '未拆封或未使用', 10);
INSERT INTO "ProductCondition" ("conditionName", "description", "displayOrder") VALUES ('几乎全新', '仅短暂试用，无明显痕迹', 20);
INSERT INTO "ProductCondition" ("conditionName", "description", "displayOrder") VALUES ('良好', '正常使用，功能完好', 30);
INSERT INTO "ProductCondition" ("conditionName", "description", "displayOrder") VALUES ('有使用痕迹', '功能正常，有明显使用痕迹', 40);

INSERT INTO "PostSensitiveWord" ("word", "createTime") VALUES ('违禁', SYSTIMESTAMP);
INSERT INTO "PostSensitiveWord" ("word", "createTime") VALUES ('敏感词', SYSTIMESTAMP);
INSERT INTO "PostSensitiveWord" ("word", "createTime") VALUES ('spam', SYSTIMESTAMP);

-- ============================================================
-- 1. 论坛板块
-- ============================================================

INSERT INTO "Forum" ("forumName", "description", "status", "createTime", "creatorId")
SELECT '校园生活', '分享校园日常，聊聊学习、生活、社团活动', 'Active', SYSTIMESTAMP - INTERVAL '30' DAY, "userId"
FROM "User" WHERE "email" = '1@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "Forum" WHERE "forumName" = '校园生活');

INSERT INTO "Forum" ("forumName", "description", "status", "createTime", "creatorId")
SELECT '技术讨论', '编程技术交流、项目经验分享、求助解答', 'Active', SYSTIMESTAMP - INTERVAL '28' DAY, "userId"
FROM "User" WHERE "email" = '1@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "Forum" WHERE "forumName" = '技术讨论');

INSERT INTO "Forum" ("forumName", "description", "status", "createTime", "creatorId")
SELECT '二手交易', '闲置物品转让、求购信息、二手好物推荐', 'Active', SYSTIMESTAMP - INTERVAL '25' DAY, "userId"
FROM "User" WHERE "email" = '1@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "Forum" WHERE "forumName" = '二手交易');

INSERT INTO "Forum" ("forumName", "description", "status", "createTime", "creatorId")
SELECT '校园活动', '发布社团活动、讲座和展会信息', 'Active', SYSTIMESTAMP - INTERVAL '22' DAY, "userId"
FROM "User" WHERE "email" = '1@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "Forum" WHERE "forumName" = '校园活动');

INSERT INTO "Forum" ("forumName", "description", "status", "createTime", "creatorId")
SELECT '学习交流', '课程资料分享与学习交流', 'Active', SYSTIMESTAMP - INTERVAL '21' DAY, "userId"
FROM "User" WHERE "email" = '1@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "Forum" WHERE "forumName" = '学习交流');

INSERT INTO "Forum" ("forumName", "description", "status", "createTime", "creatorId")
SELECT '求职求助', '实习、求职、考研与就业信息分享', 'Active', SYSTIMESTAMP - INTERVAL '20' DAY, "userId"
FROM "User" WHERE "email" = '1@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "Forum" WHERE "forumName" = '求职求助');

INSERT INTO "Forum" ("forumName", "description", "status", "createTime", "creatorId")
SELECT '校园公告', '校园通知与官方通告', 'Active', SYSTIMESTAMP - INTERVAL '19' DAY, "userId"
FROM "User" WHERE "email" = '1@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "Forum" WHERE "forumName" = '校园公告');

INSERT INTO "Forum" ("forumName", "description", "status", "createTime", "creatorId")
SELECT '失物招领', '发布失物和招领信息，帮助物品回到主人身边', 'Active', SYSTIMESTAMP - INTERVAL '18' DAY, "userId"
FROM "User" WHERE "email" = '1@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "Forum" WHERE "forumName" = '失物招领');

-- ============================================================
-- 2. 帖子 — 校园生活板块
-- ============================================================

INSERT INTO "Post" ("title", "content", "likeCount", "viewCount", "createTime", "updateTime", "status", "userId", "forumId")
SELECT '图书馆自习攻略：哪个楼层人最少？', '最近在准备期末考试，发现图书馆每天都爆满。经过一周的蹲点观察，我总结了以下规律：' || CHR(10) || CHR(10) || '1. 三楼东侧上午人最少，靠窗位置充足' || CHR(10) || '2. 五楼研究生区全天都比较安静' || CHR(10) || '3. 一楼咖啡吧附近适合小组讨论，但不适合自习' || CHR(10) || '4. 周末早上8点之前去基本随便坐' || CHR(10) || CHR(10) || '大家还有什么好的自习地点推荐吗？', 15, 230, SYSTIMESTAMP - INTERVAL '5' DAY, SYSTIMESTAMP - INTERVAL '5' DAY, 'Active', "userId", (SELECT "forumId" FROM "Forum" WHERE "forumName" = '校园生活' AND ROWNUM = 1)
FROM "User" WHERE "email" = '4@tongji.edu.cn';

INSERT INTO "Post" ("title", "content", "likeCount", "viewCount", "createTime", "updateTime", "status", "userId", "forumId")
SELECT '食堂新出的菜品测评来了！', '上周食堂二楼新开了一家档口，主打川菜。我连着吃了三天，给大家做个测评：' || CHR(10) || CHR(10) || '水煮鱼：★★★★☆ 鱼肉嫩滑，辣度适中，就是油有点多' || CHR(10) || '宫保鸡丁：★★★☆☆ 味道一般，花生不够脆' || CHR(10) || '麻婆豆腐：★★★★★ 强烈推荐！麻辣鲜香，配饭绝了' || CHR(10) || CHR(10) || '价格：每份15-25元，性价比不错。' || CHR(10) || '大家有去尝过吗？', 28, 410, SYSTIMESTAMP - INTERVAL '3' DAY, SYSTIMESTAMP - INTERVAL '3' DAY, 'Active', "userId", (SELECT "forumId" FROM "Forum" WHERE "forumName" = '校园生活' AND ROWNUM = 1)
FROM "User" WHERE "email" = '3@tongji.edu.cn';

INSERT INTO "Post" ("title", "content", "likeCount", "viewCount", "createTime", "updateTime", "status", "userId", "forumId")
SELECT '社团招新季到了，大家推荐几个社团', '大一新生马上要来了，各社团也开始准备招新了。作为大三老学姐/学长，推荐几个我觉得体验最好的社团：' || CHR(10) || CHR(10) || '1. 机器人社 - 参加过两次RoboMaster，收获满满' || CHR(10) || '2. 摄影社 - 有专业老师指导，还能借用器材' || CHR(10) || '3. 辩论队 - 锻炼逻辑思维，比赛氛围超棒' || CHR(10) || '4. 志愿者协会 - 周末去社区服务，很有意义' || CHR(10) || CHR(10) || '当然最重要的是找到自己喜欢的方向，不要盲目跟风~', 12, 180, SYSTIMESTAMP - INTERVAL '2' DAY, SYSTIMESTAMP - INTERVAL '2' DAY, 'Active', "userId", (SELECT "forumId" FROM "Forum" WHERE "forumName" = '校园生活' AND ROWNUM = 1)
FROM "User" WHERE "email" = '2@tongji.edu.cn';

-- ============================================================
-- 3. 帖子 — 技术讨论板块
-- ============================================================

INSERT INTO "Post" ("title", "content", "likeCount", "viewCount", "createTime", "updateTime", "status", "userId", "forumId")
SELECT 'C# 异步编程踩坑记录', '最近在做一个 Web API 项目，踩了不少异步编程的坑，记录一下：' || CHR(10) || CHR(10) || '1. 不要在异步方法中使用 lock，应该用 SemaphoreSlim' || CHR(10) || '2. 避免 async void，除了事件处理程序' || CHR(10) || '3. ConfigureAwait(false) 在库代码中很有用' || CHR(10) || '4. 注意 DbContext 的生命周期，不要在多线程中共享' || CHR(10) || CHR(10) || '希望对大家有帮助！', 35, 520, SYSTIMESTAMP - INTERVAL '4' DAY, SYSTIMESTAMP - INTERVAL '4' DAY, 'Active', "userId", (SELECT "forumId" FROM "Forum" WHERE "forumName" = '技术讨论' AND ROWNUM = 1)
FROM "User" WHERE "email" = '1@tongji.edu.cn';

INSERT INTO "Post" ("title", "content", "likeCount", "viewCount", "createTime", "updateTime", "status", "userId", "forumId")
SELECT '求助：Oracle 数据库连接超时问题', '在本地开发环境连接 Oracle XE 数据库时，偶尔会出现连接超时的问题。' || CHR(10) || CHR(10) || '环境配置：' || CHR(10) || '- Oracle 18c XE (Docker)' || CHR(10) || '- .NET 6 + Oracle.ManagedDataAccess.Core' || CHR(10) || '- 连接池大小设置为 10' || CHR(10) || CHR(10) || '已尝试：' || CHR(10) || '1. 增大连接池 - 问题减缓但没根治' || CHR(10) || '2. 检查 Docker 资源限制 - CPU/内存都正常' || CHR(10) || '3. 添加 Validate Connection=true - 报错频率降低' || CHR(10) || CHR(10) || '有没有大佬遇到过类似问题？求指教！', 8, 340, SYSTIMESTAMP - INTERVAL '1' DAY, SYSTIMESTAMP - INTERVAL '1' DAY, 'Active', "userId", (SELECT "forumId" FROM "Forum" WHERE "forumName" = '技术讨论' AND ROWNUM = 1)
FROM "User" WHERE "email" = '4@tongji.edu.cn';

INSERT INTO "Post" ("title", "content", "likeCount", "viewCount", "createTime", "updateTime", "status", "userId", "forumId")
SELECT 'Vue 3 + TypeScript 项目搭建教程', '分享一个从零搭建 Vue 3 + TypeScript 项目的完整流程：' || CHR(10) || CHR(10) || '1. 使用 Vite 创建项目：npm create vite@latest my-app -- --template vue-ts' || CHR(10) || '2. 安装常用依赖：Pinia, Vue Router, Axios' || CHR(10) || '3. 配置 ESLint + Prettier 统一代码风格' || CHR(10) || '4. 设置路径别名 @/ 指向 src/' || CHR(10) || '5. 封装 Axios 请求拦截器' || CHR(10) || CHR(10) || '项目结构推荐：src/api、src/components、src/views、src/stores、src/utils' || CHR(10) || CHR(10) || '完整代码已上传到 GitHub，链接在评论区~', 45, 680, SYSTIMESTAMP - INTERVAL '6' DAY, SYSTIMESTAMP - INTERVAL '6' DAY, 'Active', "userId", (SELECT "forumId" FROM "Forum" WHERE "forumName" = '技术讨论' AND ROWNUM = 1)
FROM "User" WHERE "email" = '3@tongji.edu.cn';

-- ============================================================
-- 4. 帖子 — 二手交易板块
-- ============================================================

INSERT INTO "Post" ("title", "content", "likeCount", "viewCount", "createTime", "updateTime", "status", "userId", "forumId")
SELECT '毕业清仓：教材、考研资料、电子设备', '即将毕业，清理一波闲置，以下是待出物品：' || CHR(10) || CHR(10) || '1. 《数据结构（C语言版）》严蔚敏 - 20元' || CHR(10) || '2. 《计算机网络（第7版）》谢希仁 - 25元' || CHR(10) || '3. 考研数学全书 + 张宇1000题 - 40元（打包）' || CHR(10) || '4. 罗技 K380 蓝牙键盘 - 80元（九成新）' || CHR(10) || '5. 小米充电宝 10000mAh - 50元' || CHR(10) || CHR(10) || '可面交（校内）或快递（运费自理）。' || CHR(10) || '感兴趣的私信我~', 22, 390, SYSTIMESTAMP - INTERVAL '2' DAY, SYSTIMESTAMP - INTERVAL '2' DAY, 'Active', "userId", (SELECT "forumId" FROM "Forum" WHERE "forumName" = '二手交易' AND ROWNUM = 1)
FROM "User" WHERE "email" = '2@tongji.edu.cn';

INSERT INTO "Post" ("title", "content", "likeCount", "viewCount", "createTime", "updateTime", "status", "userId", "forumId")
SELECT '求购：二手显示器，24寸以上', '最近写代码需要一个外接显示器，预算 200-400 元。' || CHR(10) || CHR(10) || '要求：' || CHR(10) || '- 尺寸 24 寸及以上' || CHR(10) || '- 分辨率 1080p 以上' || CHR(10) || '- 有 HDMI 接口' || CHR(10) || '- 屏幕无明显坏点' || CHR(10) || CHR(10) || '有闲置的同学联系我，可以先看看实物。谢谢！', 5, 120, SYSTIMESTAMP - INTERVAL '1' DAY, SYSTIMESTAMP - INTERVAL '1' DAY, 'Active', "userId", (SELECT "forumId" FROM "Forum" WHERE "forumName" = '二手交易' AND ROWNUM = 1)
FROM "User" WHERE "email" = '4@tongji.edu.cn';

-- ============================================================
-- 5. 评论
-- ============================================================

INSERT INTO "PostComment" ("content", "status", "createTime", "postId", "userId", "parentCommentId")
SELECT '五楼确实很安静，我经常去那边写论文。', 'Active', SYSTIMESTAMP - INTERVAL '4' DAY, p."postId", u."userId", NULL
FROM "Post" p, "User" u WHERE p."title" = '图书馆自习攻略：哪个楼层人最少？' AND u."email" = '2@tongji.edu.cn';

INSERT INTO "PostComment" ("content", "status", "createTime", "postId", "userId", "parentCommentId")
SELECT '推荐去南楼的自习室，人少空调足！', 'Active', SYSTIMESTAMP - INTERVAL '4' DAY, p."postId", u."userId", NULL
FROM "Post" p, "User" u WHERE p."title" = '图书馆自习攻略：哪个楼层人最少？' AND u."email" = '3@tongji.edu.cn';

INSERT INTO "PostComment" ("content", "status", "createTime", "postId", "userId", "parentCommentId")
SELECT '写得很详细，已收藏！能加上 Tailwind CSS 的配置就更好了。', 'Active', SYSTIMESTAMP - INTERVAL '5' DAY, p."postId", u."userId", NULL
FROM "Post" p, "User" u WHERE p."title" = 'Vue 3 + TypeScript 项目搭建教程' AND u."email" = '1@tongji.edu.cn';

INSERT INTO "PostComment" ("content", "status", "createTime", "postId", "userId", "parentCommentId")
SELECT '感谢分享！正好在学 Vue 3，这篇教程帮了大忙。', 'Active', SYSTIMESTAMP - INTERVAL '5' DAY, p."postId", u."userId", NULL
FROM "Post" p, "User" u WHERE p."title" = 'Vue 3 + TypeScript 项目搭建教程' AND u."email" = '4@tongji.edu.cn';

INSERT INTO "PostComment" ("content", "status", "createTime", "postId", "userId", "parentCommentId")
SELECT '之前也遇到过类似问题，最后发现是 Docker 的 DNS 配置问题。试试在 docker-compose.yml 里加 dns: 8.8.8.8', 'Active', SYSTIMESTAMP - INTERVAL '1' DAY, p."postId", u."userId", NULL
FROM "Post" p, "User" u WHERE p."title" = '求助：Oracle 数据库连接超时问题' AND u."email" = '1@tongji.edu.cn';

INSERT INTO "PostComment" ("content", "status", "createTime", "postId", "userId", "parentCommentId")
SELECT '数据结构还在吗？我想要一本，可以面交。', 'Active', SYSTIMESTAMP - INTERVAL '1' DAY, p."postId", u."userId", NULL
FROM "Post" p, "User" u WHERE p."title" = '毕业清仓：教材、考研资料、电子设备' AND u."email" = '4@tongji.edu.cn';

-- ============================================================
-- 6. 点赞
-- ============================================================

INSERT INTO "PostLike" ("postId", "userId", "createTime") SELECT p."postId", u."userId", SYSTIMESTAMP - INTERVAL '5' DAY FROM "Post" p, "User" u WHERE p."title" = 'Vue 3 + TypeScript 项目搭建教程' AND u."email" = '1@tongji.edu.cn' AND NOT EXISTS (SELECT 1 FROM "PostLike" WHERE "postId" = p."postId" AND "userId" = u."userId");
INSERT INTO "PostLike" ("postId", "userId", "createTime") SELECT p."postId", u."userId", SYSTIMESTAMP - INTERVAL '5' DAY FROM "Post" p, "User" u WHERE p."title" = 'Vue 3 + TypeScript 项目搭建教程' AND u."email" = '4@tongji.edu.cn' AND NOT EXISTS (SELECT 1 FROM "PostLike" WHERE "postId" = p."postId" AND "userId" = u."userId");
INSERT INTO "PostLike" ("postId", "userId", "createTime") SELECT p."postId", u."userId", SYSTIMESTAMP - INTERVAL '3' DAY FROM "Post" p, "User" u WHERE p."title" = 'C# 异步编程踩坑记录' AND u."email" = '3@tongji.edu.cn' AND NOT EXISTS (SELECT 1 FROM "PostLike" WHERE "postId" = p."postId" AND "userId" = u."userId");
INSERT INTO "PostLike" ("postId", "userId", "createTime") SELECT p."postId", u."userId", SYSTIMESTAMP - INTERVAL '3' DAY FROM "Post" p, "User" u WHERE p."title" = 'C# 异步编程踩坑记录' AND u."email" = '4@tongji.edu.cn' AND NOT EXISTS (SELECT 1 FROM "PostLike" WHERE "postId" = p."postId" AND "userId" = u."userId");
INSERT INTO "PostLike" ("postId", "userId", "createTime") SELECT p."postId", u."userId", SYSTIMESTAMP - INTERVAL '2' DAY FROM "Post" p, "User" u WHERE p."title" = '食堂新出的菜品测评来了！' AND u."email" = '1@tongji.edu.cn' AND NOT EXISTS (SELECT 1 FROM "PostLike" WHERE "postId" = p."postId" AND "userId" = u."userId");
INSERT INTO "PostLike" ("postId", "userId", "createTime") SELECT p."postId", u."userId", SYSTIMESTAMP - INTERVAL '2' DAY FROM "Post" p, "User" u WHERE p."title" = '食堂新出的菜品测评来了！' AND u."email" = '4@tongji.edu.cn' AND NOT EXISTS (SELECT 1 FROM "PostLike" WHERE "postId" = p."postId" AND "userId" = u."userId");

-- ============================================================
-- 7. 商品
-- ============================================================

INSERT INTO "Product" ("title", "description", "categoryId", "conditionId", "price", "stock", "status", "publishTime", "userId")
SELECT '机械键盘 Cherry MX 青轴', 'Cherry G80-3000 机械键盘，青轴手感，使用约半年。键帽已更换为 PBT 热升华键帽。功能完全正常，无任何暗病。附赠拔键器和备用键帽。原价 599 元，现价 280 元出。', (SELECT "categoryId" FROM "ProductCategory" WHERE "categoryName" = '数码设备'), (SELECT "conditionId" FROM "ProductCondition" WHERE "conditionName" = '良好'), 280, 1, 'Active', SYSTIMESTAMP - INTERVAL '3' DAY, "userId"
FROM "User" WHERE "email" = '2@tongji.edu.cn' AND NOT EXISTS (SELECT 1 FROM "Product" WHERE "title" = '机械键盘 Cherry MX 青轴');

INSERT INTO "Product" ("title", "description", "categoryId", "conditionId", "price", "stock", "status", "publishTime", "userId")
SELECT '《算法导论》第四版', '全新未拆封的《算法导论（原书第4版）》，中文版。买重了一本，多出来的这本转让。原价 198 元，现价 120 元。', (SELECT "categoryId" FROM "ProductCategory" WHERE "categoryName" = '教材资料'), (SELECT "conditionId" FROM "ProductCondition" WHERE "conditionName" = '全新'), 120, 1, 'Active', SYSTIMESTAMP - INTERVAL '2' DAY, "userId"
FROM "User" WHERE "email" = '1@tongji.edu.cn' AND NOT EXISTS (SELECT 1 FROM "Product" WHERE "title" = '《算法导论》第四版');

INSERT INTO "Product" ("title", "description", "categoryId", "conditionId", "price", "stock", "status", "publishTime", "userId")
SELECT '二手自行车 捷安特 ATX', '捷安特 ATX 660 山地自行车，骑行约 2000 公里。车况良好，变速器和刹车正常，轮胎今年换过。适合校内通勤，毕业带不走了。自提，价格可小刀。', (SELECT "categoryId" FROM "ProductCategory" WHERE "categoryName" = '交通出行'), (SELECT "conditionId" FROM "ProductCondition" WHERE "conditionName" = '良好'), 350, 1, 'Active', SYSTIMESTAMP - INTERVAL '4' DAY, "userId"
FROM "User" WHERE "email" = '3@tongji.edu.cn' AND NOT EXISTS (SELECT 1 FROM "Product" WHERE "title" = '二手自行车 捷安特 ATX');

INSERT INTO "Product" ("title", "description", "categoryId", "conditionId", "price", "stock", "status", "publishTime", "userId")
SELECT '考研政治全套资料', '包含：肖秀荣精讲精练 + 1000题 + 肖四肖八 + 徐涛核心考案。大部分只翻阅了一两遍，保存良好。打包出售，不拆卖。', (SELECT "categoryId" FROM "ProductCategory" WHERE "categoryName" = '教材资料'), (SELECT "conditionId" FROM "ProductCondition" WHERE "conditionName" = '有使用痕迹'), 65, 1, 'Active', SYSTIMESTAMP - INTERVAL '1' DAY, "userId"
FROM "User" WHERE "email" = '2@tongji.edu.cn' AND NOT EXISTS (SELECT 1 FROM "Product" WHERE "title" = '考研政治全套资料');

INSERT INTO "Product" ("title", "description", "categoryId", "conditionId", "price", "stock", "status", "publishTime", "userId")
SELECT '小米台灯 Pro', '小米智能台灯 Pro，支持色温和亮度无级调节。使用约一年，外观无划痕，功能正常。附带原装电源适配器。', (SELECT "categoryId" FROM "ProductCategory" WHERE "categoryName" = '生活用品'), (SELECT "conditionId" FROM "ProductCondition" WHERE "conditionName" = '良好'), 80, 1, 'Active', SYSTIMESTAMP - INTERVAL '5' DAY, "userId"
FROM "User" WHERE "email" = '4@tongji.edu.cn' AND NOT EXISTS (SELECT 1 FROM "Product" WHERE "title" = '小米台灯 Pro');

INSERT INTO "Product" ("title", "description", "categoryId", "conditionId", "price", "stock", "status", "publishTime", "userId")
SELECT '罗技 G502 鼠标', '罗技 G502 Hero 游戏鼠标，有线版。使用一年半，微动正常无双击问题。附赠额外配重块和原装包装盒。', (SELECT "categoryId" FROM "ProductCategory" WHERE "categoryName" = '数码设备'), (SELECT "conditionId" FROM "ProductCondition" WHERE "conditionName" = '有使用痕迹'), 150, 1, 'Active', SYSTIMESTAMP - INTERVAL '2' DAY, "userId"
FROM "User" WHERE "email" = '3@tongji.edu.cn' AND NOT EXISTS (SELECT 1 FROM "Product" WHERE "title" = '罗技 G502 鼠标');

-- ============================================================
-- 8. 论坛管理员（role：Moderator=版主，Admin=管理员）
-- 演示数据约定：所有版块的版主统一为 1@tongji.edu.cn
-- ============================================================

INSERT INTO "ForumManager" ("forumId", "userId", "role")
SELECT f."forumId", u."userId", 'Moderator' FROM "Forum" f, "User" u
WHERE u."email" = '1@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "ForumManager" fm WHERE fm."forumId" = f."forumId" AND fm."userId" = u."userId");

-- ============================================================
-- 9. 演示用通知关联数据
-- 说明：这些数据不是测试垃圾数据，而是为了让通知中心重建后有正式、可讲解的业务上下文。
-- ============================================================

-- 演示交易：用户4购买 Admin User 发布的《算法导论》第四版
INSERT INTO "Transaction" ("transactionAmount", "transactionStatus", "createTime", "payTime", "userId", "productId")
SELECT pr."price", 'Completed', SYSTIMESTAMP - INTERVAL '5' DAY, SYSTIMESTAMP - INTERVAL '5' DAY + INTERVAL '1' HOUR, buyer."userId", pr."productId"
FROM "Product" pr, "User" buyer
WHERE pr."title" = '《算法导论》第四版'
  AND buyer."email" = '4@tongji.edu.cn'
AND NOT EXISTS (
    SELECT 1 FROM "Transaction" t
    WHERE t."productId" = pr."productId" AND t."userId" = buyer."userId" AND t."transactionStatus" = 'Completed'
);

-- 演示纠纷：围绕上面的历史订单生成一条已解决纠纷记录
INSERT INTO "DisputeTicket" ("reason", "status", "createTime", "assignTime", "transactionId", "userId", "arbitratorId")
SELECT '商品描述与实物细节存在差异，申请管理员协助确认。', 'Resolved', SYSTIMESTAMP - INTERVAL '4' DAY, SYSTIMESTAMP - INTERVAL '4' DAY + INTERVAL '30' MINUTE, t."transactionId", buyer."userId", adminUser."userId"
FROM "Transaction" t, "Product" pr, "User" buyer, "User" adminUser
WHERE t."productId" = pr."productId"
  AND t."userId" = buyer."userId"
  AND pr."title" = '《算法导论》第四版'
  AND buyer."email" = '4@tongji.edu.cn'
  AND adminUser."email" = '1@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "DisputeTicket" d WHERE d."transactionId" = t."transactionId");

-- 演示好友关系：Admin User 与用户4已经是好友
INSERT INTO "FriendShip" ("userId", "friendId", "status", "createTime", "updateTime")
SELECT u4."userId", adminUser."userId", 'Accepted', SYSTIMESTAMP - INTERVAL '3' DAY, SYSTIMESTAMP - INTERVAL '3' DAY + INTERVAL '1' HOUR
FROM "User" u4, "User" adminUser
WHERE u4."email" = '4@tongji.edu.cn'
  AND adminUser."email" = '1@tongji.edu.cn'
AND NOT EXISTS (
    SELECT 1 FROM "FriendShip" f
    WHERE ((f."userId" = u4."userId" AND f."friendId" = adminUser."userId") OR (f."userId" = adminUser."userId" AND f."friendId" = u4."userId"))
);

-- 演示私信：Admin User 给用户4发送一条未读私信
INSERT INTO "PrivateMessage" ("content", "sendTime", "isRead", "receiverId", "senderId")
SELECT '我看到了你在 Oracle 连接问题帖子下的讨论，晚点可以一起看一下配置。', SYSTIMESTAMP - INTERVAL '2' HOUR, '0', u4."userId", adminUser."userId"
FROM "User" u4, "User" adminUser
WHERE u4."email" = '4@tongji.edu.cn'
  AND adminUser."email" = '1@tongji.edu.cn'
AND NOT EXISTS (
    SELECT 1 FROM "PrivateMessage" m
    WHERE m."receiverId" = u4."userId" AND m."senderId" = adminUser."userId" AND m."content" LIKE '我看到了你在 Oracle%'
);

-- 演示举报记录：用户4举报“小米台灯 Pro”，管理员已处理为不成立
INSERT INTO "ReportTicket" ("targetType", "targetId", "reason", "status", "createTime", "reviewTime", "result", "reporterId", "reviewerId")
SELECT 'Product', pr."productId", '商品图片信息不够清晰，申请管理员核查。', 'Rejected', SYSTIMESTAMP - INTERVAL '2' DAY, SYSTIMESTAMP - INTERVAL '2' DAY + INTERVAL '2' HOUR, '经核查暂未发现违规，建议卖家补充更多实物说明。', reporter."userId", reviewer."userId"
FROM "Product" pr, "User" reporter, "User" reviewer
WHERE pr."title" = '小米台灯 Pro'
  AND reporter."email" = '4@tongji.edu.cn'
  AND reviewer."email" = '1@tongji.edu.cn'
AND NOT EXISTS (
    SELECT 1 FROM "ReportTicket" r
    WHERE r."targetType" = 'Product' AND r."targetId" = pr."productId" AND r."reporterId" = reporter."userId"
);

-- ============================================================
-- 10. 通知中心演示数据
-- 说明：这些通知覆盖 System、Mention、Reply、Audit、Report、Transaction、Dispute、Forum、Friend、Message。
-- 每条通知都尽量指向真实的业务对象，方便给组长演示 type / targetType / targetId / link / eventKey。
-- ============================================================

-- System：发给用户4的系统公告
INSERT INTO "Notification" ("title", "content", "createTime", "type", "targetType", "targetId", "link", "isRead", "readTime", "eventKey", "userId")
SELECT '系统公告', '欢迎使用同济校园论坛。通知中心已支持类型筛选、已读管理和事件提醒。', SYSTIMESTAMP - INTERVAL '3' HOUR, 'System', 'System', NULL, NULL, '0', NULL, 'seed:notification:system:user4:welcome', u."userId"
FROM "User" u
WHERE u."email" = '4@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "Notification" WHERE "eventKey" = 'seed:notification:system:user4:welcome');

-- Mention：用户4在 Vue 教程帖中被提及
INSERT INTO "Notification" ("title", "content", "createTime", "type", "targetType", "targetId", "link", "isRead", "readTime", "eventKey", "userId")
SELECT '帖子提及', 'Moderator User 在《Vue 3 + TypeScript 项目搭建教程》中提到了你，邀请你补充前端工程化经验。', SYSTIMESTAMP - INTERVAL '2' HOUR, 'Mention', 'Post', p."postId", '/forums', '0', NULL, 'seed:notification:mention:user4:vue-post', u."userId"
FROM "User" u, "Post" p
WHERE u."email" = '4@tongji.edu.cn'
  AND p."title" = 'Vue 3 + TypeScript 项目搭建教程'
AND NOT EXISTS (SELECT 1 FROM "Notification" WHERE "eventKey" = 'seed:notification:mention:user4:vue-post');

-- Reply：用户4的 Oracle 求助帖收到新评论
INSERT INTO "Notification" ("title", "content", "createTime", "type", "targetType", "targetId", "link", "isRead", "readTime", "eventKey", "userId")
SELECT '帖子新评论', 'Admin User 回复了你的《求助：Oracle 数据库连接超时问题》，建议检查 Docker DNS 和连接池配置。', SYSTIMESTAMP - INTERVAL '90' MINUTE, 'Reply', 'Post', p."postId", '/forums', '0', NULL, 'seed:notification:reply:user4:oracle-post', u."userId"
FROM "User" u, "Post" p
WHERE u."email" = '4@tongji.edu.cn'
  AND p."title" = '求助：Oracle 数据库连接超时问题'
AND NOT EXISTS (SELECT 1 FROM "Notification" WHERE "eventKey" = 'seed:notification:reply:user4:oracle-post');

-- Audit：用户4的 Oracle 求助帖审核通过，设置为已读以便演示已读/未读筛选
INSERT INTO "Notification" ("title", "content", "createTime", "type", "targetType", "targetId", "link", "isRead", "readTime", "eventKey", "userId")
SELECT '帖子审核通过', '你的帖子《求助：Oracle 数据库连接超时问题》已通过审核，当前可在技术讨论版块正常展示。', SYSTIMESTAMP - INTERVAL '1' HOUR, 'Audit', 'Post', p."postId", '/forums', '1', SYSTIMESTAMP - INTERVAL '40' MINUTE, 'seed:notification:audit:user4:oracle-post-approved', u."userId"
FROM "User" u, "Post" p
WHERE u."email" = '4@tongji.edu.cn'
  AND p."title" = '求助：Oracle 数据库连接超时问题'
AND NOT EXISTS (SELECT 1 FROM "Notification" WHERE "eventKey" = 'seed:notification:audit:user4:oracle-post-approved');

-- Report：用户4提交的商品举报已处理
INSERT INTO "Notification" ("title", "content", "createTime", "type", "targetType", "targetId", "link", "isRead", "readTime", "eventKey", "userId")
SELECT '举报处理结果', '你提交的商品举报已处理：该商品信息经核查暂未发现违规。', SYSTIMESTAMP - INTERVAL '35' MINUTE, 'Report', 'Product', pr."productId", NULL, '0', NULL, 'seed:notification:report:user4:desk-lamp', u."userId"
FROM "User" u, "Product" pr
WHERE u."email" = '4@tongji.edu.cn'
  AND pr."title" = '小米台灯 Pro'
AND NOT EXISTS (SELECT 1 FROM "Notification" WHERE "eventKey" = 'seed:notification:report:user4:desk-lamp');

-- Transaction：用户4的历史订单已完成
INSERT INTO "Notification" ("title", "content", "createTime", "type", "targetType", "targetId", "link", "isRead", "readTime", "eventKey", "transactionId", "userId")
SELECT '交易完成', '你购买的《算法导论》第四版订单已完成，感谢使用校园二手交易。', SYSTIMESTAMP - INTERVAL '30' MINUTE, 'Transaction', 'Transaction', t."transactionId", '/products', '0', NULL, 'seed:notification:transaction:user4:algorithm-completed', t."transactionId", u."userId"
FROM "User" u, "Transaction" t, "Product" pr
WHERE u."email" = '4@tongji.edu.cn'
  AND t."userId" = u."userId"
  AND t."productId" = pr."productId"
  AND pr."title" = '《算法导论》第四版'
AND NOT EXISTS (SELECT 1 FROM "Notification" WHERE "eventKey" = 'seed:notification:transaction:user4:algorithm-completed');

-- Dispute：用户4的历史纠纷已处理完成
INSERT INTO "Notification" ("title", "content", "createTime", "type", "targetType", "targetId", "link", "isRead", "readTime", "eventKey", "transactionId", "userId")
SELECT '纠纷处理完成', '你发起的订单纠纷已由管理员处理完成，处理结果可在交易记录中查看。', SYSTIMESTAMP - INTERVAL '20' MINUTE, 'Dispute', 'Transaction', t."transactionId", '/products', '0', NULL, 'seed:notification:dispute:user4:algorithm-resolved', t."transactionId", u."userId"
FROM "User" u, "Transaction" t, "Product" pr
WHERE u."email" = '4@tongji.edu.cn'
  AND t."userId" = u."userId"
  AND t."productId" = pr."productId"
  AND pr."title" = '《算法导论》第四版'
AND NOT EXISTS (SELECT 1 FROM "Notification" WHERE "eventKey" = 'seed:notification:dispute:user4:algorithm-resolved');

-- Forum：用户1是校园生活版主
INSERT INTO "Notification" ("title", "content", "createTime", "type", "targetType", "targetId", "link", "isRead", "readTime", "eventKey", "userId")
SELECT '版主权限已分配', '你已成为“校园生活”版块版主，可以协助维护帖子和评论秩序。', SYSTIMESTAMP - INTERVAL '25' MINUTE, 'Forum', 'Forum', f."forumId", '/forums', '0', NULL, 'seed:notification:forum:user1:campus-manager', u."userId"
FROM "User" u, "Forum" f
WHERE u."email" = '1@tongji.edu.cn'
  AND f."forumName" = '校园生活'
AND NOT EXISTS (SELECT 1 FROM "Notification" WHERE "eventKey" = 'seed:notification:forum:user1:campus-manager');

-- Friend：用户4和 Admin User 的好友关系
INSERT INTO "Notification" ("title", "content", "createTime", "type", "targetType", "targetId", "link", "isRead", "readTime", "eventKey", "userId")
SELECT '好友申请已通过', 'Admin User 已通过你的好友申请，现在可以在消息中心发送私信。', SYSTIMESTAMP - INTERVAL '10' MINUTE, 'Friend', 'Friendship', f."friendshipId", '/messages', '0', NULL, 'seed:notification:friend:user4:admin-accepted', u4."userId"
FROM "FriendShip" f, "User" u4, "User" adminUser
WHERE u4."email" = '4@tongji.edu.cn'
  AND adminUser."email" = '1@tongji.edu.cn'
  AND ((f."userId" = u4."userId" AND f."friendId" = adminUser."userId") OR (f."userId" = adminUser."userId" AND f."friendId" = u4."userId"))
AND NOT EXISTS (SELECT 1 FROM "Notification" WHERE "eventKey" = 'seed:notification:friend:user4:admin-accepted');

-- Message：用户4收到一条私信
INSERT INTO "Notification" ("title", "content", "createTime", "type", "targetType", "targetId", "link", "isRead", "readTime", "eventKey", "userId")
SELECT '新的私信', '你收到了一条来自 Admin User 的新私信。', SYSTIMESTAMP - INTERVAL '5' MINUTE, 'Message', 'Message', m."messageId", '/messages', '0', NULL, 'seed:notification:message:user4:admin-message', u4."userId"
FROM "PrivateMessage" m, "User" u4, "User" adminUser
WHERE u4."email" = '4@tongji.edu.cn'
  AND adminUser."email" = '1@tongji.edu.cn'
  AND m."receiverId" = u4."userId"
  AND m."senderId" = adminUser."userId"
  AND m."content" LIKE '我看到了你在 Oracle%'
AND NOT EXISTS (SELECT 1 FROM "Notification" WHERE "eventKey" = 'seed:notification:message:user4:admin-message');

-- Admin User：系统维护和已完成交易通知，便于演示管理员自己的通知列表
INSERT INTO "Notification" ("title", "content", "createTime", "type", "targetType", "targetId", "link", "isRead", "readTime", "eventKey", "userId")
SELECT '系统维护提醒', '今晚 23:00 将进行短时系统维护，请管理员关注服务状态和用户反馈。', SYSTIMESTAMP - INTERVAL '15' MINUTE, 'System', 'System', NULL, NULL, '0', NULL, 'seed:notification:system:user1:maintenance', u."userId"
FROM "User" u
WHERE u."email" = '1@tongji.edu.cn'
AND NOT EXISTS (SELECT 1 FROM "Notification" WHERE "eventKey" = 'seed:notification:system:user1:maintenance');

INSERT INTO "Notification" ("title", "content", "createTime", "type", "targetType", "targetId", "link", "isRead", "readTime", "eventKey", "transactionId", "userId")
SELECT '交易完成', '你发布的《算法导论》第四版已完成交易，资金已结算。', SYSTIMESTAMP - INTERVAL '12' MINUTE, 'Transaction', 'Transaction', t."transactionId", '/products', '1', SYSTIMESTAMP - INTERVAL '8' MINUTE, 'seed:notification:transaction:user1:algorithm-sold', t."transactionId", seller."userId"
FROM "User" seller, "Transaction" t, "Product" pr
WHERE seller."email" = '1@tongji.edu.cn'
  AND pr."userId" = seller."userId"
  AND t."productId" = pr."productId"
  AND pr."title" = '《算法导论》第四版'
AND NOT EXISTS (SELECT 1 FROM "Notification" WHERE "eventKey" = 'seed:notification:transaction:user1:algorithm-sold');

-- Manager User：商品相关通知
INSERT INTO "Notification" ("title", "content", "createTime", "type", "targetType", "targetId", "link", "isRead", "readTime", "eventKey", "userId")
SELECT '版主权限已分配', '你已成为“二手交易”版块版主，请协助维护商品帖和交易秩序。', SYSTIMESTAMP - INTERVAL '50' MINUTE, 'Forum', 'Forum', f."forumId", '/forums', '0', NULL, 'seed:notification:forum:user1:market-manager', u."userId"
FROM "User" u, "Forum" f
WHERE u."email" = '1@tongji.edu.cn'
  AND f."forumName" = '二手交易'
AND NOT EXISTS (SELECT 1 FROM "Notification" WHERE "eventKey" = 'seed:notification:forum:user1:market-manager');

INSERT INTO "Notification" ("title", "content", "createTime", "type", "targetType", "targetId", "link", "isRead", "readTime", "eventKey", "userId")
SELECT '商品收到咨询', '有同学对你发布的“机械键盘 Cherry MX 青轴”感兴趣，建议及时查看私信。', SYSTIMESTAMP - INTERVAL '45' MINUTE, 'Message', 'Product', pr."productId", '/messages', '0', NULL, 'seed:notification:message:user2:keyboard-inquiry', u."userId"
FROM "User" u, "Product" pr
WHERE u."email" = '2@tongji.edu.cn'
  AND pr."title" = '机械键盘 Cherry MX 青轴'
AND NOT EXISTS (SELECT 1 FROM "Notification" WHERE "eventKey" = 'seed:notification:message:user2:keyboard-inquiry');

-- Moderator User：帖子互动通知
INSERT INTO "Notification" ("title", "content", "createTime", "type", "targetType", "targetId", "link", "isRead", "readTime", "eventKey", "userId")
SELECT '帖子新评论', '你的《食堂新出的菜品测评来了！》收到新的评论，大家正在讨论食堂新品。', SYSTIMESTAMP - INTERVAL '55' MINUTE, 'Reply', 'Post', p."postId", '/forums', '0', NULL, 'seed:notification:reply:user3:canteen-comment', u."userId"
FROM "User" u, "Post" p
WHERE u."email" = '3@tongji.edu.cn'
  AND p."title" = '食堂新出的菜品测评来了！'
AND NOT EXISTS (SELECT 1 FROM "Notification" WHERE "eventKey" = 'seed:notification:reply:user3:canteen-comment');

INSERT INTO "Notification" ("title", "content", "createTime", "type", "targetType", "targetId", "link", "isRead", "readTime", "eventKey", "userId")
SELECT '帖子提及', 'Admin User 在技术讨论中提到了你的 Vue 3 教程，建议你补充项目结构说明。', SYSTIMESTAMP - INTERVAL '42' MINUTE, 'Mention', 'Post', p."postId", '/forums', '1', SYSTIMESTAMP - INTERVAL '30' MINUTE, 'seed:notification:mention:user3:vue-followup', u."userId"
FROM "User" u, "Post" p
WHERE u."email" = '3@tongji.edu.cn'
  AND p."title" = 'Vue 3 + TypeScript 项目搭建教程'
AND NOT EXISTS (SELECT 1 FROM "Notification" WHERE "eventKey" = 'seed:notification:mention:user3:vue-followup');
COMMIT;
