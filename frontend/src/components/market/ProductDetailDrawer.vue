<template>
  <div v-if="open" class="detail-backdrop" @click.self="$emit('close')">
    <section class="product-detail-panel">
      <div class="detail-header">
        <button class="link-button" @click="$emit('close')">返回列表</button>
        <span v-if="product" class="muted">{{ statusText(product.status) }}</span>
      </div>

      <div v-if="loading" class="loading">加载中...</div>
      <template v-else-if="product">
        <article class="product-detail">
          <div class="product-head">
            <h2>{{ product.title }}</h2>
            <strong>¥{{ product.price }}</strong>
          </div>
          <div v-if="product.imageUrls?.length" class="detail-images">
            <img v-for="url in product.imageUrls" :key="url" :src="url" alt="" loading="lazy" />
          </div>
          <p>{{ product.description || '暂无描述' }}</p>
          <div class="product-meta">
            <span>库存 {{ product.stock }}</span>
            <span>{{ product.category || '其他' }}</span>
            <span>{{ product.condition || '良好' }}</span>
            <span
              class="seller-link"
              :title="product.userID ? '查看卖家主页' : ''"
              @click="openSellerHome(product)"
            >卖家 {{ product.sellerName || '匿名卖家' }}</span>
            <span>{{ formatDate(product.publishTime) }}</span>
          </div>
          <div class="row-actions">
            <button
              class="btn btn-primary"
              :disabled="product.status !== 'Active' || product.stock <= 0"
              @click="$emit('order', product)"
            >
              下单锁定
            </button>
            <button class="btn" @click="$emit('report', product)">举报商品</button>
          </div>
        </article>
      </template>
    </section>
  </div>
</template>

<script setup>
import { formatDate, statusText, useMarket } from '../../composables/useMarket'

defineProps({
  open: { type: Boolean, default: false },
  product: { type: Object, default: null },
  loading: { type: Boolean, default: false },
})

defineEmits(['close', 'order', 'report'])

const { openSellerHome } = useMarket()
</script>

<style scoped>
.detail-backdrop {
  background: rgba(15, 23, 42, 0.36);
  bottom: 0;
  display: flex;
  justify-content: flex-end;
  left: 0;
  position: fixed;
  right: 0;
  top: 0;
  z-index: 1200;
}

.product-detail-panel {
  background: var(--bg);
  border-left: 1px solid var(--border);
  box-shadow: -12px 0 30px rgba(15, 23, 42, 0.16);
  height: 100vh;
  max-width: 760px;
  overflow-y: auto;
  padding: 1rem;
  width: min(760px, 100vw);
}

.detail-header,
.product-detail {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1rem;
}

.detail-header {
  align-items: center;
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1rem;
}

.product-detail {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.product-detail h2 {
  font-size: 1.25rem;
}

.detail-images {
  display: grid;
  gap: 0.75rem;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
}

.detail-images img {
  aspect-ratio: 16 / 10;
  background: var(--bg);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  object-fit: cover;
  width: 100%;
}

.product-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.75rem;
}

.product-head h2 {
  font-size: 1rem;
}

.product-meta,
.row-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.product-meta {
  color: var(--text-secondary);
  font-size: 0.875rem;
}

.product-meta strong {
  color: var(--primary);
  font-size: 1.125rem;
}

.link-button {
  border: none;
  background: transparent;
  color: var(--primary);
  cursor: pointer;
  font: inherit;
  text-align: left;
}

.muted {
  color: var(--text-secondary);
  font-size: 0.875rem;
}

@media (max-width: 760px) {
  .detail-backdrop {
    display: block;
  }

  .product-detail-panel {
    border-left: none;
    width: 100vw;
  }

  .detail-header {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>

<style scoped>
/* Marketplace theme */
.detail-backdrop { background: rgba(15,18,38,.55); backdrop-filter: blur(5px); }
.product-detail-panel { padding: 1.25rem; border-left: 0; background: #f6f6fb; box-shadow: -24px 0 60px rgba(12,16,35,.24); }
.detail-header, .product-detail { border: 1px solid rgba(25,34,59,.08); border-radius: 20px; box-shadow: 0 10px 30px rgba(29,35,58,.05); }
.product-detail .product-head strong { color: #5d4dd7; font-size: 1.6rem; }

.product-head h2 { font-size: 1.05rem; letter-spacing: -.02em; }
.product-meta { gap: .45rem; }
.product-meta span { padding: .28rem .5rem; border-radius: 999px; background: #f4f5f8; font-size: .7rem; }
.product-meta .seller-link { color: #5d4fd5; cursor: pointer; }
.product-meta .seller-link:hover { text-decoration: underline; }
.product-meta strong { width: 100%; letter-spacing: -.04em; }

.row-actions .btn { border-radius: 10px; }

@media (max-width: 640px) {
  .row-actions { display: grid; grid-template-columns: repeat(2, 1fr); }
  .row-actions .btn { width: 100%; }
}
</style>
