# API 测试架构

## 概述

基于 Python + pytest + requests 的 API 集成测试框架，通过 HTTP 请求直接测试运行中的后端服务。

使用 [uv](https://docs.astral.sh/uv/) 作为包管理器。

## 目录结构

```
test/
├── pyproject.toml       # 项目配置与依赖（uv 管理）
├── conftest.py          # pytest 全局 fixture
├── config.py            # 环境配置（URL、超时、测试账号）
├── api/                 # API 客户端层
│   ├── base.py          # BaseAPIClient — session 管理、通用请求
│   ├── auth.py          # AuthAPI — 认证相关接口
│   ├── user.py          # UserAPI（待扩展）
│   └── ...              # 其他模块按需添加
├── tests/               # 测试用例，按模块组织
│   ├── test_login.py    # 登录测试
│   └── ...              # 其他模块测试
└── utils/               # 工具函数
    └── assertions.py    # 通用断言（assert_success / assert_failure）
```

## 核心设计

### BaseAPIClient

所有 API 模块的基类，封装 `requests.Session`：
- 统一管理 `base_url` 和超时
- Session 自动持久化 Cookie（登录后请求自动携带认证 Cookie）
- 子类只需关注具体接口的参数构造

### API 模块

每个后端模块对应一个 API 客户端类（如 `AuthAPI`），继承 `BaseAPIClient`，封装该模块的所有 HTTP 调用。新增模块时只需添加 `api/xxx.py`。

### Fixture

`conftest.py` 提供可复用的 fixture：

| fixture | 说明 |
|---------|------|
| `client` | 未登录的 `AuthAPI` 实例 |
| `admin_client` | 已以 Admin 身份登录的实例 |
| `user_client` | 已以普通 User 身份登录的实例 |

测试函数通过参数名即可获取对应 fixture。

### 配置

通过环境变量切换测试目标：

| 变量 | 默认值 | 说明 |
|------|--------|------|
| `BASE_URL` | `http://localhost:8080` | 后端地址 |
| `TEST_TIMEOUT` | `10` | 请求超时（秒） |

## 运行
在服务启动的情况下：
```bash
bash ./scripts/test.sh
```

## 扩展指南

1. **新增 API 模块**：在 `api/` 下创建新文件，继承 `BaseAPIClient`，实现对应接口方法
2. **新增测试**：在 `tests/` 下创建 `test_xxx.py`，使用 fixture 获取 API 客户端
3. **新增 fixture**：在 `conftest.py` 中添加，如需要特定角色或状态的 session
4. **新增预设用户**：在 `config.py` 的 `TEST_USERS` 中添加
