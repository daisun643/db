# API 测试

测试使用 pytest + requests，通过 HTTP 验证运行中的后端；依赖由 `uv` 根据 `test/pyproject.toml` 管理。

## 运行

```bash
# 服务已启动
./scripts/test.sh

# 重启并等待服务健康后测试
./scripts/test.sh --restart

# 完全重建测试数据后测试（会删除数据卷）
./scripts/test.sh --reset-data

# 指定文件或 pytest 参数
./scripts/test.sh -- tests/test_forum.py -q
./scripts/test.sh -- -k pagination

# 不访问 API，只验证测试收集
./scripts/test.sh --collect-only
```

环境变量：

| 名称 | 默认值 | 说明 |
| --- | --- | --- |
| `BASE_URL` | `http://localhost:8080` | API 地址 |
| `TEST_TIMEOUT` | `10` | 单次请求超时（秒） |
| `UV_CACHE_DIR` | `/tmp/tongji-forum-uv-cache` | uv 缓存目录 |

## 结构

| 路径 | 职责 |
| --- | --- |
| `test/api/` | HTTP 客户端封装，不放业务断言 |
| `test/tests/` | 按模块组织的测试场景 |
| `test/conftest.py` | 登录客户端和共享 fixture |
| `test/config.py` | 地址、超时和预置账号 |
| `test/utils/` | 通用断言与辅助函数 |

## 编写约定

- 测试必须可重复执行；创建资源时使用唯一名称，不能依赖空数据库。
- 每个测试只断言自己创建或明确查询的数据，避免依赖列表第一项。
- 状态修改应在 `finally` 中恢复，或创建隔离数据。
- 分页接口同时校验响应列表、`X-Total-Count` 和页大小。
- 权限测试至少覆盖未登录、普通用户和授权角色。
- API 参数名称集中在 `test/api/` 转换，测试场景使用 Python 风格参数名。

测试是集成测试，会写入开发数据库。需要完全隔离时使用独立 Compose 项目或一次性数据卷。
