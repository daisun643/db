<template>
  <div class="page-container">
    <div class="page-header page-header-tabs">
      <h1 class="page-title">交易</h1>

      <div class="tabs">
        <button :class="['tab', { active: activeTab === 'all' }]" @click="activeTab = 'all'">
          商品列表
        </button>
        <button :class="['tab', { active: activeTab === 'my-products' }]" @click="activeTab = 'my-products'">
          我的商品
        </button>
        <button :class="['tab', { active: activeTab === 'orders' }]" @click="activeTab = 'orders'">
          我的订单
        </button>
      </div>
    </div>

    <div v-if="error" class="error-message">{{ error }}</div>

    <section class="wallet-panel">
      <div>
        <span class="muted">钱包余额</span>
        <strong>¥{{ walletBalance }}</strong>
      </div>
      <form class="wallet-actions" @submit.prevent="handleDeposit">
        <input v-model.number="depositAmount" type="number" min="0.01" step="0.01" placeholder="充值金额" />
        <button class="btn" type="submit" :disabled="!depositAmount || depositing">
          {{ depositing ? '处理中...' : '充值' }}
        </button>
      </form>
    </section>

    <div v-if="activeTab === 'all'" class="market-layout">
      <form class="product-form" @submit.prevent="handleCreateProduct">
        <h2>发布闲置</h2>
        <input v-model="productForm.title" type="text" placeholder="商品标题" required />
        <textarea v-model="productForm.description" placeholder="商品描述"></textarea>
        <input v-model="productImageText" type="text" placeholder="图片 URL，用逗号分隔" />
        <div class="form-row">
          <input v-model.number="productForm.price" type="number" min="0.01" step="0.01" placeholder="价格" required />
          <input v-model.number="productForm.stock" type="number" min="1" step="1" placeholder="库存" required />
          <button class="btn btn-primary" type="submit" :disabled="submitting">
            {{ submitting ? '发布中...' : '发布' }}
          </button>
        </div>
      </form>

      <div v-if="loading" class="loading">加载中...</div>
      <div v-else class="product-grid">
        <article v-for="product in products" :key="product.productID" class="product-card">
          <div class="product-head">
            <h2>{{ product.title }}</h2>
            <span :class="['badge', product.status === 'Active' ? 'badge-green' : 'badge-yellow']">
              {{ statusText(product.status) }}
            </span>
          </div>
          <div v-if="product.imageUrls?.length" class="product-images">
            <img v-for="url in product.imageUrls.slice(0, 3)" :key="url" :src="url" alt="" loading="lazy" />
          </div>
          <p>{{ product.description || '暂无描述' }}</p>
          <div class="product-meta">
            <strong>¥{{ product.price }}</strong>
            <span>库存 {{ product.stock }}</span>
            <span>{{ product.sellerName || '匿名卖家' }}</span>
          </div>
          <button class="btn" @click="openProductDetail(product)">查看详情</button>
          <button
            class="btn btn-primary"
            :disabled="product.status !== 'Active' || product.stock <= 0"
            @click="handleCreateOrder(product)"
          >
            下单锁定
          </button>
          <button class="link-button danger" @click="openReport(product)">举报商品</button>
        </article>
        <div v-if="products.length === 0" class="empty-state">
          <p>暂无商品</p>
        </div>
      </div>
    </div>

    <div v-else-if="activeTab === 'my-products'" class="tab-content">
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
            <button class="btn" @click="openEditProduct(product)">编辑</button>
            <button class="btn" @click="handleProductStatus(product, 'restore')">上架</button>
            <button class="btn" @click="handleProductStatus(product, 'off-shelf')">下架</button>
            <button class="btn" @click="loadSales">刷新订单</button>
          </div>
        </article>
        <div v-if="myProducts.length === 0" class="empty-state">
          <p>暂无发布的商品</p>
        </div>
      </div>

      <section class="orders-panel">
        <h2>卖出订单</h2>
        <article v-for="order in sales" :key="order.transactionID" class="order-row">
          <span>#{{ order.transactionID }}</span>
          <span>{{ order.productTitle }}</span>
          <span>买家 {{ order.buyerName || '-' }}</span>
          <strong>¥{{ order.transactionAmount }}</strong>
          <span>{{ orderStatusText(order.transactionStatus) }}</span>
          <button class="btn" :disabled="!canDispute(order)" @click="openDispute(order)">纠纷</button>
          <button class="btn" @click="openOrderMessages(order)">留言板</button>
        </article>
        <div v-if="sales.length === 0" class="empty-state compact">
          <p>暂无卖出订单</p>
        </div>
      </section>
    </div>

    <div v-else-if="activeTab === 'orders'" class="tab-content">
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
            <button class="btn" :disabled="order.transactionStatus !== 'Pending'" @click="handlePay(order)">支付</button>
            <button class="btn" :disabled="order.transactionStatus !== 'Paid'" @click="handleConfirm(order)">确认收货</button>
            <button class="btn" :disabled="order.transactionStatus !== 'Pending'" @click="handleCancel(order)">取消</button>
            <button class="btn" :disabled="!canDispute(order)" @click="openDispute(order)">纠纷</button>
            <button class="btn" @click="openOrderMessages(order)">留言板</button>
          </div>
        </article>
        <div v-if="orders.length === 0" class="empty-state">
          <p>暂无订单</p>
        </div>
      </div>
    </div>

    <form v-if="disputeTarget" class="dispute-form" @submit.prevent="handleCreateDispute">
      <h2>订单 #{{ disputeTarget.transactionID }} 纠纷</h2>
      <textarea v-model="disputeReason" placeholder="描述纠纷原因" required></textarea>
      <div class="form-row">
        <button class="btn btn-primary" type="submit">提交纠纷</button>
        <button class="btn" type="button" @click="disputeTarget = null">取消</button>
      </div>
    </form>

    <section v-if="messageTarget" class="orders-panel">
      <h2>订单 #{{ messageTarget.transactionID }} 留言板</h2>
      <div class="order-message-list">
        <article v-for="message in orderMessages" :key="message.orderMessageID" class="order-message">
          <strong>{{ message.senderName || '用户' }}</strong>
          <span>{{ formatDate(message.sendTime) }}</span>
          <p>{{ message.content }}</p>
        </article>
        <div v-if="orderMessages.length === 0" class="empty-state compact">
          <p>暂无留言</p>
        </div>
      </div>
      <form v-if="!isOrderArchived(messageTarget)" class="message-form" @submit.prevent="handleSendOrderMessage">
        <input v-model="orderMessageText" type="text" placeholder="输入订单留言" required />
        <button class="btn btn-primary" type="submit">发送</button>
      </form>
      <div v-else class="muted">交易已结束，留言板已归档只读。</div>
    </section>

    <form v-if="reportTarget" class="dispute-form" @submit.prevent="handleCreateReport">
      <h2>举报商品：{{ reportTarget.title }}</h2>
      <textarea v-model="reportReason" placeholder="描述举报原因" required></textarea>
      <div class="form-row">
        <button class="btn btn-primary" type="submit">提交举报</button>
        <button class="btn" type="button" @click="reportTarget = null">取消</button>
      </div>
    </form>

    <div v-if="detailOpen" class="detail-backdrop" @click.self="closeProductDetail">
      <section class="product-detail-panel">
        <div class="detail-header">
          <button class="link-button" @click="closeProductDetail">返回列表</button>
          <span v-if="selectedProduct" class="muted">{{ statusText(selectedProduct.status) }}</span>
        </div>

        <div v-if="detailLoading" class="loading">加载中...</div>
        <template v-else-if="selectedProduct">
          <article class="product-detail">
            <div class="product-head">
              <h2>{{ selectedProduct.title }}</h2>
              <strong>¥{{ selectedProduct.price }}</strong>
            </div>
            <div v-if="selectedProduct.imageUrls?.length" class="detail-images">
              <img v-for="url in selectedProduct.imageUrls" :key="url" :src="url" alt="" loading="lazy" />
            </div>
            <p>{{ selectedProduct.description || '暂无描述' }}</p>
            <div class="product-meta">
              <span>库存 {{ selectedProduct.stock }}</span>
              <span>卖家 {{ selectedProduct.sellerName || '匿名卖家' }}</span>
              <span>{{ formatDate(selectedProduct.publishTime) }}</span>
            </div>
            <div class="row-actions">
              <button
                class="btn btn-primary"
                :disabled="selectedProduct.status !== 'Active' || selectedProduct.stock <= 0"
                @click="handleCreateOrder(selectedProduct)"
              >
                下单锁定
              </button>
              <button class="btn" @click="openReport(selectedProduct)">举报商品</button>
            </div>
          </article>
        </template>
      </section>
    </div>

    <div v-if="editingProduct" class="detail-backdrop" @click.self="closeEditProduct">
      <form class="product-detail-panel product-edit-form" @submit.prevent="handleUpdateProduct">
        <div class="detail-header">
          <button class="link-button" type="button" @click="closeEditProduct">取消编辑</button>
          <span class="muted">商品 #{{ editingProduct.productID }}</span>
        </div>
        <h2>编辑商品</h2>
        <input v-model="editForm.title" type="text" placeholder="商品标题" required />
        <textarea v-model="editForm.description" placeholder="商品描述"></textarea>
        <input v-model="editImageText" type="text" placeholder="图片 URL，用逗号分隔" />
        <div class="form-row">
          <input v-model.number="editForm.price" type="number" min="0.01" step="0.01" placeholder="价格" required />
          <input v-model.number="editForm.stock" type="number" min="1" step="1" placeholder="库存" required />
          <select v-model="editForm.status">
            <option value="Active">发布中</option>
            <option value="Locked">已锁定</option>
            <option value="Sold">已售出</option>
            <option value="Inactive">已下架</option>
          </select>
        </div>
        <button class="btn btn-primary" type="submit" :disabled="editingSaving">
          {{ editingSaving ? '保存中...' : '保存商品' }}
        </button>
      </form>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref, watch } from 'vue'
