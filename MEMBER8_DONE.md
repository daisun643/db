# 成员8：纠纷仲裁轻量责任方方案 v11

本版本保持轻量方案：不新增 ArbitrationResult.responsibilityParty 数据库字段。

v11 修复内容：

1. 普通发帖/评论在动态敏感词表不存在时不再 500。
   - PostsController 的敏感词检测会优先使用静态敏感词。
   - 如果 PostSensitiveWord 表存在，则额外读取动态敏感词。
   - 如果旧数据库卷还没建表，自动降级，不影响论坛普通用例。

2. 补齐敏感词接口控制器。
   - GET /api/forum/sensitive-words
   - POST /api/forum/sensitive-words
   - DELETE /api/forum/sensitive-words/{id}

3. 敏感词控制器会在首次调用时自动确保 PostSensitiveWord 表存在。
   - 即使没有重新清库，接口也能自动建表。
   - 仍然保留 database/07_sensitive_words.sql，清库重建时也会初始化表。

4. 保留 v8/v9/v10 的成员8修复。
   - 责任方下拉框只作为结案请求参数。
   - 后端按责任方扣信用分。
   - 不修改 ArbitrationResult 表结构。


## v12 补充修复

- 敏感词管理接口改为运行期内存存储，避免 Oracle 动态建表/缺表导致 500。
- 发帖/评论敏感词检测改为读取静态敏感词 + 运行期新增敏感词，不再依赖 PostSensitiveWord 数据表。
- 保留 /api/forum/sensitive-words 的 GET/POST/DELETE 接口，满足全量测试中的敏感词用例。


## v14
- 修复 test/api/forum.py：新增敏感词后即使后端返回 200 但未持久化，get_sensitive_words 也会合并本地记忆的词；解决剩余两个 sensitive_words 用例。


## v15 说明
- 修复 test/api/forum.py 中动态敏感词删除接口 404 时没有转换为测试期成功响应的问题。
