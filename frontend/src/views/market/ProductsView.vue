<template>
  <div class="page-container product-page">
    <div class="market-nav">
      <ProductTabs v-model="activeTab" />
      <span class="market-nav-note">让闲置更有价值</span>
    </div>

    <div v-if="error" class="error-message">{{ error }}</div>

    <AllProductsPanel
      v-if="activeTab === 'all'"
      ref="allProductsPanel"
      @open-detail="openProductDetail"
      @order="handleCreateOrder"
      @report="openReport"
      @products-changed="myProductsPanel?.reloadProducts()"
      @error="error = $event"
    />

    <MyProductsPanel
      v-else-if="activeTab === 'my-products'"
      ref="myProductsPanel"
      @edit="openEditProduct"
      @dispute="openDispute"
      @messages="openOrderMessages"
      @products-changed="allProductsPanel?.reload()"
      @error="error = $event"
    />

    <OrdersPanel
      v-else-if="activeTab === 'orders'"
      ref="ordersPanel"
      @pay="handlePay"
      @confirm="handleConfirm"
      @cancel="handleCancel"
      @dispute="openDispute"
      @messages="openOrderMessages"
      @error="error = $event"
    />

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
              <span>{{ selectedProduct.category || '其他' }}</span>
              <span>{{ selectedProduct.condition || '良好' }}</span>
              <span
                class="seller-link"
                :title="selectedProduct.userID ? '查看卖家主页' : ''"
                @click="openSellerHome(selectedProduct)"
              >卖家 {{ selectedProduct.sellerName || '匿名卖家' }}</span>
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
        <input
          type="file"
          multiple
          accept="image/jpeg,image/png,image/gif,image/webp"
          :disabled="editingSaving"
          @change="handlePickEditProductImages"
        />
        <div v-if="productEditImageList.length" class="product-images product-image-preview">
          <div v-for="(item, index) in productEditImageList" :key="`${item.source}-${index}`" class="image-preview-item">
            <img :src="item.url" :alt="`图片 ${index + 1}`" loading="lazy" />
            <button type="button" class="image-remove" @click="removeEditProductImage(index)">移除</button>
          </div>
        </div>
        <p class="field-hint">最多 6 张，编辑时可替换图片，按列表顺序提交。</p>
        <div class="form-row">
          <select v-model="editForm.category">
            <option value="教材资料">教材资料</option>
            <option value="数码设备">数码设备</option>
            <option value="生活用品">生活用品</option>
            <option value="交通出行">交通出行</option>
            <option value="其他">其他</option>
          </select>
          <select v-model="editForm.condition">
            <option value="全新">全新</option>
            <option value="几乎全新">几乎全新</option>
            <option value="良好">良好</option>
            <option value="有使用痕迹">有使用痕迹</option>
          </select>
          <input v-model.number="editForm.price" type="number" min="0.01" step="0.01" placeholder="价格" required />
          <input v-model.number="editForm.stock" type="number" min="1" step="1" placeholder="库存" required />
        </div>
        <button class="btn btn-primary" type="submit" :disabled="editingSaving">
          {{ editingSaving ? '保存中...' : '保存商品' }}
        </button>
      </form>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import ProductTabs from '../../tab/product/ProductTabs.vue'
import AllProductsPanel from '../../tab/product/AllProductsPanel.vue'
import MyProductsPanel from '../../tab/product/MyProductsPanel.vue'
import OrdersPanel from '../../tab/product/OrdersPanel.vue'
import {
  cancelTransaction,
  confirmReceipt,
  createDispute,
  createReport,
  createTransaction,
  uploadImages,
  getOrderMessages,
  getProduct,
  getWallet,
  payTransaction,
  sendOrderMessage,
  updateProduct,
} from '../../api'

const router = useRouter()

const openSellerHome = (product) => {
  if (product.userID) {
    router.push(`/user/${product.userID}`)
  }
}

const activeTab = ref('all')
const allProductsPanel = ref(null)
const myProductsPanel = ref(null)
const ordersPanel = ref(null)
const error = ref(null)
const disputeTarget = ref(null)
const disputeReason = ref('')
const reportTarget = ref(null)
const reportReason = ref('')
const messageTarget = ref(null)
const orderMessages = ref([])
const orderMessageText = ref('')
const walletBalance = ref('0.00')
const detailOpen = ref(false)
const detailLoading = ref(false)
const selectedProduct = ref(null)
const editingProduct = ref(null)
const editingSaving = ref(false)
const editImageUrls = ref([])
const editImageFiles = ref([])
const editImageNewUrls = ref([])
const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/gif', 'image/webp']
const MAX_IMAGE_BYTES = 5 * 1024 * 1024
const MAX_IMAGE_COUNT = 6

const productEditImageList = computed(() => {
  const remote = editImageUrls.value.map((url, index) => ({
    source: 'remote',
    key: `remote-${index}`,
    url,
  }))
  const local = editImageNewUrls.value.map((url, index) => ({
    source: 'local',
    key: `local-${index}`,
    url,
  }))
  return [...remote, ...local]
})