import {
  cancelTransaction,
  changeProductStatus,
  confirmReceipt,
  createDispute,
  createProduct,
  createReport,
  createTransaction,
  depositWallet,
  getOrderMessages,
  getMyProducts,
  getMyTransactions,
  getProducts,
  getProduct,
  getSalesTransactions,
  getWallet,
  payTransaction,
  sendOrderMessage,
  updateProduct,
} from '../api'

const activeTab = ref('all')
const products = ref([])
const myProducts = ref([])
const orders = ref([])
const sales = ref([])
const loading = ref(true)
const loadingMine = ref(false)
const loadingOrders = ref(false)
const submitting = ref(false)
const error = ref(null)
const disputeTarget = ref(null)
const disputeReason = ref('')
const reportTarget = ref(null)
const reportReason = ref('')
const messageTarget = ref(null)
const orderMessages = ref([])
const orderMessageText = ref('')
const walletBalance = ref('0.00')
const depositAmount = ref(null)
const depositing = ref(false)
const productImageText = ref('')
const detailOpen = ref(false)
const detailLoading = ref(false)
const selectedProduct = ref(null)
const editingProduct = ref(null)
const editingSaving = ref(false)
const editImageText = ref('')

const productForm = ref({
  title: '',
  description: '',
  price: null,
  stock: 1,
})

