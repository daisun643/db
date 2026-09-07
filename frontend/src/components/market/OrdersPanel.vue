<template>
  <div class="tab-content">
    <div v-if="loadingOrders" class="loading">加载中...</div>
    <div v-else class="product-list">
      <article v-for="order in orders" :key="order.transactionID" class="order-row">
        <div>
          <h2>#{{ order.transactionID }} {{ order.productTitle }}</h2>
          <div class="product-meta">
            <strong>¥{{ order.transactionAmount }}</strong>
            <span>{{ orderStatusText(order.transactionStatus) }}</span>
            <span>卖家 {{ order.sellerName || '-' }}</span>
            <span>{{ formatDate(order.createTime) }}</span>
          </div>
        </div>
        <div class="row-actions">
          <button class="btn" :disabled="order.transactionStatus !== 'Pending'" @click="$emit('pay', order)">支付</button>
          <button class="btn" :disabled="order.transactionStatus !== 'Paid'" @click="$emit('confirm', order)">确认收货</button>
          <button class="btn" :disabled="order.transactionStatus !== 'Pending'" @click="$emit('cancel', order)">取消</button>
          <button class="btn" :disabled="!canDispute(order)" @click="$emit('dispute', order)">纠纷</button>
          <button class="btn" @click="$emit('messages', order)">留言板</button>
        </div>
      </article>
      <div v-if="orders.length === 0" class="empty-state">
        <p>暂无订单</p>
      </div>
      <section v-if="orderTotalPages > 1" class="market-pagination">
        <button class="btn" :disabled="orderPage <= 1" @click="handleOrderPageChange(orderPage - 1)">上一页</button>
        <span>{{ orderPage }} / {{ orderTotalPages }}</span>
        <button class="btn" :disabled="orderPage >= orderTotalPages" @click="handleOrderPageChange(orderPage + 1)">下一页</button>
      </section>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { getMyTransactions } from '../../api'

const emit = defineEmits(['pay', 'confirm', 'cancel', 'dispute', 'messages', 'error'])

const orders = ref([])
const loadingOrders = ref(false)
const totalOrders = ref(0)
const orderPage = ref(1)
const orderPageSize = ref(10)
const orderTotalPages = computed(() => Math.max(1, Math.ceil(totalOrders.value / orderPageSize.value)))

const orderStatusText = (status) => ({
  Pending: '待支付',
  Paid: '已支付',
  Completed: '已完成',
  Cancelled: '已取消',
  Disputed: '纠纷中',
  Refunded: '已退款',
}[status] || status || '未知')

const formatDate = (value) => {
  if (!value) return ''
  return new Date(value).toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

const canDispute = (order) => {
  return order.transactionStatus === 'Paid'
}

const loadOrders = async () => {
  try {
    loadingOrders.value = true
    const res = await getMyTransactions({
      page: orderPage.value,
      pageSize: orderPageSize.value,
    })
    orders.value = res.data
    const totalHeader = res.headers?.['x-total-count'] || res.headers?.['X-Total-Count']
    totalOrders.value = Number.parseInt(totalHeader || '0', 10) || 0
  } catch (e) {
    emit('error', '无法加载订单: ' + (e.response?.data?.message || e.message))
  } finally {
    loadingOrders.value = false
  }
}

const handleOrderPageChange = (nextPage) => {
  const target = Math.min(Math.max(1, nextPage), orderTotalPages.value)
  if (target !== orderPage.value) {
    orderPage.value = target
  }
}

const resetAndReload = () => {
  orderPage.value = 1
  loadOrders()
}

watch(orderPage, () => {
  loadOrders()
})

watch(orderPageSize, () => {
  orderPage.value = 1
  loadOrders()
})

defineExpose({ reload: loadOrders, resetAndReload })

onMounted(() => {
  loadOrders()
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

.order-row {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1rem;
}

.order-row {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
}

.order-row h2 {
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

@media (max-width: 820px) {
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
.order-row { align-items: center; padding: 1.15rem 1.25rem; border: 1px solid rgba(25,34,59,.08); border-radius: 18px; background: #fff; box-shadow: 0 8px 25px rgba(29,35,58,.04); }
.row-actions .btn { border-radius: 10px; }
.market-pagination { padding: 1rem; border-radius: 16px; background: #fff; }
</style>
