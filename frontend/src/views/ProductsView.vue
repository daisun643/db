<template>
  <div class="page-container product-page">
    <section class="market-hero">
      <div class="market-hero-copy">
        <span class="market-eyebrow"><i></i>校园闲置</span>
        <h1>发现好物，<span>让闲置继续流转。</span></h1>
        <p>浏览同学发布的商品，安全完成校内交易。</p>
        <div class="market-hero-pills">
          <span>校内实名</span><span>订单锁定</span><span>交易可追溯</span>
        </div>
      </div>
      <div class="market-wallet-card">
        <div class="wallet-card-heading">
          <span>AVAILABLE BALANCE</span>
          <span class="wallet-card-icon">¥</span>
        </div>
        <strong>¥{{ walletBalance }}</strong>
        <small>当前可用余额</small>
        <form class="wallet-actions" @submit.prevent="handleDeposit">
          <input v-model.number="depositAmount" type="number" min="0.01" step="0.01" placeholder="输入充值金额" />
          <button type="submit" :disabled="!depositAmount || depositing">
            {{ depositing ? '处理中...' : '立即充值' }}
          </button>
        </form>
        <div class="wallet-card-stats">
          <span><b>{{ totalProducts }}</b><small>在售好物</small></span>
          <span><b>安心</b><small>交易保障</small></span>
        </div>
      </div>
    </section>

    <div class="market-nav">
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
      <span class="market-nav-note">让闲置更有价值</span>
    </div>

    <div v-if="error" class="error-message">{{ error }}</div>

    <div v-if="activeTab === 'all'" class="market-layout">
      <section class="market-toolbar">
        <div class="toolbar-title">
          <h2>商品列表</h2>
          <span class="muted">共 {{ totalProducts }} 件</span>
        </div>
        <form class="market-filter-bar" @submit.prevent="loadProducts">
          <label class="market-search-field">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><circle cx="11" cy="11" r="7" /><path d="m20 20-3.6-3.6" /></svg>
            <input v-model="productFilter.keyword" type="search" placeholder="搜索商品、描述或卖家" />
          </label>
          <select v-model="productFilter.status">
            <option value="">全部状态</option>
            <option value="Active">上架中</option>
            <option value="Locked">锁定中</option>
            <option value="Sold">已售出</option>
            <option value="Inactive">已下架</option>
          </select>
          <select v-model="productFilter.category">
            <option value="">全部分类</option>
            <option value="教材资料">教材资料</option>
            <option value="数码设备">数码设备</option>
            <option value="生活用品">生活用品</option>
            <option value="交通出行">交通出行</option>
            <option value="其他">其他</option>
          </select>
          <select v-model="productFilter.sort">
            <option value="latest">按发布时间</option>
            <option value="price-asc">价格从低到高</option>
            <option value="price-desc">价格从高到低</option>
            <option value="stock-asc">库存从低到高</option>
            <option value="stock-desc">库存从高到低</option>
          </select>
          <button class="market-search-submit" type="submit">搜索</button>
          <button class="market-reset" type="button" @click="resetProductFilters">重置</button>
        </form>
      </section>

      <details class="product-create-panel">
        <summary><span><strong>发布闲置</strong><small>填写商品信息并上传图片</small></span><b>＋</b></summary>
      <form class="product-form" @submit.prevent="handleCreateProduct">
        <label class="product-form-field"><span>商品标题</span>
        <input v-model="productForm.title" type="text" placeholder="商品标题" required />
        </label>
        <label class="product-form-field"><span>商品描述</span>
        <textarea v-model="productForm.description" placeholder="商品描述"></textarea>
        </label>
        <input
          type="file"
          multiple
          accept="image/jpeg,image/png,image/gif,image/webp"
          :disabled="submitting"
          @change="handlePickCreateProductImages"
        />
        <div v-if="productImagePreviewUrls.length" class="product-images product-image-preview">
          <div v-for="(url, index) in productImagePreviewUrls" :key="url" class="image-preview-item">
            <img :src="url" :alt="`预览图 ${index + 1}`" loading="lazy" />
            <button type="button" class="image-remove" @click="removeCreateProductImage(index)">移除</button>
          </div>
        </div>
        <p class="field-hint">最多可上传 6 张，单张不超过 5MB。</p>
        <div class="form-row">
          <select v-model="productForm.category">
            <option value="教材资料">教材资料</option>
            <option value="数码设备">数码设备</option>
            <option value="生活用品">生活用品</option>
            <option value="交通出行">交通出行</option>
            <option value="其他">其他</option>
          </select>
          <select v-model="productForm.condition">
            <option value="全新">全新</option>
            <option value="几乎全新">几乎全新</option>
            <option value="良好">良好</option>
            <option value="有使用痕迹">有使用痕迹</option>
          </select>
          <input v-model.number="productForm.price" type="number" min="0.01" step="0.01" placeholder="价格" required />
          <input v-model.number="productForm.stock" type="number" min="1" step="1" placeholder="库存" required />
          <button class="btn btn-primary" type="submit" :disabled="submitting">
            {{ submitting ? '发布中...' : '发布' }}
          </button>
        </div>
      </form>
      </details>

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
            <span>{{ product.category || '其他' }}</span>
            <span>{{ product.condition || '良好' }}</span>
            <span>{{ product.sellerName || '匿名卖家' }}</span>
          </div>
          <div class="product-card-actions">
            <button class="btn" @click="openProductDetail(product)">查看详情</button>
            <button
              class="btn btn-primary"
              :disabled="product.status !== 'Active' || product.stock <= 0"
              @click="handleCreateOrder(product)"
            >
              下单锁定
            </button>
            <button class="link-button danger" @click="openReport(product)">举报</button>
          </div>
        </article>
        <div v-if="products.length === 0" class="empty-state">
          <p>暂无商品</p>
        </div>
      </div>

      <section v-if="productTotalPages > 1" class="market-pagination">
        <button class="btn" :disabled="productPage <= 1" @click="handleProductPageChange(productPage - 1)">上一页</button>
        <span>{{ productPage }} / {{ productTotalPages }}</span>
        <button class="btn" :disabled="productPage >= productTotalPages" @click="handleProductPageChange(productPage + 1)">下一页</button>
      </section>
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
            <button class="btn" :disabled="!canEditProduct(product)" @click="openEditProduct(product)">编辑</button>
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
          <button class="btn" :disabled="!canDispute(order)" @click="openDispute(order)">纠纷</button>
          <button class="btn" @click="openOrderMessages(order)">留言板</button>
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
        <section v-if="orderTotalPages > 1" class="market-pagination">
          <button class="btn" :disabled="orderPage <= 1" @click="handleOrderPageChange(orderPage - 1)">上一页</button>
          <span>{{ orderPage }} / {{ orderTotalPages }}</span>
          <button class="btn" :disabled="orderPage >= orderTotalPages" @click="handleOrderPageChange(orderPage + 1)">下一页</button>
        </section>
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
              <span>{{ selectedProduct.category || '其他' }}</span>
              <span>{{ selectedProduct.condition || '良好' }}</span>
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
import { computed, onMounted, ref, watch } from 'vue'
import {
  cancelTransaction,
  changeProductStatus,
  confirmReceipt,
  createDispute,
  createProduct,
  createReport,
  createTransaction,
  depositWallet,
  uploadImages,
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
const loadingSales = ref(false)
const submitting = ref(false)
const totalProducts = ref(0)
const totalOrders = ref(0)
const totalSales = ref(0)
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
const detailOpen = ref(false)
const detailLoading = ref(false)
const selectedProduct = ref(null)
const editingProduct = ref(null)
const editingSaving = ref(false)
const productImageFiles = ref([])
const productImagePreviewUrls = ref([])
const editImageUrls = ref([])
const editImageFiles = ref([])
const editImageNewUrls = ref([])
const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/gif', 'image/webp']
const MAX_IMAGE_BYTES = 5 * 1024 * 1024
const MAX_IMAGE_COUNT = 6
const productFilter = ref({
  keyword: '',
  status: '',
  category: '',
  sort: 'latest',
})
const productPage = ref(1)
const productPageSize = ref(10)
const orderPage = ref(1)
const orderPageSize = ref(10)
const salesPage = ref(1)
const salesPageSize = ref(10)
const productTotalPages = computed(() => Math.max(1, Math.ceil(totalProducts.value / productPageSize.value)))
const orderTotalPages = computed(() => Math.max(1, Math.ceil(totalOrders.value / orderPageSize.value)))
const salesTotalPages = computed(() => Math.max(1, Math.ceil(totalSales.value / salesPageSize.value)))

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

const productForm = ref({
  title: '',
  description: '',
  category: '其他',
  condition: '良好',
  price: null,
  stock: 1,
})

const editForm = ref({
  title: '',
  description: '',
  category: '其他',
  condition: '良好',
  price: null,
  stock: 1,
})

const loadProducts = async () => {
  try {
    loading.value = true
    const res = await getProducts({
      keyword: productFilter.value.keyword || undefined,
      status: productFilter.value.status || undefined,
      category: productFilter.value.category || undefined,
      sort: productFilter.value.sort,
      page: productPage.value,
      pageSize: productPageSize.value,
    })
    products.value = res.data
    const totalHeader = res.headers?.['x-total-count'] || res.headers?.['X-Total-Count']
    totalProducts.value = Number.parseInt(totalHeader || '0', 10) || 0
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
    const res = await getMyTransactions({
      page: orderPage.value,
      pageSize: orderPageSize.value,
    })
    orders.value = res.data
    const totalHeader = res.headers?.['x-total-count'] || res.headers?.['X-Total-Count']
    totalOrders.value = Number.parseInt(totalHeader || '0', 10) || 0
  } catch (e) {
    error.value = '无法加载订单: ' + (e.response?.data?.message || e.message)
  } finally {
    loadingOrders.value = false
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
    error.value = '无法加载卖出订单: ' + (e.response?.data?.message || e.message)
  } finally {
    loadingSales.value = false
  }
}

const clearCreateProductImageState = () => {
  productImagePreviewUrls.value.forEach(url => URL.revokeObjectURL(url))
  productImageFiles.value = []
  productImagePreviewUrls.value = []
}

const clearEditProductImageState = () => {
  editImageNewUrls.value.forEach(url => URL.revokeObjectURL(url))
  editImageFiles.value = []
  editImageNewUrls.value = []
  editImageUrls.value = []
}

const resetCreateProductForm = () => {
  clearCreateProductImageState()
  productForm.value = {
    title: '',
    description: '',
    category: '其他',
    condition: '良好',
    price: null,
    stock: 1,
  }
}

const resetProductFilters = () => {
  productFilter.value = {
    keyword: '',
    status: '',
    category: '',
    sort: 'latest',
  }
  productPage.value = 1
  loadProducts()
}

const handleProductPageChange = (nextPage) => {
  const target = Math.min(Math.max(1, nextPage), productTotalPages.value)
  if (target !== productPage.value) {
    productPage.value = target
  }
}

const handleOrderPageChange = (nextPage) => {
  const target = Math.min(Math.max(1, nextPage), orderTotalPages.value)
  if (target !== orderPage.value) {
    orderPage.value = target
  }
}

const handleSalesPageChange = (nextPage) => {
  const target = Math.min(Math.max(1, nextPage), salesTotalPages.value)
  if (target !== salesPage.value) {
    salesPage.value = target
  }
}

const openOrderListTab = () => {
  orderPage.value = 1
  if (activeTab.value === 'orders') {
    loadOrders()
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

const handlePickCreateProductImages = (event) => {
  const files = Array.from(event.target.files || [])
  event.target.value = ''
  if (files.length === 0) return

  const nextCount = productImageFiles.value.length + files.length
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
  productImageFiles.value = [...productImageFiles.value, ...files]
  productImagePreviewUrls.value = [...productImagePreviewUrls.value, ...previewUrls]
}

const removeCreateProductImage = (index) => {
  const removedUrl = productImagePreviewUrls.value[index]
  if (removedUrl) {
    URL.revokeObjectURL(removedUrl)
  }
  productImagePreviewUrls.value.splice(index, 1)
  productImageFiles.value.splice(index, 1)
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

const handleCreateProduct = async () => {
  try {
    submitting.value = true
    error.value = null
    const uploaded = productImageFiles.value.length > 0 ? await uploadImages(productImageFiles.value, 'products') : null
    const imageUrls = (uploaded?.data?.urls || []).slice(0, MAX_IMAGE_COUNT)
    await createProduct({ ...productForm.value, imageUrls })
    resetCreateProductForm()
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
  try {
    error.value = null
    await changeProductStatus(product.productID, { action })
    await Promise.all([loadProducts(), loadMyProducts()])
  } catch (e) {
    error.value = '状态更新失败: ' + (e.response?.data?.message || e.message)
  }
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
  try {
    error.value = null
    await confirmReceipt(order.transactionID)
    await Promise.all([loadOrders(), loadProducts(), loadWallet()])
  } catch (e) {
    error.value = '确认收货失败: ' + (e.response?.data?.message || e.message)
  }
}

const handleCancel = async (order) => {
  try {
    error.value = null
    await cancelTransaction(order.transactionID)
    await Promise.all([loadOrders(), loadProducts()])
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
  await Promise.all([loadOrders(), loadSales()])
}

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
  if (tab === 'all') {
    await loadProducts()
  }
  if (tab === 'my-products') {
    await Promise.all([loadMyProducts(), loadSales()])
  }
  if (tab === 'orders') {
    await loadOrders()
  }
})

watch(
  () => productFilter.value,
  () => {
    productPage.value = 1
    if (activeTab.value === 'all') {
      loadProducts()
    }
  },
  { deep: true },
)

watch(productPage, () => {
  if (activeTab.value === 'all') {
    loadProducts()
  }
})

watch(productPageSize, () => {
  productPage.value = 1
  if (activeTab.value === 'all') {
    loadProducts()
  }
})

watch(orderPage, () => {
  if (activeTab.value === 'orders') {
    loadOrders()
  }
})

watch(orderPageSize, () => {
  orderPage.value = 1
  if (activeTab.value === 'orders') {
    loadOrders()
  }
})

watch(salesPage, () => {
  if (activeTab.value === 'my-products') {
    loadSales()
  }
})

watch(salesPageSize, () => {
  salesPage.value = 1
  if (activeTab.value === 'my-products') {
    loadSales()
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
  background: linear-gradient(180deg, color-mix(in oklab, var(--surface) 94%, #f8fafc 6%), var(--surface));
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1rem;
}

.market-toolbar {
  background: linear-gradient(120deg, rgba(59, 130, 246, 0.08), rgba(14, 165, 233, 0.08));
  border: 1px solid var(--border);
  border-radius: var(--radius);
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 1rem;
}

.toolbar-title {
  align-items: center;
  display: flex;
  justify-content: space-between;
  width: 100%;
}

.toolbar-title h2 {
  font-size: 1rem;
  margin: 0;
}

.market-filter-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 0.625rem;
}

.market-filter-bar input,
.market-filter-bar select {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
  padding: 0.55rem 0.625rem;
}

.market-filter-bar input {
  min-width: 220px;
}

.market-filter-bar select {
  min-width: 150px;
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
.product-form select,
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

.market-hero {
  position: relative;
  isolation: isolate;
  min-height: 330px;
  display: grid;
  grid-template-columns: minmax(0, 1.15fr) minmax(340px, .85fr);
  align-items: center;
  gap: clamp(2rem, 5vw, 5rem);
  overflow: hidden;
  padding: clamp(2rem, 4.5vw, 4.25rem);
  border-radius: 30px;
  color: #fff;
  background: radial-gradient(circle at 12% 0%, rgba(112, 238, 224, .2), transparent 30%), linear-gradient(135deg, #162b43 0%, #263c65 47%, #6252d9 100%);
  box-shadow: 0 28px 65px rgba(31, 44, 91, .2);
}

.market-hero::before { content: ''; position: absolute; inset: 0; z-index: -1; opacity: .14; background-image: linear-gradient(rgba(255,255,255,.17) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,.17) 1px, transparent 1px); background-size: 42px 42px; mask-image: linear-gradient(to right, #000, transparent 75%); }
.market-hero::after { content: ''; position: absolute; z-index: -1; width: 320px; height: 320px; right: -120px; top: -170px; border-radius: 50%; background: rgba(83, 224, 217, .2); }
.market-eyebrow { display: inline-flex; align-items: center; gap: .65rem; color: #a8f0e9; font-size: .68rem; font-weight: 800; letter-spacing: .18em; }
.market-eyebrow i { width: 8px; height: 8px; border-radius: 50%; background: #68f5c8; box-shadow: 0 0 0 6px rgba(104,245,200,.11), 0 0 18px rgba(104,245,200,.7); }
.market-hero h1 { max-width: 690px; margin-top: 1.25rem; font-size: clamp(2.5rem, 4.8vw, 4.7rem); line-height: 1; letter-spacing: -.055em; }
.market-hero h1 span { color: #a8eee9; }
.market-hero-copy > p { max-width: 620px; margin-top: 1.25rem; color: rgba(255,255,255,.68); font-size: .95rem; line-height: 1.8; }
.market-hero-pills { display: flex; flex-wrap: wrap; gap: .5rem; margin-top: 1.5rem; }
.market-hero-pills span { padding: .42rem .72rem; border: 1px solid rgba(255,255,255,.14); border-radius: 999px; background: rgba(255,255,255,.07); color: rgba(255,255,255,.78); font-size: .7rem; }

.market-wallet-card { padding: 1.4rem; border: 1px solid rgba(255,255,255,.17); border-radius: 24px; background: rgba(8,17,38,.35); box-shadow: 0 22px 50px rgba(5,13,33,.25); backdrop-filter: blur(22px); }
.wallet-card-heading { display: flex; align-items: center; justify-content: space-between; color: #9de6df; font-size: .62rem; font-weight: 800; letter-spacing: .14em; }
.wallet-card-icon { width: 38px; height: 38px; display: grid; place-items: center; border-radius: 12px; background: rgba(105,241,222,.12); color: #8ff5df; font-size: 1rem; }
.market-wallet-card > strong { display: block; margin-top: .65rem; font-size: clamp(2.2rem, 4vw, 3.5rem); letter-spacing: -.05em; }
.market-wallet-card > small { color: rgba(255,255,255,.48); font-size: .7rem; }
.market-wallet-card .wallet-actions { display: grid; grid-template-columns: 1fr auto; margin-top: 1.25rem; padding: .35rem; border-radius: 14px; background: rgba(255,255,255,.08); }
.market-wallet-card .wallet-actions input { width: 100%; border: 0; background: transparent; color: #fff; outline: none; }
.market-wallet-card .wallet-actions input::placeholder { color: rgba(255,255,255,.38); }
.market-wallet-card .wallet-actions button { border: 0; border-radius: 10px; padding: .7rem 1rem; color: #25324f; background: #fff; font-weight: 750; cursor: pointer; }
.market-wallet-card .wallet-actions button:disabled { opacity: .55; cursor: not-allowed; }
.wallet-card-stats { display: grid; grid-template-columns: repeat(2, 1fr); gap: .55rem; margin-top: .85rem; }
.wallet-card-stats > span { display: flex; flex-direction: column; padding: .75rem; border-radius: 13px; background: rgba(255,255,255,.065); }
.wallet-card-stats b { font-size: .9rem; }
.wallet-card-stats small { margin-top: .15rem; color: rgba(255,255,255,.42); font-size: .62rem; }

.market-nav { display: flex; align-items: center; justify-content: space-between; gap: 1rem; margin: 1.25rem 0; padding: .55rem; border: 1px solid rgba(24,32,55,.08); border-radius: 18px; background: #fff; box-shadow: 0 10px 30px rgba(28,34,64,.05); }
.market-nav .tabs { gap: .35rem; }
.market-nav .tab { border: 0; border-radius: 12px; padding: .72rem 1.05rem; color: #70778a; background: transparent; font-weight: 700; }
.market-nav .tab.active { color: #fff; background: linear-gradient(135deg, #5f50dc, #7967f3); box-shadow: 0 8px 20px rgba(95,80,220,.22); }
.market-nav-note { padding-right: .8rem; color: #9a9fb0; font-size: .72rem; }

.product-page .market-layout { gap: 1.1rem; }
.product-page .market-toolbar, .product-page .product-form, .product-page .orders-panel, .product-page .dispute-form { border: 1px solid rgba(25,34,59,.08); border-radius: 22px; background: #fff; box-shadow: 0 12px 35px rgba(29,35,58,.05); }
.product-page .market-toolbar { padding: 1.25rem; background: linear-gradient(135deg, #f2f0ff 0%, #f0fbfa 100%); }
.product-page .toolbar-title h2, .product-page .product-form h2, .product-page .orders-panel h2 { font-size: 1.15rem; letter-spacing: -.025em; }
.product-page .market-filter-bar { display: grid; grid-template-columns: minmax(220px, 1.5fr) repeat(3, minmax(135px, .65fr)) auto; }
.product-page .market-filter-bar input, .product-page .market-filter-bar select, .product-page .product-form input, .product-page .product-form select, .product-page .product-form textarea, .product-page .product-edit-form input, .product-page .product-edit-form select, .product-page .product-edit-form textarea, .product-page .dispute-form textarea, .product-page .message-form input { border: 1px solid #e3e5ed; border-radius: 12px; background: #fff; transition: border-color .2s, box-shadow .2s; }
.product-page :is(input, select, textarea):focus { border-color: #7463ee; outline: none; box-shadow: 0 0 0 4px rgba(105,87,245,.1); }
.product-page .product-form { position: relative; overflow: hidden; padding: 1.35rem; }
.product-page .product-form::after { content: ''; position: absolute; right: -55px; top: -65px; width: 150px; height: 150px; border-radius: 50%; background: #ddf7f4; pointer-events: none; }
.product-page .product-form > * { position: relative; z-index: 1; }
.product-page .product-form h2::before { content: '＋'; display: inline-grid; place-items: center; width: 28px; height: 28px; margin-right: .55rem; border-radius: 9px; color: #fff; background: #6654ee; font-size: .9rem; }
.product-page .form-row { align-items: stretch; }
.product-page .form-row > * { flex: 1 1 130px; }

.product-page .product-grid { grid-template-columns: repeat(auto-fill, minmax(285px, 1fr)); gap: 1rem; }
.product-page .product-card { position: relative; overflow: hidden; gap: .85rem; padding: 1.15rem; border: 1px solid rgba(25,34,59,.08); border-radius: 22px; background: #fff; box-shadow: 0 12px 32px rgba(29,35,58,.055); transition: transform .25s, box-shadow .25s, border-color .25s; }
.product-page .product-card:hover { transform: translateY(-5px); border-color: rgba(102,84,238,.22); box-shadow: 0 22px 48px rgba(45,39,99,.12); }
.product-page .product-head h2 { font-size: 1.05rem; letter-spacing: -.02em; }
.product-page .product-images { overflow: hidden; border-radius: 15px; background: #f2f4f7; }
.product-page .product-card .product-images img:first-child:last-child { grid-column: 1 / -1; aspect-ratio: 16 / 10; }
.product-page .product-images img { border: 0; border-radius: 0; }
.product-page .product-card > p { min-height: 2.8em; margin: 0; line-height: 1.55; }
.product-page .product-meta { gap: .45rem; }
.product-page .product-meta span { padding: .28rem .5rem; border-radius: 999px; background: #f4f5f8; font-size: .7rem; }
.product-page .product-meta strong { width: 100%; color: #5d4dd7; font-size: 1.45rem; letter-spacing: -.04em; }
.product-page .product-card > .btn { min-height: 42px; border-radius: 12px; }
.product-page .product-card > .btn-primary { background: linear-gradient(135deg, #5f50dc, #7563ef); }
.product-page .product-card > .link-button { align-self: center; font-size: .72rem; }

.product-page .product-row, .product-page .order-row { align-items: center; padding: 1.15rem 1.25rem; border: 1px solid rgba(25,34,59,.08); border-radius: 18px; background: #fff; box-shadow: 0 8px 25px rgba(29,35,58,.04); }
.product-page .row-actions .btn { border-radius: 10px; }
.product-page .market-pagination { padding: 1rem; border-radius: 16px; background: #fff; }
.product-page .empty-state { border: 1px dashed #d9dbe5; border-radius: 20px; background: #fafaff; }
.product-page .order-message { border-radius: 14px; background: #f8f8fc; }

.product-page .detail-backdrop { background: rgba(15,18,38,.55); backdrop-filter: blur(5px); }
.product-page .product-detail-panel { padding: 1.25rem; border-left: 0; background: #f6f6fb; box-shadow: -24px 0 60px rgba(12,16,35,.24); }
.product-page .detail-header, .product-page .product-detail { border: 1px solid rgba(25,34,59,.08); border-radius: 20px; box-shadow: 0 10px 30px rgba(29,35,58,.05); }
.product-page .product-detail .product-head strong { color: #5d4dd7; font-size: 1.6rem; }

@media (max-width: 1120px) {
  .product-page .market-filter-bar { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .product-page .market-filter-bar input { min-width: 0; }
}
@media (max-width: 820px) {
  .market-hero { grid-template-columns: 1fr; padding: 2rem; border-radius: 24px; }
  .market-wallet-card { max-width: 620px; }
  .product-page .product-row, .product-page .order-row { align-items: stretch; flex-direction: column; }
}
@media (max-width: 640px) {
  .market-hero { min-height: auto; gap: 1.5rem; padding: 1.45rem; border-radius: 20px; }
  .market-hero h1 { font-size: 2.05rem; }
  .market-hero-copy > p { font-size: .86rem; }
  .market-wallet-card { padding: 1rem; border-radius: 18px; }
  .market-wallet-card .wallet-actions { grid-template-columns: 1fr; }
  .market-nav { align-items: stretch; overflow-x: auto; }
  .market-nav .tabs { flex-wrap: nowrap; }
  .market-nav .tab { white-space: nowrap; }
  .market-nav-note { display: none; }
  .product-page .market-filter-bar { grid-template-columns: 1fr; }
  .product-page .market-filter-bar select { min-width: 0; width: 100%; }
  .product-page .product-grid { grid-template-columns: 1fr; }
  .product-page .product-card { border-radius: 18px; }
  .product-page .row-actions { display: grid; grid-template-columns: repeat(2, 1fr); }
  .product-page .row-actions .btn { width: 100%; }
}
</style>

<style scoped>
/* Marketplace hierarchy */
.product-page .market-toolbar { padding:1.15rem; border:1px solid var(--border); border-radius:14px; background:#fff; box-shadow:none; }
.toolbar-title { margin-bottom:1rem; }
.toolbar-title h2 { color:#171d2e; font-size:1.05rem; }
.product-page .market-filter-bar { display:grid; grid-template-columns:minmax(260px,1fr) repeat(3,minmax(120px,.42fr)) auto auto; gap:.6rem; }
.market-search-field { min-width:0; display:flex; align-items:center; gap:.55rem; padding:0 .75rem; border:1px solid #dfe2e8; border-radius:9px; background:#f8f9fb; }
.market-search-field:focus-within { border-color:#7463ee; background:#fff; box-shadow:0 0 0 3px rgba(105,87,245,.09); }
.market-search-field svg { width:17px; height:17px; flex:0 0 auto; color:#8b93a3; }
.product-page .market-search-field input { width:100%; padding:.68rem 0; border:0; border-radius:0; outline:0; background:transparent; box-shadow:none; }
.product-page .market-search-field input:focus { border:0; box-shadow:none; }
.product-page .market-filter-bar select { min-width:0; padding:.68rem .7rem; border:1px solid #dfe2e8; border-radius:9px; background:#fff; }
.market-search-submit, .market-reset { padding:.68rem .9rem; border:0; border-radius:9px; font:inherit; font-size:.8rem; font-weight:650; cursor:pointer; }
.market-search-submit { color:#fff; background:#2c3344; }
.market-search-submit:hover { background:#171d2e; }
.market-reset { color:#626b7d; background:#f0f2f5; }
.product-create-panel { overflow:hidden; margin-top:.75rem; border:1px solid var(--border); border-radius:14px; background:#fff; }
.product-create-panel summary { display:flex; align-items:center; justify-content:space-between; gap:1rem; padding:1rem 1.15rem; cursor:pointer; list-style:none; }
.product-create-panel summary::-webkit-details-marker { display:none; }
.product-create-panel summary span { display:flex; flex-direction:column; }
.product-create-panel summary strong { color:#252c3d; font-size:.9rem; }
.product-create-panel summary small { margin-top:.15rem; color:var(--text-secondary); font-size:.72rem; }
.product-create-panel summary b { width:28px; height:28px; display:grid; place-items:center; border-radius:8px; color:var(--primary); background:#f0effc; font-size:1rem; transition:transform .2s; }
.product-create-panel[open] summary { border-bottom:1px solid var(--border); }
.product-create-panel[open] summary b { transform:rotate(45deg); }
.product-page .product-create-panel .product-form { margin:0; padding:1.15rem; border:0; border-radius:0; box-shadow:none; background:#fff; }
.product-form-field { display:flex; flex-direction:column; gap:.4rem; color:#4c5567; font-size:.76rem; font-weight:650; }
.product-page .product-form-field :is(input,textarea) { width:100%; font-weight:400; }
.product-page .product-card { padding:1rem; border:1px solid var(--border); border-radius:12px; box-shadow:none; }
.product-page .product-card:hover { transform:none; border-color:#c8c4ee; box-shadow:0 5px 18px rgba(31,35,55,.055); }
.product-page .product-card h2 { font-size:1rem; }
.product-page .product-meta strong { color:#3f35a4; font-size:1.08rem; }
.product-card-actions { display:grid; grid-template-columns:1fr 1fr auto; align-items:center; gap:.5rem; margin-top:auto; padding-top:.8rem; border-top:1px solid var(--border); }
.product-card-actions .btn { justify-content:center; }
.product-card-actions .link-button { padding:.45rem; font-size:.74rem; }
@media(max-width:1100px){.product-page .market-filter-bar{grid-template-columns:minmax(240px,1fr) repeat(2,minmax(120px,.45fr))}.product-page .market-filter-bar select:nth-of-type(3){grid-column:2/3}.market-search-submit,.market-reset{grid-row:2}}
@media(max-width:700px){.product-page .market-filter-bar{grid-template-columns:1fr 1fr}.market-search-field{grid-column:1/-1}.product-page .market-filter-bar select{width:100%}.product-page .market-filter-bar select:nth-of-type(3){grid-column:1/-1}.market-search-submit,.market-reset{grid-row:auto}.product-card-actions{grid-template-columns:1fr 1fr}.product-card-actions .link-button{grid-column:1/-1}}
</style>