const editForm = ref({
  title: '',
  description: '',
  price: null,
  stock: 1,
  status: 'Active',
})

const loadProducts = async () => {
  try {
    loading.value = true
    const res = await getProducts()
    products.value = res.data
  } catch (e) {
    error.value = '无法加载商品数据: ' + (e.response?.data?.message || e.message)
  } finally {
    loading.value = false
  }
}

const loadWallet = async () => {
  const res = await getWallet()
  walletBalance.value = Number(res.data.balance || 0).toFixed(2)
}

const loadMyProducts = async () => {
  try {
    loadingMine.value = true
    const res = await getMyProducts()
    myProducts.value = res.data
  } catch (e) {
    error.value = '无法加载我的商品: ' + (e.response?.data?.message || e.message)
  } finally {
    loadingMine.value = false
  }
}

const loadOrders = async () => {
  try {
    loadingOrders.value = true
    const res = await getMyTransactions()
    orders.value = res.data
  } catch (e) {
    error.value = '无法加载订单: ' + (e.response?.data?.message || e.message)
  } finally {
    loadingOrders.value = false
  }
}

const loadSales = async () => {
  const res = await getSalesTransactions()
  sales.value = res.data
}

const handleCreateProduct = async () => {
  try {
    submitting.value = true
    error.value = null
    const imageUrls = productImageText.value.split(/[,，]/).map(url => url.trim()).filter(Boolean)
    await createProduct({ ...productForm.value, imageUrls })
    productForm.value = { title: '', description: '', price: null, stock: 1 }
    productImageText.value = ''
    await Promise.all([loadProducts(), loadMyProducts()])
  } catch (e) {
    error.value = '发布失败: ' + (e.response?.data?.message || e.message)
  } finally {
    submitting.value = false
  }
}

