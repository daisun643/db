# Oracle 初始化脚本

该目录挂载到 Oracle 容器的 `/container-entrypoint-initdb.d`。脚本仅在数据库数据卷首次创建时，按文件名前缀顺序执行。

| 顺序 | 文件 | 职责 |
| --- | --- | --- |
| 01 | `01_schema.sql` | 创建表、主键、外键与检查约束 |
| 02 | `02_indexes.sql` | 创建业务唯一索引与查询索引 |
| 03 | `03_rbac.sql` | 创建默认角色、权限与角色权限关系 |
| 04 | `04_demo_users.sql` | 创建本地演示账号并分配角色 |
| 05 | `05_seed_core.sql` | 写入论坛、帖子、商品等核心演示数据 |
| 06 | `06_seed_scenarios.sql` | 补充媒体、收藏、钱包、通知、订单和仲裁场景 |
| 07 | `07_seed_user_avatars.sql` | 写入演示用户头像的 MediaFile / UserAvatar 关联（幂等，需先导入 MinIO） |
| 08 | `08_triggers.sql` | 创建敏感词函数与 11 个触发器（内容审核、点赞计数、默认钱包、创建者版主、已读时间、好友时间、留言归档、库存锁定） |
| 09 | `09_procedures.sql` | 创建钱包兜底函数与 8 个存储过程（信用调整、充值、浏览量、下单、支付、确认收货、取消、纠纷结案） |
| 10 | `10_views.sql` | 创建资金流水视图 `V_FinanceFlow`，固化收入与支出口径 |
| 11 | `11_reassign_seed_publishers.sql` | 将已存在的演示帖子和商品统一归属到 `2@tongji.edu.cn` |

08~10 是 PL/SQL 对象，必须排在种子数据之后执行，避免触发器改写演示数据中已有的汇总值。各对象的职责、出参约定与错误码见 [`docs/数据库触发器与存储过程.md`](../docs/数据库触发器与存储过程.md)。

## 维护约定

- 表结构、EF Core 实体和 `AppDbContext` 映射必须同步修改。
- 新脚本使用连续的两位数字前缀，依赖其他数据的脚本排在其后。
- 初始化脚本面向全新数据卷；已执行过的脚本不会在容器重启时自动重跑。
- 已部署数据库的变更应使用独立、可审查且尽可能幂等的迁移脚本，不要依赖重新执行初始化脚本。
- 图片二进制由 MinIO/S3 保存，`MediaFile` 只保存元数据和 URL；帖子、商品与头像通过关联表引用媒体。
- 外部演示图片使用 `storageProvider = 'external'`，仅保存 URL，不写入 MinIO。
- SQL*Plus 中包含 `&` 的 URL 时，脚本必须先执行 `SET DEFINE OFF`；PL/SQL 块内有空行时需要 `SET SQLBLANKLINES ON`。
- 08~10 的对象名都带双引号创建，因此大小写与脚本一致：触发器为 `TRG_Xxx`，过程与函数为全小写的 `sp_xxx`、`fn_xxx`。C# 调用时过程名必须同样加引号，否则会被 Oracle 大写后找不到对象。
- 存储过程不执行 `COMMIT`，只用 `SAVEPOINT` 与 `ROLLBACK TO` 做局部回滚，事务边界交给调用方。
- 触发器改写的字段 EF Core 不感知，接口返回前需要 `ReloadAsync()` 或重新查询。
- 每个 PL/SQL 脚本末尾都有校验块，对象无效或数量不足时用 `RAISE_APPLICATION_ERROR` 让初始化直接失败；新增对象后同步调整数量阈值。
- 校验块查 `all_objects`、`all_triggers`、`all_procedures` 并按 `owner = 'APPUSER'` 过滤。本目录的脚本以 SYS 连接执行，对象属于 APPUSER，`user_*` 视图看不到它们。

重建数据卷会删除本地 Oracle 和 MinIO 数据。仅在确认数据可丢弃后，按开发指南执行重置操作。
