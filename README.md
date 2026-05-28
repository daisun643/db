# 同济论坛 - 全栈开发环境

基于 Docker Compose 的全栈开发环境，包含 Vue 3 前端、ASP.NET 8 后端和 Oracle 18c 数据库。

## 快速启动

```bash
bash ./scripts/restrat.sh
```

## 访问地址

- **前端**: http://localhost:5173
- **后端 API**: http://localhost:8080
- **Swagger 文档**: http://localhost:8080/swagger

## 技术栈

| 层级 | 技术 | 端口 |
|------|------|------|
| 前端 | Vue 3 + Vite + Vue Router + Pinia | 5173 |
| 后端 | ASP.NET 8 + EF Core + BCrypt + MailKit | 8080 |
| 数据库 | Oracle 18c XE | 1521 |