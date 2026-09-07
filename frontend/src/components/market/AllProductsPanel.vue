<template>
  <div class="market-layout">
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
    <div v-else-if="products.length === 0" class="empty-state">
      <p>暂无商品</p>
    </div>
    <div v-else class="product-grid">
      <article v-for="product in products" :key="product.productID" class="product-card">
        <div class="product-head">
          <h2>{{ product.title }}</h2>
          <span :class="['badge', product.status === 'Active' ? 'badge-green' : 'badge-yellow']">
            {{ statusText(product.status) }}
          </span>
        </div>
        <div v-if="product.imageUrls?.length" class="product-images">
          <img :src="product.imageUrls[0]" :alt="product.title" loading="lazy" />
          <span v-if="product.imageUrls.length > 1" class="image-count">{{ product.imageUrls.length }} 图</span>
        </div>
        <p>{{ product.description || '暂无描述' }}</p>
        <div class="product-meta">
          <strong>¥{{ product.price }}</strong>
          <span>库存 {{ product.stock }}</span>
          <span>{{ product.category || '其他' }}</span>
          <span>{{ product.condition || '良好' }}</span>
          <span
            class="seller-link"
            :title="product.userID ? '查看卖家主页' : ''"
            @click.stop="openSellerHome(product)"
          >{{ product.sellerName || '匿名卖家' }}</span>
        </div>
        <div class="product-card-actions">
          <button class="btn" @click="$emit('open-detail', product)">查看详情</button>
          <button
            class="btn btn-primary"
            :disabled="product.status !== 'Active' || product.stock <= 0"
            @click="$emit('order', product)"
          >
            下单锁定
          </button>
          <button class="link-button danger" @click="$emit('report', product)">举报</button>
        </div>
      </article>
    </div>

    <section v-if="productTotalPages > 1" class="market-pagination">
      <button class="btn" :disabled="productPage <= 1" @click="handleProductPageChange(productPage - 1)">上一页</button>
      <span>{{ productPage }} / {{ productTotalPages }}</span>
      <button class="btn" :disabled="productPage >= productTotalPages" @click="handleProductPageChange(productPage + 1)">下一页</button>
    </section>
  </div>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { createProduct, getProducts, uploadImages } from '../../api'

const emit = defineEmits(['open-detail', 'order', 'report', 'error', 'products-changed'])

const router = useRouter()

const openSellerHome = (product) => {
  if (product.userID) {
    router.push(`/user/${product.userID}`)
  }
}

const products = ref([])
const loading = ref(true)
const submitting = ref(false)
const totalProducts = ref(0)
const productImageFiles = ref([])
const productImagePreviewUrls = ref([])
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
const productTotalPages = computed(() => Math.max(1, Math.ceil(totalProducts.value / productPageSize.value)))

const productForm = ref({
  title: '',
  description: '',
  category: '其他',
  condition: '良好',
  price: null,
  stock: 1,
})

const statusText = (status) => ({
  Active: '发布中',
  Locked: '已锁定',
  Sold: '已售出',
  Inactive: '已下架',
}[status] || status || '未知')

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
    products.value = []
    totalProducts.value = 0
    emit('error', '无法加载商品数据: ' + (e.response?.data?.message || e.message))
  } finally {
    loading.value = false
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

const validateImageFile = (file) => {
  if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
    return '仅支持 JPG、PNG、GIF、WebP 图片'
  }
  if (file.size > MAX_IMAGE_BYTES) {
    return '单张图片不能超过5MB'
  }
  return null
}

