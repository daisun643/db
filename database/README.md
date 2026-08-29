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
| 08 | `08_migrate_forum_manager_role.sql` | ForumManager 增加 role 列，存量数据补齐版主角色（幂等，可单独在已部署库执行） |

## 维护约定

- 表结构、EF Core 实体和 `AppDbContext` 映射必须同步修改。
- 新脚本使用连续的两位数字前缀，依赖其他数据的脚本排在其后。
- 初始化脚本面向全新数据卷；已执行过的脚本不会在容器重启时自动重跑。
- 已部署数据库的变更应使用独立、可审查且尽可能幂等的迁移脚本，不要依赖重新执行初始化脚本。
- 图片二进制由 MinIO/S3 保存，`MediaFile` 只保存元数据和 URL；帖子、商品与头像通过关联表引用媒体。
- 外部演示图片使用 `storageProvider = 'external'`，仅保存 URL，不写入 MinIO。
- SQL*Plus 中包含 `&` 的 URL 时，脚本必须先执行 `SET DEFINE OFF`。

重建数据卷会删除本地 Oracle 和 MinIO 数据。仅在确认数据可丢弃后，按开发指南执行重置操作。
