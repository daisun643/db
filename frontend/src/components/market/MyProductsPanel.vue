<template>
  <div class="tab-content">
    <div v-if="loadingMine" class="loading">加载中...</div>
    <div v-else class="product-list">
      <article v-for="product in myProducts" :key="product.productID" class="product-row">
        <div>
          <h2>{{ product.title }}</h2>
          <p>{{ product.description || '暂无描述' }}</p>
          <div class="product-meta">
            <strong>¥{{ product.price }}</strong>
            <span>库存 {{ product.stock }}</span>
            <span>{{ statusText(product.status) }}</span>
          </div>
        </div>
        <div class="row-actions">
          <button class="btn" :disabled="!canEditProduct(product)" @click="$emit('edit', product)">编辑</button>
          <button class="btn" :disabled="!canRestoreProduct(product)" @click="handleProductStatus(product, 'restore')">上架</button>
          <button class="btn" :disabled="!canOffShelfProduct(product)" @click="handleProductStatus(product, 'off-shelf')">下架</button>
          <button class="btn" @click="loadSales">刷新订单</button>
        </div>
      </article>
      <div v-if="myProducts.length === 0" class="empty-state">
        <p>暂无发布的商品</p>
      </div>
    </div>

    <section class="orders-panel">
      <h2>卖出订单</h2>
      <div v-if="loadingSales" class="loading">加载中...</div>
      <article v-for="order in sales" :key="order.transactionID" class="order-row">
        <span>#{{ order.transactionID }}</span>
        <span>{{ order.productTitle }}</span>
        <span>买家 {{ order.buyerName || '-' }}</span>
        <strong>¥{{ order.transactionAmount }}</strong>
        <span>{{ orderStatusText(order.transactionStatus) }}</span>
        <button class="btn" :disabled="!canDispute(order)" @click="$emit('dispute', order)">纠纷</button>
        <button class="btn" @click="$emit('messages', order)">留言板</button>
      </article>
      <div v-if="sales.length === 0" class="empty-state compact">
        <p>暂无卖出订单</p>
      </div>
      <section v-if="salesTotalPages > 1" class="market-pagination">
        <button class="btn" :disabled="salesPage <= 1" @click="handleSalesPageChange(salesPage - 1)">上一页</button>
        <span>{{ salesPage }} / {{ salesTotalPages }}</span>
        <button class="btn" :disabled="salesPage >= salesTotalPages" @click="handleSalesPageChange(salesPage + 1)">下一页</button>
      </section>
    </section>
  </div>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { changeProductStatus, getMyProducts, getSalesTransactions } from '../../api'

const emit = defineEmits(['edit', 'dispute', 'messages', 'error', 'products-changed'])

const myProducts = ref([])
const sales = ref([])
const loadingMine = ref(false)
const loadingSales = ref(false)
const totalSales = ref(0)
const salesPage = ref(1)
const salesPageSize = ref(10)
const salesTotalPages = computed(() => Math.max(1, Math.ceil(totalSales.value / salesPageSize.value)))

const statusText = (status) => ({
  Active: '发布中',
  Locked: '已锁定',
  Sold: '已售出',
  Inactive: '已下架',
}[status] || status || '未知')

const orderStatusText = (status) => ({
  Pending: '待支付',
  Paid: '已支付',
  Completed: '已完成',
  Cancelled: '已取消',
  Disputed: '纠纷中',
  Refunded: '已退款',
}[status] || status || '未知')

const canDispute = (order) => {
  return order.transactionStatus === 'Paid'
}

const canEditProduct = (product) => {
  return product.status === 'Active'
}

const canRestoreProduct = (product) => {
  return product.status === 'Inactive' && product.stock > 0
}

const canOffShelfProduct = (product) => {
  return product.status === 'Active' || product.status === 'Locked'
}

const loadMyProducts = async () => {
  try {
    loadingMine.value = true
    const res = await getMyProducts()
    myProducts.value = res.data
  } catch (e) {
    emit('error', '无法加载我的商品: ' + (e.response?.data?.message || e.message))
  } finally {
    loadingMine.value = false
  }
}

const loadSales = async () => {
  try {
    loadingSales.value = true
    const res = await getSalesTransactions({
      page: salesPage.value,
      pageSize: salesPageSize.value,
    })
    sales.value = res.data
    const totalHeader = res.headers?.['x-total-count'] || res.headers?.['X-Total-Count']
    totalSales.value = Number.parseInt(totalHeader || '0', 10) || 0
  } catch (e) {
    emit('error', '无法加载卖出订单: ' + (e.response?.data?.message || e.message))
  } finally {
    loadingSales.value = false
  }
}

const handleProductStatus = async (product, action) => {
  try {
    await changeProductStatus(product.productID, { action })
    await loadMyProducts()
    emit('products-changed')
  } catch (e) {
    emit('error', '状态更新失败: ' + (e.response?.data?.message || e.message))
  }
}

const handleSalesPageChange = (nextPage) => {
  const target = Math.min(Math.max(1, nextPage), salesTotalPages.value)
  if (target !== salesPage.value) {
    salesPage.value = target
  }
}

watch(salesPage, () => {
  loadSales()
})

watch(salesPageSize, () => {
  salesPage.value = 1
  loadSales()
})

defineExpose({ reloadProducts: loadMyProducts, reloadSales: loadSales })

onMounted(() => {
  loadMyProducts()
  loadSales()
})
</script>

<style scoped>
.tab-content {
  min-height: 400px;
  min-width: 0;
}

.product-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.orders-panel {
  background: linear-gradient(180deg, color-mix(in oklab, var(--surface) 94%, #f8fafc 6%), var(--surface));
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1rem;
}

.orders-panel h2 {
  font-size: 1rem;
}

.product-row,
.order-row {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1rem;
}

.product-row,
.order-row {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
}

.product-row h2,
.order-row h2 {
  font-size: 1rem;
}

.product-row p {
  color: var(--text-secondary);
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

.market-pagination {
  align-items: center;
  display: flex;
  justify-content: center;
  gap: 0.625rem;
}

.market-pagination span {
  color: var(--text-secondary);
  font-size: 0.875rem;
}

.empty-state {
  border: 1px dashed #d9dbe5;
  border-radius: 20px;
  background: #fafaff;
}

.compact {
  min-height: auto;
  padding: 1.5rem;
}

@media (max-width: 820px) {
  .product-row,
  .order-row {
    align-items: stretch;
    flex-direction: column;
  }
}

@media (max-width: 640px) {
  .row-actions {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
  }

  .row-actions .btn {
    width: 100%;
  }
}
</style>

<style scoped>
/* Marketplace theme */
.orders-panel { border: 1px solid rgba(25,34,59,.08); border-radius: 22px; background: #fff; box-shadow: 0 12px 35px rgba(29,35,58,.05); }
.orders-panel h2 { font-size: 1.15rem; letter-spacing: -.025em; }
.product-row, .order-row { align-items: center; padding: 1.15rem 1.25rem; border: 1px solid rgba(25,34,59,.08); border-radius: 18px; background: #fff; box-shadow: 0 8px 25px rgba(29,35,58,.04); }
.row-actions .btn { border-radius: 10px; }
.market-pagination { padding: 1rem; border-radius: 16px; background: #fff; }
</style>