const clearCreateProductImageState = () => {
  productImagePreviewUrls.value.forEach(url => URL.revokeObjectURL(url))
  productImageFiles.value = []
  productImagePreviewUrls.value = []
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

const handlePickCreateProductImages = (event) => {
  const files = Array.from(event.target.files || [])
  event.target.value = ''
  if (files.length === 0) return

  const nextCount = productImageFiles.value.length + files.length
  if (nextCount > MAX_IMAGE_COUNT) {
    emit('error', `图片数量不能超过 ${MAX_IMAGE_COUNT} 张`)
    return
  }

  for (const file of files) {
    const message = validateImageFile(file)
    if (message) {
      emit('error', message)
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

const handleCreateProduct = async () => {
  try {
    submitting.value = true
    const uploaded = productImageFiles.value.length > 0 ? await uploadImages(productImageFiles.value, 'products') : null
    const imageUrls = (uploaded?.data?.urls || []).slice(0, MAX_IMAGE_COUNT)
    await createProduct({ ...productForm.value, imageUrls })
    resetCreateProductForm()
    await loadProducts()
    emit('products-changed')
  } catch (e) {
    emit('error', '发布失败: ' + (e.response?.data?.message || e.message))
  } finally {
    submitting.value = false
  }
}

watch(
  () => productFilter.value,
  () => {
    productPage.value = 1
    loadProducts()
  },
  { deep: true },
)

watch(productPage, () => {
  loadProducts()
})

watch(productPageSize, () => {
  productPage.value = 1
  loadProducts()
})

defineExpose({ reload: loadProducts })

onMounted(() => {
  loadProducts()
})
</script>

<style scoped>
.market-layout {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.product-form {
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

.product-form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.product-form input,
.product-form select,
.product-form textarea {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
  padding: 0.625rem 0.75rem;
}

.product-form textarea {
  min-height: 96px;
  resize: vertical;
}

.form-row,
.product-meta {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.form-row input {
  min-width: 120px;
}

.product-grid {
  column-gap: 1rem;
  column-width: 270px;
}

.product-card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1rem;
}

.product-card {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-bottom: 1rem;
  break-inside: avoid;
}

.product-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.75rem;
}

.product-images {
  position: relative;
}

.product-images img {
  aspect-ratio: 16 / 10;
  background: var(--bg);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  object-fit: cover;
  width: 100%;
}

.image-count {
  align-items: center;
  background: rgba(17, 24, 43, 0.66);
  border-radius: 999px;
  bottom: 0.5rem;
  color: #fff;
  display: inline-flex;
  font-size: 0.7rem;
  padding: 0.125rem 0.5rem;
  position: absolute;
  right: 0.5rem;
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

.product-head h2 {
  font-size: 1rem;
}

.product-card p {
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

.muted {
  color: var(--text-secondary);
  font-size: 0.875rem;
}

.empty-state {
  border: 1px dashed #d9dbe5;
  border-radius: 20px;
  background: #fafaff;
}

@media (max-width: 760px) {
  .form-row {
    align-items: stretch;
    flex-direction: column;
  }
}
</style>

<style scoped>
/* Marketplace theme */
.market-layout { gap: 1.1rem; }
.market-toolbar, .product-form { border: 1px solid rgba(25,34,59,.08); border-radius: 22px; background: #fff; box-shadow: 0 12px 35px rgba(29,35,58,.05); }
.toolbar-title h2 { letter-spacing: -.025em; }
.market-filter-bar input, .market-filter-bar select, .product-form input, .product-form select, .product-form textarea { border: 1px solid #e3e5ed; border-radius: 12px; background: #fff; transition: border-color .2s, box-shadow .2s; }
:is(input, select, textarea):focus { border-color: #7463ee; outline: none; box-shadow: 0 0 0 4px rgba(105,87,245,.1); }
.product-form { position: relative; overflow: hidden; padding: 1.35rem; }
.product-form::after { content: ''; position: absolute; right: -55px; top: -65px; width: 150px; height: 150px; border-radius: 50%; background: #ddf7f4; pointer-events: none; }
.product-form > * { position: relative; z-index: 1; }
.form-row { align-items: stretch; }
.form-row > * { flex: 1 1 130px; }

.product-grid { column-width: 285px; column-gap: 1rem; }
.product-card { position: relative; overflow: hidden; gap: .85rem; background: #fff; transition: transform .25s, box-shadow .25s, border-color .25s; }
.product-head h2 { font-size: 1.05rem; letter-spacing: -.02em; }
.product-images { overflow: hidden; border-radius: 15px; background: #f2f4f7; }
.product-images img { border: 0; border-radius: 0; }
.product-card > p { min-height: 2.8em; margin: 0; line-height: 1.55; }
.product-meta { gap: .45rem; }
.product-meta span { padding: .28rem .5rem; border-radius: 999px; background: #f4f5f8; font-size: .7rem; }
.product-meta .seller-link { color: #5d4fd5; cursor: pointer; }
.product-meta .seller-link:hover { text-decoration: underline; }
.product-meta strong { width: 100%; letter-spacing: -.04em; }
.product-card > .btn { min-height: 42px; border-radius: 12px; }
.product-card > .btn-primary { background: linear-gradient(135deg, #5f50dc, #7563ef); }
.product-card > .link-button { align-self: center; font-size: .72rem; }

.market-pagination { padding: 1rem; border-radius: 16px; background: #fff; }

@media (max-width: 1120px) {
  .market-filter-bar { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .market-filter-bar input { min-width: 0; }
}
@media (max-width: 640px) {
  .market-filter-bar { grid-template-columns: 1fr; }
  .market-filter-bar select { min-width: 0; width: 100%; }
  .product-card { border-radius: 18px; }
}
</style>

<style scoped>
/* Marketplace hierarchy */
.market-toolbar { padding:1.15rem; border:1px solid var(--border); border-radius:14px; background:#fff; box-shadow:none; }
.toolbar-title { margin-bottom:1rem; }
.toolbar-title h2 { color:#171d2e; font-size:1.05rem; }
.market-filter-bar { display:grid; grid-template-columns:minmax(260px,1fr) repeat(3,minmax(120px,.42fr)) auto auto; gap:.6rem; }
.market-search-field { min-width:0; display:flex; align-items:center; gap:.55rem; padding:0 .75rem; border:1px solid #dfe2e8; border-radius:9px; background:#f8f9fb; }
.market-search-field:focus-within { border-color:#7463ee; background:#fff; box-shadow:0 0 0 3px rgba(105,87,245,.09); }
.market-search-field svg { width:17px; height:17px; flex:0 0 auto; color:#8b93a3; }
.market-search-field input { width:100%; padding:.68rem 0; border:0; border-radius:0; outline:0; background:transparent; box-shadow:none; }
.market-search-field input:focus { border:0; box-shadow:none; }
.market-filter-bar select { min-width:0; padding:.68rem .7rem; border:1px solid #dfe2e8; border-radius:9px; background:#fff; }
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
.product-create-panel .product-form { margin:0; padding:1.15rem; border:0; border-radius:0; box-shadow:none; background:#fff; }
.product-form-field { display:flex; flex-direction:column; gap:.4rem; color:#4c5567; font-size:.76rem; font-weight:650; }
.product-form-field :is(input,textarea) { width:100%; font-weight:400; }
.product-card { padding:1rem; border:1px solid var(--border); border-radius:12px; box-shadow:none; }
.product-card:hover { transform:none; border-color:#c8c4ee; box-shadow:0 5px 18px rgba(31,35,55,.055); }
.product-card h2 { font-size:1rem; }
.product-meta strong { color:#3f35a4; font-size:1.08rem; }
.product-card-actions { display:grid; grid-template-columns:1fr 1fr auto; align-items:center; gap:.5rem; margin-top:auto; padding-top:.8rem; border-top:1px solid var(--border); }
.product-card-actions .btn { justify-content:center; }
.product-card-actions .link-button { padding:.45rem; font-size:.74rem; }
@media(max-width:1100px){.market-filter-bar{grid-template-columns:minmax(240px,1fr) repeat(2,minmax(120px,.45fr))}.market-filter-bar select:nth-of-type(3){grid-column:2/3}.market-search-submit,.market-reset{grid-row:2}}
@media(max-width:700px){.market-filter-bar{grid-template-columns:1fr 1fr}.market-search-field{grid-column:1/-1}.market-filter-bar select{width:100%}.market-filter-bar select:nth-of-type(3){grid-column:1/-1}.market-search-submit,.market-reset{grid-row:auto}.product-card-actions{grid-template-columns:1fr 1fr}.product-card-actions .link-button{grid-column:1/-1}}
</style>