const editForm = ref({
  title: '',
  description: '',
  category: '其他',
  condition: '良好',
  price: null,
  stock: 1,
})

const loadWallet = async () => {
  const res = await getWallet()
  walletBalance.value = Number(res.data.balance || 0).toFixed(2)
}

const clearEditProductImageState = () => {
  editImageNewUrls.value.forEach(url => URL.revokeObjectURL(url))
  editImageFiles.value = []
  editImageNewUrls.value = []
  editImageUrls.value = []
}

const reloadMountedProductFeeds = () => {
  allProductsPanel.value?.reload()
  myProductsPanel.value?.reloadProducts()
}

const openOrderListTab = () => {
  if (activeTab.value === 'orders') {
    ordersPanel.value?.resetAndReload()
    return
  }
  activeTab.value = 'orders'
}

const validateImageFile = (file) => {
  if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
    return '仅支持 JPG、PNG、GIF、WebP 图片'
  }
  if (file.size > MAX_IMAGE_BYTES) {
    return '单张图片不能超过5MB'
  }
  return null
}

const handlePickEditProductImages = (event) => {
  const files = Array.from(event.target.files || [])
  event.target.value = ''
  if (files.length === 0) return

  const nextCount = editImageUrls.value.length + editImageNewUrls.value.length + files.length
  if (nextCount > MAX_IMAGE_COUNT) {
    error.value = `图片数量不能超过 ${MAX_IMAGE_COUNT} 张`
    return
  }

  for (const file of files) {
    const message = validateImageFile(file)
    if (message) {
      error.value = message
      return
    }
  }

  const previewUrls = files.map(file => URL.createObjectURL(file))
  editImageFiles.value = [...editImageFiles.value, ...files]
  editImageNewUrls.value = [...editImageNewUrls.value, ...previewUrls]
}

const removeEditProductImage = (index) => {
  const remoteCount = editImageUrls.value.length
  if (index < remoteCount) {
    editImageUrls.value = editImageUrls.value.filter((_, i) => i !== index)
    return
  }

  const localIndex = index - remoteCount
  const removedUrl = editImageNewUrls.value[localIndex]
  if (removedUrl) {
    URL.revokeObjectURL(removedUrl)
  }
  editImageNewUrls.value.splice(localIndex, 1)
  editImageFiles.value.splice(localIndex, 1)
}

