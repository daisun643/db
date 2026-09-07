# 阿里云 ECS 生产部署

生产 Compose 使用根目录的 `docker-compose.yml`。它只启动 Nginx、ASP.NET Core API、Oracle XE 和 MinIO；前端在构建 Nginx 镜像时执行 `npm run build`，生成的 `dist` 由 Nginx 直接提供。Oracle、MinIO 和 API 没有宿主机公网端口，容器之间通过 `app-network` 通信。

## 1. 域名和 ECS 安全组

在域名 DNS 控制台添加 A 记录：

```text
主机记录：@（或 example.com 对应的主机名）
记录类型：A
记录值：ECS 公网 IPv4
```

将 `.env` 中的 `DOMAIN` 设置为实际访问的域名，例如 `example.com`。如果还需要 `www`，应为它单独配置 DNS，并在 Nginx 配置中增加对应 `server_name` 后重新构建镜像。

ECS 安全组只放行：

```text
TCP 22   SSH 管理
TCP 80   HTTP → HTTPS 跳转及证书签发
TCP 443  HTTPS 业务访问
```

不要放行 `1521`、`1522`、`8080`、`5173`、`9000` 或 `9001`。需要管理 MinIO 时使用 ECS 内部网络或临时、受限的管理通道。

## 2. 安装 Docker

以下命令适用于 Ubuntu ECS：

```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo \"$VERSION_CODENAME\") stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker "$USER"
newgrp docker
docker run --rm hello-world
docker compose version
```

## 3. 获取代码和填写环境变量

在 ECS 上执行：

```bash
git clone <你的仓库地址> forum
cd forum
cp .env.example .env
nano .env
```

必须替换这些值：

```text
DOMAIN
MINIO_ROOT_USER
MINIO_ROOT_PASSWORD
ORACLE_PASSWORD
APP_USER_PASSWORD
EMAIL_SENDER_EMAIL
EMAIL_SENDER_PASSWORD
```

密码不要使用示例值。Oracle 的密码中不要使用会破坏连接字符串的未转义分号；修改 Oracle 初始化凭据后，如果已有 `oracle_data` volume，旧用户密码不会自动重建，需要按数据库迁移策略处理现有数据。

## 4. HTTPS 证书

Nginx 读取标准 PEM 文件：

```text
nginx/ssl/fullchain.pem
nginx/ssl/privkey.pem
```

### 方式 A：已有阿里云 SSL 证书

在阿里云证书控制台下载 Nginx 证书包。将服务器证书和完整中间证书链合并或转换为 `fullchain.pem`，将私钥保存为 `privkey.pem`。不同证书包的文件名可能不同，请以下载包内说明为准，然后执行：

```bash
mkdir -p nginx/ssl
cp /path/to/fullchain.pem nginx/ssl/fullchain.pem
cp /path/to/privkey.pem nginx/ssl/privkey.pem
chmod 600 nginx/ssl/privkey.pem
```

确认 `fullchain.pem` 与 `privkey.pem` 属于同一个域名和密钥对。

### 方式 B：Let's Encrypt

最简单的流程是在部署前暂时停止占用 80 端口的服务，使用 Certbot 的 standalone 模式签发证书：

```bash
sudo apt-get update
sudo apt-get install -y certbot
sudo systemctl stop nginx 2>/dev/null || true
set -a; . ./.env; set +a
sudo certbot certonly --standalone -d "$DOMAIN"

mkdir -p nginx/ssl
sudo cp "/etc/letsencrypt/live/$DOMAIN/fullchain.pem" nginx/ssl/fullchain.pem
sudo cp "/etc/letsencrypt/live/$DOMAIN/privkey.pem" nginx/ssl/privkey.pem
sudo chown "$USER":"$USER" nginx/ssl/*.pem
chmod 600 nginx/ssl/privkey.pem
```

续期时先执行 `sudo certbot renew`，再把更新后的两个 PEM 文件复制到 `nginx/ssl/`，最后重启 Nginx 容器：

```bash
docker compose --env-file .env -f docker-compose.yml restart nginx
```

如果 ECS 上已有宿主机 Nginx 或其他程序监听 80/443，应先停用它，确保端口只由 Compose 的 Nginx 使用。

## 5. 检查并启动

证书和 `.env` 准备好后，在项目根目录执行：

```bash
docker compose --env-file .env -f docker-compose.yml config
./scripts/restart.sh --wait
```

首次启动 Oracle XE 可能需要几分钟。确认 DNS 已生效并且证书匹配后，访问：

```text
https://example.com
```

浏览器对 `https://example.com/api/...` 的请求会由 Nginx 转发到 `backend-api:8080`，API 再通过 Docker 网络访问 `oracle-db:1521` 和 `minio:9000`。HTTP 请求会被 301 跳转到 HTTPS。

## 6. 日常运维

查看状态：

```bash
docker compose --env-file .env -f docker-compose.yml ps
```

查看日志：

```bash
docker compose --env-file .env -f docker-compose.yml logs -f nginx
docker compose --env-file .env -f docker-compose.yml logs -f backend-api
docker compose --env-file .env -f docker-compose.yml logs -f oracle-db
```

重启：

```bash
./scripts/restart.sh --no-build
```

更新代码并重新构建：

```bash
git pull
./scripts/restart.sh --wait
```

不要使用 `docker compose down -v`，否则会删除 Compose 管理的 Oracle、MinIO 和 ASP.NET Data Protection volumes。当前 Compose 保留了 `oracle_data`、`minio_data` 和 `backend_data_protection` 持久化卷。
