# 模块功能总览

本目录按小组 10 个负责人拆分模块功能。每个模块文档用于说明功能范围、开发落点、测试要求和验收标准。

## 模块列表

| 成员 | 模块 | 建议分支 | 文档 |
|---|---|---|---|
| 成员 1 | 用户、账号与后台权限 | `feature/user-auth-rbac` | `01_用户账号与后台权限.md` |
| 成员 2 | 社交关系与私信 | `feature/social-friends-messages` | `02_社交关系与私信.md` |
| 成员 3 | 系统通知与事件推送 | `feature/social-notifications` | `03_系统通知与事件推送.md` |
| 成员 4 | 论坛帖子与评论 | `feature/forum-posts-comments` | `04_论坛帖子与评论.md` |
| 成员 5 | 论坛治理与检索 | `feature/forum-governance-search` | `05_论坛治理与检索.md` |
| 成员 6 | 商品发布与库存控制 | `feature/market-products-stock` | `06_商品发布与库存控制.md` |
| 成员 7 | 订单交易与钱包 | `feature/market-orders-wallet` | `07_订单交易与钱包.md` |
| 成员 8 | 纠纷仲裁 | `feature/market-disputes` | `08_纠纷仲裁.md` |
| 成员 9 | 举报与审核闭环 | `feature/reports-audit` | `09_举报与审核闭环.md` |
| 成员 10 | 部署、集成与验收 | `feature/deploy-acceptance` | `10_部署集成与验收.md` |

## 分支建议

- `main`：稳定版本，只放最终可验收代码。
- `dev`：集成分支，各模块通过测试后合并到这里。
- `feature/*`：个人功能分支，每个成员只维护自己的模块。
- `fix/*`：联调阶段的问题修复分支。
- `test/*`：只调整测试或验收用例的分支。

## 统一要求

1. 每个模块至少完成数据库、后端接口、前端入口和接口测试。
2. 涉及权限的接口必须校验登录用户身份和角色权限。
3. 涉及信用分的业务必须记录信用分变化原因。
4. 涉及状态流转的业务必须限制非法状态变更。
5. 测试至少包含正常流程、异常流程和权限限制。
6. 最终以 Docker Compose 环境运行结果为准。
