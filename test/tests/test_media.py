def make_png_bytes() -> bytes:
    # Minimal PNG header with payload; sufficient for service-side magic validation
    return b"\x89PNG\r\n\x1a\n" + b"\x00" * 16


def make_invalid_png_bytes() -> bytes:
    # Valid content type but invalid magic to触发签名校验失败
    return b"not-a-png-image"


def make_upload_payload(filenames: list[str], file_bytes: list[bytes] | None = None, content_type: str = "image/png"):
    payload = []
    content_list = file_bytes or [make_png_bytes() for _ in range(len(filenames))]
    for filename, data in zip(filenames, content_list):
        payload.append(("files", (filename, data, content_type)))
    return payload


def upload_images(client, bucket: str, count: int = 1, suffix: str = "png", content_type: str = "image/png", file_bytes: list[bytes] | None = None):
    filenames = [f"test-{i}.{suffix}" for i in range(count)]
    payload = make_upload_payload(filenames, file_bytes=file_bytes, content_type=content_type)
    resp = client.post("/api/media/images", params={"bucket": bucket}, files=payload)
    assert resp.status_code == 200
    body = resp.json()
    assert "urls" in body
    assert isinstance(body["urls"], list)
    return body["urls"]


def test_media_upload_and_create_post_with_uploaded_images(forum_client):
    forum_id = forum_client.get_forums().json()[0]["forumID"]
    urls = upload_images(forum_client, "posts", 1)
    assert len(urls) == 1
    assert urls[0].startswith("/uploads/posts/")

    create_resp = forum_client.create_post(
        forum_id,
        "媒体链路帖子",
        "图片上传后发帖",
        image_urls=urls,
    )
    assert create_resp.status_code == 201
    created_post = create_resp.json()
    assert created_post["imageUrls"] == urls

    detail = forum_client.get_post(created_post["postID"])
    assert detail.status_code == 200
    assert detail.json()["imageUrls"] == urls

    assert forum_client.delete_post(created_post["postID"]).status_code == 200


def test_media_upload_and_create_product_with_uploaded_images(market_client):
    urls = upload_images(market_client, "products", 1)
    assert len(urls) == 1
    assert urls[0].startswith("/uploads/products/")

    create_resp = market_client.create_product(
        "媒体链路商品",
        12.34,
        stock=2,
        image_urls=urls,
    )
    assert create_resp.status_code == 201
    created_product = create_resp.json()
    assert created_product["imageUrls"] == urls

    product_id = created_product["productID"]
    assert market_client.get_product(product_id).status_code == 200
    assert market_client.delete_product(product_id).status_code == 200


def test_media_upload_rejects_invalid_bucket(forum_client):
    payload = make_upload_payload(["bad.png"], [make_png_bytes()])
    resp = forum_client.post("/api/media/images", params={"bucket": "invalid"}, files=payload)
    assert resp.status_code == 400
    assert resp.json()["message"] == "仅支持 posts、products、avatars 的上传目录"


def test_media_upload_rejects_invalid_signature(forum_client):
    payload = make_upload_payload(["invalid.png"], [make_invalid_png_bytes()])
    resp = forum_client.post("/api/media/images", params={"bucket": "posts"}, files=payload)
    assert resp.status_code == 400
    assert resp.json()["message"] == "图片文件格式不正确"


def test_media_upload_rejects_invalid_mime_type(forum_client):
    # 上传后端通过文件头校验 + mime 白名单
    payload = make_upload_payload(
        ["bad.txt"],
        [b"hello world"],
        content_type="text/plain",
    )
    resp = forum_client.post("/api/media/images", params={"bucket": "posts"}, files=payload)
    assert resp.status_code == 400
    assert resp.json()["message"] == "仅支持 JPG、PNG、GIF、WebP 图片"


def test_media_upload_rejects_oversize_file(forum_client):
    # 5MB+1 的图片应被拒绝（服务端有严格上限）
    resp = forum_client.post(
        "/api/media/images",
        params={"bucket": "posts"},
        files=[("files", ("big.png", b"\x89PNG\r\n\x1a\n" + b"\x00" * (5 * 1024 * 1024), "image/png"))],
    )
    assert resp.status_code == 400
    assert resp.json()["message"] == "图片文件不能超过5MB"


def test_media_upload_rejects_excessive_count(forum_client):
    payload = [
        ("files", (f"img{i}.png", make_png_bytes(), "image/png"))
        for i in range(7)
    ]
    resp = forum_client.post("/api/media/images", params={"bucket": "posts"}, files=payload)
    assert resp.status_code == 400


def test_media_upload_requires_auth(client):
    resp = client.post("/api/media/images", params={"bucket": "posts"}, files=[
        ("files", ("noauth.png", make_png_bytes(), "image/png"))
    ])
    assert resp.status_code == 401


def test_media_upload_and_update_post_images(forum_client):
    forum_id = forum_client.get_forums().json()[0]["forumID"]
    initial_urls = upload_images(forum_client, "posts", 1, suffix="old")
    assert initial_urls[0].startswith("/uploads/posts/")

    create_resp = forum_client.create_post(forum_id, "图片替换测试", "先上传1张图", image_urls=initial_urls)
    assert create_resp.status_code == 201
    post_id = create_resp.json()["postID"]

    next_urls = upload_images(forum_client, "posts", 1, suffix="new")
    update_resp = forum_client.update_post(post_id, "图片替换测试", "更新为新图", image_urls=next_urls)
    assert update_resp.status_code == 200
    updated = update_resp.json()
    assert updated["imageUrls"] == next_urls

    detail = forum_client.get_post(post_id).json()
    assert detail["imageUrls"] == next_urls

    assert forum_client.delete_post(post_id).status_code == 200


def test_media_upload_and_update_product_images(market_client):
    initial_urls = upload_images(market_client, "products", 1, suffix="old")
    assert initial_urls[0].startswith("/uploads/products/")

    create_resp = market_client.create_product("图片替换测试商品", 88.88, stock=1, image_urls=initial_urls)
    assert create_resp.status_code == 201
    product_id = create_resp.json()["productID"]

    next_urls = upload_images(market_client, "products", 1, suffix="new")
    update_resp = market_client.update_product(product_id, "图片替换测试商品", 88.88, image_urls=next_urls, stock=1)
    assert update_resp.status_code == 200
    updated = update_resp.json()
    assert updated["imageUrls"] == next_urls

    detail = market_client.get_product(product_id).json()
    assert detail["imageUrls"] == next_urls

    assert market_client.delete_product(product_id).status_code == 200
