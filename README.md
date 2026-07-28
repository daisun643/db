# 同济论坛

Vue 3 + ASP.NET Core 8 + Oracle 18c 的论坛与校园闲置交易系统，开发环境由 Docker Compose 管理。

## 快速开始

要求：Docker（含 Compose 插件）；运行接口测试还需要 `uv`。

```bash
./scripts/restart.sh --wait
```

默认保留数据库和 MinIO 数据。仅在确定需要完全重建开发数据时执行：

```bash
./scripts/restart.sh --reset-data --wait
```

访问地址：

- 前端：http://localhost:5173
- API：http://localhost:8080
- Swagger：http://localhost:8080/swagger
- MinIO 控制台：http://localhost:9001

## 常用命令

```bash
# 查看状态与日志
docker compose ps
docker compose logs -f backend-api

# 运行全部 API 测试（服务需已启动）
./scripts/test.sh

# 自动重启服务后运行论坛测试
./scripts/test.sh --restart -- tests/test_forum.py -q

# 只检查测试能否收集
./scripts/test.sh --collect-only

# 生成 BCrypt 密码摘要（交互输入，不回显明文）
uv run --with bcrypt scripts/encrypt.py
```

## 项目结构

| 路径 | 内容 |
| --- | --- |
| `backend/` | ASP.NET Core API、EF Core 模型和服务 |
| `frontend/` | Vue 3 + Vite 前端 |
| `database/` | Oracle 初始化脚本，按文件名前缀顺序执行 |
| `test/` | pytest HTTP 集成测试 |
| `scripts/` | 开发环境与测试入口 |
| `docs/` | 项目功能与开发指南 |

进一步阅读：

- [项目介绍与功能](docs/项目介绍与功能.md)
- [开发指南](docs/开发指南.md)
