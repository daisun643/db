# 同济论坛 - 全栈开发环境

基于 Docker Compose 的全栈开发环境，包含 Vue 3 前端、ASP.NET 8 后端和 Oracle 18c 数据库。

## 快速启动

```bash
bash ./scripts/restrat.sh
```

## 访问地址

- **HTTPS 入口**: https://localhost:8443
- **后端 API**: https://localhost:8443/api
- **Swagger 文档**: https://localhost:8443/swagger

首次访问会使用 Caddy 本地开发证书。浏览器如果提示证书不受信任，可以临时信任该证书；团队开发建议改用 `mkcert` 生成并信任本地 CA。

## 技术栈

| 层级 | 技术 | 端口 |
|------|------|------|
| HTTPS 代理 | Caddy | 8443 |
| 前端 | Vue 3 + Vite + Vue Router + Pinia | 5173 |
| 后端 | ASP.NET 8 + EF Core + BCrypt + MailKit | 8080 |
| 数据库 | Oracle 18c XE | 1521 |
