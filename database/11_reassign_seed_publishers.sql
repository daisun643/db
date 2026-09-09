-- ============================================================
-- 11_reassign_seed_publishers.sql
-- 将已存在的演示帖子和商品统一归属到 2@tongji.edu.cn。
-- 该脚本用于兼容已经执行过旧版种子脚本的数据卷；对全新数据卷
-- 05/06 中的插入语句本身也已使用同一发布方。
-- ============================================================

ALTER SESSION SET CONTAINER = XEPDB1;
ALTER SESSION SET CURRENT_SCHEMA = APPUSER;

UPDATE "Post"
SET "userId" = (SELECT "userId" FROM "User" WHERE "email" = '2@tongji.edu.cn')
WHERE "title" IN (
    '图书馆自习攻略：哪个楼层人最少？',
    '食堂新出的菜品测评来了！',
    '社团招新季到了，大家推荐几个社团',
    'C# 异步编程踩坑记录',
    '求助：Oracle 数据库连接超时问题',
    'Vue 3 + TypeScript 项目搭建教程',
    '毕业清仓：教材、考研资料、电子设备',
    '求购：二手显示器，24寸以上',
    '本周五四平路校区夜跑活动报名',
    '高等数学期末复习资料索引',
    '暑期实习简历互助修改',
    '校园二手交易安全提醒'
);

UPDATE "Product"
SET "userId" = (SELECT "userId" FROM "User" WHERE "email" = '2@tongji.edu.cn')
WHERE "title" IN (
    '机械键盘 Cherry MX 青轴',
    '《算法导论》第四版',
    '二手自行车 捷安特 ATX',
    '考研政治全套资料',
    '小米台灯 Pro',
    '罗技 G502 鼠标',
    '宜家 LERSTA 落地灯',
    '羽毛球拍双拍套装',
    'USB-C 七合一扩展坞'
);

COMMIT;