const handleCreateOrder = async (product) => {
  try {
    await createTransaction({ productID: product.productID })
    await Promise.all([loadProducts(), loadOrders()])
    closeProductDetail()
    activeTab.value = 'orders'
  } catch (e) {
    error.value = '下单失败: ' + (e.response?.data?.message || e.message)
  }
}

const openProductDetail = async (product) => {
  try {
    detailOpen.value = true
    detailLoading.value = true
    error.value = null
    const res = await getProduct(product.productID)
    selectedProduct.value = res.data
  } catch (e) {
    error.value = '无法加载商品详情: ' + (e.response?.data?.message || e.message)
  } finally {
    detailLoading.value = false
  }
}

const closeProductDetail = () => {
  detailOpen.value = false
  selectedProduct.value = null
}

const openEditProduct = (product) => {
  editingProduct.value = product
  editForm.value = {
    title: product.title || '',
    description: product.description || '',
    price: product.price,
    stock: product.stock || 1,
    status: product.status || 'Active',
  }
  editImageText.value = (product.imageUrls || []).join(', ')
}

const closeEditProduct = () => {
  editingProduct.value = null
  editImageText.value = ''
}

const handleUpdateProduct = async () => {
  if (!editingProduct.value) return

  try {
    editingSaving.value = true
    error.value = null
    const imageUrls = editImageText.value.split(/[,，]/).map(url => url.trim()).filter(Boolean)
    await updateProduct(editingProduct.value.productID, {
      ...editForm.value,
      imageUrls,
    })
    closeEditProduct()
    await Promise.all([loadProducts(), loadMyProducts()])
    if (selectedProduct.value) {
      const refreshed = await getProduct(selectedProduct.value.productID)
      selectedProduct.value = refreshed.data
    }
  } catch (e) {
    error.value = '保存失败: ' + (e.response?.data?.message || e.message)
  } finally {
    editingSaving.value = false
  }
}

const handleDeposit = async () => {
  try {
    depositing.value = true
    error.value = null
    await depositWallet({ amount: depositAmount.value })
    depositAmount.value = null
    await loadWallet()
  } catch (e) {
    error.value = '充值失败: ' + (e.response?.data?.message || e.message)
  } finally {
    depositing.value = false
  }
}

const handleProductStatus = async (product, action) => {
  await changeProductStatus(product.productID, { action })
  await Promise.all([loadProducts(), loadMyProducts()])
}

const handlePay = async (order) => {
  try {
    error.value = null
    await payTransaction(order.transactionID)
    await Promise.all([loadOrders(), loadWallet()])
  } catch (e) {
    error.value = '支付失败: ' + (e.response?.data?.message || e.message)
  }
}

const handleConfirm = async (order) => {
  await confirmReceipt(order.transactionID)
  await Promise.all([loadOrders(), loadProducts(), loadWallet()])
}

const handleCancel = async (order) => {
  await cancelTransaction(order.transactionID)
  await Promise.all([loadOrders(), loadProducts()])
}

const openDispute = (order) => {
  disputeTarget.value = order
  disputeReason.value = ''
}

const handleCreateDispute = async () => {
  try {
    error.value = null
    await createDispute(disputeTarget.value.transactionID, { reason: disputeReason.value })
    disputeTarget.value = null
    disputeReason.value = ''
    await Promise.all([loadOrders(), loadSales()])
  } catch (e) {
    error.value = '提交纠纷失败: ' + (e.response?.data?.message || e.message)
  }
}

