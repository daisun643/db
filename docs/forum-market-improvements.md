# forum/market 功能完善记录

## 目标
- 按照 PR 变更目标补齐 `feat/forum` 与 `feat/market` 的列表查询、分页、排序与可用性。
- 形成可单独提交的 PR 并避免与 `master` 直接修改。

## 已完成（feat/forum）

### 1) 帖子列表增强
- `GET /api/posts` 增加查询参数：
  - `tags`（逗号分隔）
  - `tagOp`（`and`/`or`）
  - `from` / `to`（创建时间范围）
  - `minHeat` / `maxHeat`
  - `page` / `pageSize`
  - `sort`（保留 `latest` / `hot`）
- 参数校验：排序、标签组合方式、状态、热度范围、时间范围非法时返回 `400`。
- 查询返回 `X-Total-Count`。
- 状态参数支持大小写兼容、映射到标准状态值。
- 热度排序/热度区间会统一在刷新后的热度分值上计算，避免排序与过滤不一致。

### 2) 标签统计接口
- 增加 `GET /api/tags/stats`。
- DTO 增补 `TagStatsResponse`，支持热门标签列表展示。

### 3) 前端接入
- `test/api/forum.py` 增加 `get_tag_stats`。
- `frontend/src/api/index.js` 增加 `getTagStats`。
- `ForumsView` 使用 `X-Total-Count` 刷新分页显示。
- 发布成功后清理富文本表单状态。

### 4) 测试补齐（forum）
- 支持多标签 `and` / `or` 筛选、热度范围筛选、状态大小写兼容、分页总量头字段校验。
- 覆盖非法排序、非法标签组合、倒置时间/热度区间和负数热度边界。
- 标签统计接口校验返回结构、正数计数和降序排列。
- 标签统计直接在数据库中关联公开帖子，不预加载帖子 ID，避免大数据量下的 SQL 参数上限问题。

## 已完成（feat/market）

### 1) 商品列表增强
- `GET /api/products` 增加参数：
  - 关键字与多字段匹配：`keyword`
  - 分类：`category`
  - 成色：`condition`
  - 价格区间：`minPrice` / `maxPrice`
  - 库存区间：`minStock` / `maxStock`
  - 发布时间范围：`from` / `to`
  - 排序：`latest` / `price-asc` / `price-desc` / `stock-asc` / `stock-desc`
  - `page` / `pageSize`
  - `status`（大小写兼容）
- 合法性校验：排序、状态、时间区间、价格区间、库存区间。
- 返回 `X-Total-Count`。

### 2) 订单列表增强
- `GET /api/transactions/me`、`GET /api/transactions/sales` 增加：
  - `status`（大小写兼容）
  - `page` / `pageSize`
- 返回 `X-Total-Count`。

### 3) 前端市场页布局与交互
- 商品列表页新增筛选 toolbar（关键字、状态、分类、排序）和分页。
- 订单/卖出订单支持分页。
- 交易页错误路径做基础兜底（支付/取消/收货/举报/消息）。

### 4) 测试补齐（market）
- 商品列表：关键字、分类、成色、价格/库存/发布时间区间、排序、状态大小写兼容、`x-total-count`。
- 覆盖非法排序、倒置区间和负数价格/库存边界。
- 订单列表：买入/卖出状态大小写兼容、非法状态、分页窗口与 `x-total-count`。
- API 层完整透传筛选参数，包括 `from` / `to`。
- 商品和订单排序增加主键兜底，在时间相同的情况下仍能稳定分页。

## 待确认 / 有疑问

### forum
1. `tag` 与 `tags` 参数共存时采用并/或语义，当前 `toLower` 查询对数据库索引友好度不足；若帖子规模上升建议预聚合或维护倒排索引。
2. 热度过滤在内存计算后分页，需要确认后续数据量预期；高并发下需加缓存策略。

### market
1. 商品列表的关键词是字符串 `Contains`，目前为数据库内匹配并区分大小写；若数据大表建议加入 `FullText` 或业务侧关键词索引。
2. `price`/`stock` 排序在数据库层使用 `PublishTime`、主键作为稳定排序键；数据规模上升后可结合执行计划补充复合索引。
3. `X-Total-Count` 与分页窗口对复杂筛选下的读一致性取决于单次请求下 SQL 语句一致性；建议后续如出现抖动再考虑快照或缓存。

## 分支合并说明

- `master` 已删除旧的 `docs/模块功能` 文档目录；功能分支不再修改或恢复该目录，避免合并时产生 delete/modify 冲突。
- forum/market 的增量接口、测试覆盖与实现注意事项统一维护在本文档中。