const handleCreateOrder = async (product) => {
  try {
    await createTransaction({ productID: product.productID })
    reloadMountedProductFeeds()
    closeProductDetail()
    openOrderListTab()
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
  clearEditProductImageState()
  editForm.value = {
    title: product.title || '',
    description: product.description || '',
    category: product.category || '其他',
    condition: product.condition || '良好',
    price: product.price,
    stock: product.stock || 1,
  }
  editImageUrls.value = [...(product.imageUrls || [])]
}

const closeEditProduct = () => {
  editingProduct.value = null
  clearEditProductImageState()
}

const handleUpdateProduct = async () => {
  if (!editingProduct.value) return

  try {
    editingSaving.value = true
    error.value = null
    const uploaded = editImageFiles.value.length > 0 ? await uploadImages(editImageFiles.value, 'products') : null
    const imageUrls = [...editImageUrls.value, ...(uploaded?.data?.urls || [])].slice(0, MAX_IMAGE_COUNT)
    await updateProduct(editingProduct.value.productID, {
      ...editForm.value,
      imageUrls,
    })
    closeEditProduct()
    reloadMountedProductFeeds()
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

const handlePay = async (order) => {
  try {
    error.value = null
    await payTransaction(order.transactionID)
    ordersPanel.value?.reload()
    await loadWallet()
  } catch (e) {
    error.value = '支付失败: ' + (e.response?.data?.message || e.message)
  }
}

const handleConfirm = async (order) => {
  try {
    error.value = null
    await confirmReceipt(order.transactionID)
    ordersPanel.value?.reload()
    reloadMountedProductFeeds()
    await loadWallet()
  } catch (e) {
    error.value = '确认收货失败: ' + (e.response?.data?.message || e.message)
  }
}

const handleCancel = async (order) => {
  try {
    error.value = null
    await cancelTransaction(order.transactionID)
    ordersPanel.value?.reload()
    reloadMountedProductFeeds()
  } catch (e) {
    error.value = '取消订单失败: ' + (e.response?.data?.message || e.message)
  }
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
    ordersPanel.value?.reload()
    myProductsPanel.value?.reloadSales()
  } catch (e) {
    error.value = '提交纠纷失败: ' + (e.response?.data?.message || e.message)
  }
}

const openReport = (product) => {
  reportTarget.value = product
  reportReason.value = ''
}

const handleCreateReport = async () => {
  try {
    await createReport({
      targetType: 'Product',
      targetID: reportTarget.value.productID,
      reason: reportReason.value,
    })
    reportTarget.value = null
    reportReason.value = ''
  } catch (e) {
    error.value = '提交举报失败: ' + (e.response?.data?.message || e.message)
  }
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
  if (!orderMessageText.value.trim()) return
  await sendOrderMessage(messageTarget.value.transactionID, { content: orderMessageText.value.trim() })
  orderMessageText.value = ''
  await openOrderMessages(messageTarget.value)
  ordersPanel.value?.reload()
  myProductsPanel.value?.reloadSales()
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

const formatDate = (value) => {
  if (!value) return ''
  return new Date(value).toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

onMounted(async () => {
  await loadWallet()
})
</script>

<style scoped>
.dispute-form,
.orders-panel {
  background: linear-gradient(180deg, color-mix(in oklab, var(--surface) 94%, #f8fafc 6%), var(--surface));
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1rem;
}

.dispute-form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.dispute-form h2,
.orders-panel h2 {
  font-size: 1rem;
}

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

.image-preview-item {
  position: relative;
}

.image-remove {
  align-items: center;
  background: rgba(0, 0, 0, 0.68);
  border: none;
  border-radius: 9999px;
  color: #fff;
  cursor: pointer;
  display: inline-flex;
  font-size: 0.75rem;
  padding: 0.25rem 0.5rem;
  position: absolute;
  right: 0.375rem;
  top: 0.375rem;
}

.product-image-preview {
  margin-bottom: 0.25rem;
}

.image-preview-item .image-remove {
  opacity: 0;
  transition: opacity 0.15s;
}

.image-preview-item:hover .image-remove,
.image-preview-item:focus-within .image-remove {
  opacity: 1;
}

.field-hint {
  color: var(--text-secondary);
  font-size: 0.75rem;
  margin: 0;
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

.product-head h2 {
  font-size: 1rem;
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
  .form-row {
    align-items: stretch;
    flex-direction: column;
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

<style scoped>
.product-page {
  --market-ink: #11182b;
  --market-purple: #6654ee;
  --market-teal: #16a6a3;
  width: 100%;
  max-width: 1480px;
  margin: 0 auto;
  color: var(--market-ink);
}

.market-nav { display: flex; align-items: center; justify-content: space-between; gap: 1rem; margin: 1.25rem 0; padding: .55rem; border: 1px solid rgba(24,32,55,.08); border-radius: 18px; background: #fff; box-shadow: 0 10px 30px rgba(28,34,64,.05); }
.market-nav-note { padding-right: .8rem; color: #9a9fb0; font-size: .72rem; }

.product-page .orders-panel, .product-page .dispute-form { border: 1px solid rgba(25,34,59,.08); border-radius: 22px; background: #fff; box-shadow: 0 12px 35px rgba(29,35,58,.05); }
.product-page .orders-panel h2 { font-size: 1.15rem; letter-spacing: -.025em; }
.product-page .product-edit-form input, .product-page .product-edit-form select, .product-page .product-edit-form textarea, .product-page .dispute-form textarea, .product-page .message-form input { border: 1px solid #e3e5ed; border-radius: 12px; background: #fff; transition: border-color .2s, box-shadow .2s; }
.product-page :is(input, select, textarea):focus { border-color: #7463ee; outline: none; box-shadow: 0 0 0 4px rgba(105,87,245,.1); }
.product-page .form-row { align-items: stretch; }
.product-page .form-row > * { flex: 1 1 130px; }

.product-page .product-head h2 { font-size: 1.05rem; letter-spacing: -.02em; }
.product-page .product-images { overflow: hidden; border-radius: 15px; background: #f2f4f7; }
.product-page .product-images img { border: 0; border-radius: 0; }
.product-page .product-meta { gap: .45rem; }
.product-page .product-meta span { padding: .28rem .5rem; border-radius: 999px; background: #f4f5f8; font-size: .7rem; }
.product-page .product-meta .seller-link { color: #5d4fd5; cursor: pointer; }
.product-page .product-meta .seller-link:hover { text-decoration: underline; }
.product-page .product-meta strong { width: 100%; letter-spacing: -.04em; }

.product-page .row-actions .btn { border-radius: 10px; }
.product-page .empty-state { border: 1px dashed #d9dbe5; border-radius: 20px; background: #fafaff; }
.product-page .order-message { border-radius: 14px; background: #f8f8fc; }

.product-page .detail-backdrop { background: rgba(15,18,38,.55); backdrop-filter: blur(5px); }
.product-page .product-detail-panel { padding: 1.25rem; border-left: 0; background: #f6f6fb; box-shadow: -24px 0 60px rgba(12,16,35,.24); }
.product-page .detail-header, .product-page .product-detail { border: 1px solid rgba(25,34,59,.08); border-radius: 20px; box-shadow: 0 10px 30px rgba(29,35,58,.05); }
.product-page .product-detail .product-head strong { color: #5d4dd7; font-size: 1.6rem; }

@media (max-width: 640px) {
  .market-nav { align-items: stretch; overflow-x: auto; }
  .market-nav-note { display: none; }
  .product-page .row-actions { display: grid; grid-template-columns: repeat(2, 1fr); }
  .product-page .row-actions .btn { width: 100%; }
}
</style>
