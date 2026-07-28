# 运行与数据更新

## 日常更新

拉取代码后重建应用并保留数据：

```bash
./scripts/restart.sh --wait
```

只重建某个服务：

```bash
docker compose build backend-api
docker compose up -d backend-api
```

## 完全重建开发数据

数据库初始化脚本只在 Oracle 数据卷首次创建时自动执行。表结构或种子数据必须从头初始化时，显式删除数据卷：

```bash
./scripts/restart.sh --reset-data --wait
```

该命令会删除 Oracle、MinIO 和前端依赖卷，数据不可恢复。不要在生产环境使用。

## 检查状态

```bash
docker compose ps
docker compose logs oracle-db
docker compose logs backend-api
```

健康标准：

- `oracle-db` 为 `healthy`；
- `backend-api` 已监听 8080；
- `GET http://localhost:8080/api/health` 返回成功。

## 数据库脚本约定

- 脚本位于 `database/`，文件名前缀决定首次初始化顺序。
- 已部署环境的变更应新增幂等迁移脚本，不要改写已经执行过的脚本来假设其会自动重跑。
- 开发环境可用 `--reset-data` 验证全量初始化。
- 提交前至少验证后端构建、前端构建和相关 API 测试。

## 常见问题

- Oracle 长时间不健康：运行 `docker compose logs oracle-db`，检查初始化 SQL。
- 后端连接失败：确认 Oracle 健康状态及 `ConnectionStrings__Oracle`。
- 代码更新未生效：重新运行 `docker compose build <service>` 后再启动服务。
- 端口冲突：检查本机的 5173、8080、9000、9001、1522 端口。
