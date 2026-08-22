<template>
  <div class="tab-content user-products-panel">
    <div v-if="loading" class="loading">加载中...</div>
    <template v-else>
      <div v-if="products.length === 0" class="empty-state">
        <p>暂无发布的商品</p>
      </div>
      <div v-else class="product-grid">
        <article v-for="product in products" :key="product.productID" class="product-card">
          <div class="product-cover">
            <img v-if="product.imageUrls?.length" :src="product.imageUrls[0]" :alt="product.title" loading="lazy" />
            <span v-else class="cover-placeholder">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true">
                <path d="M3 3h2l2.4 11.2a2 2 0 0 0 2 1.6h7.8a2 2 0 0 0 2-1.6L21 7H6" />
                <circle cx="10" cy="20" r="1" /><circle cx="18" cy="20" r="1" />
              </svg>
            </span>
          </div>
          <div class="product-body">
            <div class="product-head">
              <h3>{{ product.title }}</h3>
              <span :class="['badge', product.status === 'Active' ? 'badge-green' : 'badge-yellow']">
                {{ statusText(product.status) }}
              </span>
            </div>
            <p class="product-desc">{{ product.description || '暂无描述' }}</p>
            <div class="product-meta">
              <strong>¥{{ product.price }}</strong>
              <span>库存 {{ product.stock }}</span>
              <span>{{ product.category || '其他' }}</span>
              <span>{{ product.condition || '良好' }}</span>
            </div>
            <time class="product-time">{{ formatDate(product.publishTime) }}</time>
          </div>
        </article>
      </div>
      <div v-if="totalPages > 1" class="panel-pagination">
        <button class="btn" :disabled="page <= 1" @click="changePage(page - 1)">上一页</button>
        <span class="muted">第 {{ page }} / {{ totalPages }} 页 · 共 {{ totalProducts }} 件</span>
        <button class="btn" :disabled="page >= totalPages" @click="changePage(page + 1)">下一页</button>
      </div>
    </template>
  </div>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { getProducts } from '../../api'

const props = defineProps({
  userId: { type: Number, required: true },
})

const emit = defineEmits(['error'])

const products = ref([])
const loading = ref(false)
const totalProducts = ref(0)
const page = ref(1)
const pageSize = 12
const totalPages = computed(() => Math.max(1, Math.ceil(totalProducts.value / pageSize)))

const statusText = (status) => ({
  Active: '发布中',
  Locked: '已锁定',
  Sold: '已售出',
  Inactive: '已下架',
}[status] || status || '未知')

const formatDate = (value) => {
  if (!value) return ''
  return new Date(value).toLocaleDateString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  })
}

const loadProducts = async () => {
  try {
    loading.value = true
    const res = await getProducts({ sellerId: props.userId, page: page.value, pageSize })
    products.value = Array.isArray(res.data) ? res.data : []
    const totalFromHeader = Number(res.headers?.['x-total-count'])
    totalProducts.value = Number.isFinite(totalFromHeader) ? totalFromHeader : products.value.length
  } catch (e) {
    products.value = []
    totalProducts.value = 0
    emit('error', '无法加载 TA 的商品: ' + (e.response?.data?.message || e.message))
  } finally {
    loading.value = false
  }
}

const changePage = (next) => {
  page.value = next
  loadProducts()
}

watch(() => props.userId, () => {
  page.value = 1
  loadProducts()
})

onMounted(loadProducts)

defineExpose({ reload: loadProducts })
</script>

<style scoped>
.user-products-panel { display: grid; gap: 1rem; }
.product-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: .9rem; }
.product-card { overflow: hidden; border: 1px solid var(--border); border-radius: 16px; background: var(--surface); transition: transform .2s, box-shadow .2s; }
.product-card:hover { transform: translateY(-2px); box-shadow: 0 10px 26px rgba(29, 35, 58, .08); }
.product-cover { height: 140px; background: #f2f2f5; }
.product-cover img { width: 100%; height: 100%; object-fit: cover; }
.cover-placeholder { width: 100%; height: 100%; display: grid; place-items: center; color: #b6bac6; }
.cover-placeholder svg { width: 34px; height: 34px; }
.product-body { padding: .85rem .95rem 1rem; }
.product-head { display: flex; align-items: center; justify-content: space-between; gap: .5rem; }
.product-head h3 { margin: 0; font-size: .95rem; color: #1d2438; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.product-desc { margin: .4rem 0 0; color: var(--text-secondary); font-size: .8rem; line-height: 1.5; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; min-height: 2.3em; }
.product-meta { display: flex; flex-wrap: wrap; align-items: center; gap: .6rem; margin-top: .6rem; color: #8a90a1; font-size: .74rem; }
.product-meta strong { color: #e0485c; font-size: .95rem; }
.product-time { display: block; margin-top: .5rem; color: #a3a8b5; font-size: .7rem; }
.panel-pagination { display: flex; align-items: center; justify-content: center; gap: .9rem; }
.muted { color: var(--text-secondary); font-size: .8rem; }
.empty-state { padding: 2.5rem 0; text-align: center; color: var(--text-secondary); }
</style>