const openReport = (product) => {
  reportTarget.value = product
  reportReason.value = ''
}

const handleCreateReport = async () => {
  await createReport({
    targetType: 'Product',
    targetID: reportTarget.value.productID,
    reason: reportReason.value,
  })
  reportTarget.value = null
  reportReason.value = ''
}

const openOrderMessages = async (order) => {
  disputeTarget.value = null
  reportTarget.value = null
  messageTarget.value = order
  orderMessageText.value = ''
  const res = await getOrderMessages(order.transactionID)
  orderMessages.value = res.data
}

const handleSendOrderMessage = async () => {
  if (!messageTarget.value) return
  await sendOrderMessage(messageTarget.value.transactionID, { content: orderMessageText.value })
  orderMessageText.value = ''
  await openOrderMessages(messageTarget.value)
  await Promise.all([loadOrders(), loadSales()])
}

const canDispute = (order) => {
  return order.transactionStatus === 'Paid'
}

const isOrderArchived = (order) => {
  return ['Completed', 'Cancelled', 'Refunded'].includes(order.transactionStatus)
}

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

const formatDate = (value) => {
  if (!value) return ''
  return new Date(value).toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

watch(activeTab, async (tab) => {
  if (tab === 'my-products') {
    await Promise.all([loadMyProducts(), loadSales()])
  }
  if (tab === 'orders') {
    await loadOrders()
  }
})

onMounted(async () => {
  await Promise.all([loadProducts(), loadMyProducts(), loadOrders(), loadWallet()])
})
</script>

<style scoped>
.market-layout,
.product-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.product-form,
.dispute-form,
.orders-panel {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1rem;
}

.wallet-panel {
  align-items: center;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1rem;
  padding: 0.875rem 1rem;
}

.wallet-panel > div {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.wallet-panel strong {
  color: var(--primary);
  font-size: 1.25rem;
}

.wallet-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.wallet-actions input {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
  padding: 0.5rem 0.625rem;
  width: 140px;
}

.product-form,
.dispute-form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.product-form h2,
.dispute-form h2,
.orders-panel h2 {
  font-size: 1rem;
}

.product-form input,
.product-form textarea,
.product-edit-form input,
.product-edit-form select,
.product-edit-form textarea,
.dispute-form textarea,
.message-form input {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
  padding: 0.625rem 0.75rem;
}

.product-form textarea,
.product-edit-form textarea,
.dispute-form textarea {
  min-height: 96px;
  resize: vertical;
}

.form-row,
.product-meta,
.row-actions,
.message-form {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.form-row input {
  min-width: 120px;
}

.product-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 1rem;
}

.product-card,
.product-row,
.order-row {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1rem;
}

.product-card {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.product-row,
.order-row {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
}

.product-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.75rem;
}

.product-images {
  display: grid;
  gap: 0.5rem;
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.product-images img {
  aspect-ratio: 4 / 3;
  background: var(--bg);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  object-fit: cover;
  width: 100%;
}

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

.product-detail h2,
.product-edit-form h2 {
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

.product-edit-form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.product-head h2,
.product-row h2,
.order-row h2 {
  font-size: 1rem;
}

.product-card p,
.product-row p {
  color: var(--text-secondary);
}

.product-meta {
  color: var(--text-secondary);
  font-size: 0.875rem;
}

.product-meta strong {
  color: var(--primary);
  font-size: 1.125rem;
}

.compact {
  min-height: auto;
  padding: 1.5rem;
}

.link-button {
  border: none;
  background: transparent;
  color: var(--primary);
  cursor: pointer;
  font: inherit;
  text-align: left;
}

.link-button.danger {
  color: #dc2626;
}

.order-message-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.order-message {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 0.75rem;
}

.order-message span,
.muted {
  color: var(--text-secondary);
  font-size: 0.875rem;
}

@media (max-width: 760px) {
  .product-row,
  .order-row {
    flex-direction: column;
  }

  .form-row {
    align-items: stretch;
    flex-direction: column;
  }

  .wallet-panel,
  .wallet-actions {
    align-items: stretch;
    flex-direction: column;
  }

  .wallet-actions input {
    width: 100%;
  }

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
