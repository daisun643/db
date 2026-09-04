<template>
  <div v-if="product" class="detail-backdrop" @click.self="$emit('close')">
    <form class="product-detail-panel product-edit-form" @submit.prevent="handleUpdateProduct">
      <div class="detail-header">
        <button class="link-button" type="button" @click="$emit('close')">取消编辑</button>
        <span class="muted">商品 #{{ product.productID }}</span>
      </div>
      <h2>编辑商品</h2>
      <input v-model="editForm.title" type="text" placeholder="商品标题" required />
      <textarea v-model="editForm.description" placeholder="商品描述"></textarea>
      <input
        type="file"
        multiple
        accept="image/jpeg,image/png,image/gif,image/webp"
        :disabled="saving"
        @change="handlePickProductImages"
      />
      <div v-if="productImageList.length" class="product-images product-image-preview">
        <div v-for="(item, index) in productImageList" :key="`${item.source}-${index}`" class="image-preview-item">
          <img :src="item.url" :alt="`图片 ${index + 1}`" loading="lazy" />
          <button type="button" class="image-remove" @click="removeProductImage(index)">移除</button>
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
      <button class="btn btn-primary" type="submit" :disabled="saving">
        {{ saving ? '保存中...' : '保存商品' }}
      </button>
    </form>
  </div>
</template>

<script setup>
import { computed, ref, watch } from 'vue'
import { uploadImages, updateProduct } from '../../api'

const props = defineProps({
  product: { type: Object, default: null },
})

const emit = defineEmits(['close', 'saved', 'error'])

const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/gif', 'image/webp']
const MAX_IMAGE_BYTES = 5 * 1024 * 1024
const MAX_IMAGE_COUNT = 6

const saving = ref(false)
const imageUrls = ref([])
const imageFiles = ref([])
const imageNewUrls = ref([])

const editForm = ref({
  title: '',
  description: '',
  category: '其他',
  condition: '良好',
  price: null,
  stock: 1,
})

const productImageList = computed(() => {
  const remote = imageUrls.value.map((url, index) => ({
    source: 'remote',
    key: `remote-${index}`,
    url,
  }))
  const local = imageNewUrls.value.map((url, index) => ({
    source: 'local',
    key: `local-${index}`,
    url,
  }))
  return [...remote, ...local]
})

const clearImageState = () => {
  imageNewUrls.value.forEach(url => URL.revokeObjectURL(url))
  imageFiles.value = []
  imageNewUrls.value = []
  imageUrls.value = []
}

// 打开抽屉时以目标商品初始化表单；关闭时释放本地预览 URL
watch(
  () => props.product,
  (product) => {
    clearImageState()
    if (!product) return

    editForm.value = {
      title: product.title || '',
      description: product.description || '',
      category: product.category || '其他',
      condition: product.condition || '良好',
      price: product.price,
      stock: product.stock || 1,
    }
    imageUrls.value = [...(product.imageUrls || [])]
  },
)

const validateImageFile = (file) => {
  if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
    return '仅支持 JPG、PNG、GIF、WebP 图片'
  }
  if (file.size > MAX_IMAGE_BYTES) {
    return '单张图片不能超过5MB'
  }
  return null
}

const handlePickProductImages = (event) => {
  const files = Array.from(event.target.files || [])
  event.target.value = ''
  if (files.length === 0) return

  const nextCount = imageUrls.value.length + imageNewUrls.value.length + files.length
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
  imageFiles.value = [...imageFiles.value, ...files]
  imageNewUrls.value = [...imageNewUrls.value, ...previewUrls]
}

const removeProductImage = (index) => {
  const remoteCount = imageUrls.value.length
  if (index < remoteCount) {
    imageUrls.value = imageUrls.value.filter((_, i) => i !== index)
    return
  }

  const localIndex = index - remoteCount
  const removedUrl = imageNewUrls.value[localIndex]
  if (removedUrl) {
    URL.revokeObjectURL(removedUrl)
  }
  imageNewUrls.value.splice(localIndex, 1)
  imageFiles.value.splice(localIndex, 1)
}

const handleUpdateProduct = async () => {
  try {
    saving.value = true
    const uploaded = imageFiles.value.length > 0 ? await uploadImages(imageFiles.value, 'products') : null
    const mergedImageUrls = [...imageUrls.value, ...(uploaded?.data?.urls || [])].slice(0, MAX_IMAGE_COUNT)
    await updateProduct(props.product.productID, {
      ...editForm.value,
      imageUrls: mergedImageUrls,
    })
    emit('saved')
  } catch (e) {
    emit('error', '保存失败: ' + (e.response?.data?.message || e.message))
  } finally {
    saving.value = false
  }
}
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

.detail-header {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1rem;
  align-items: center;
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1rem;
}

.product-edit-form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.product-edit-form h2 {
  font-size: 1.25rem;
}

.product-edit-form input,
.product-edit-form select,
.product-edit-form textarea {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
  padding: 0.625rem 0.75rem;
}

.product-edit-form textarea {
  min-height: 96px;
  resize: vertical;
}

.form-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.form-row input {
  min-width: 120px;
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
/* Marketplace theme */
.detail-backdrop { background: rgba(15,18,38,.55); backdrop-filter: blur(5px); }
.product-detail-panel { padding: 1.25rem; border-left: 0; background: #f6f6fb; box-shadow: -24px 0 60px rgba(12,16,35,.24); }
.detail-header { border: 1px solid rgba(25,34,59,.08); border-radius: 20px; box-shadow: 0 10px 30px rgba(29,35,58,.05); }

.product-edit-form input, .product-edit-form select, .product-edit-form textarea { border: 1px solid #e3e5ed; border-radius: 12px; background: #fff; transition: border-color .2s, box-shadow .2s; }
.product-edit-form :is(input, select, textarea):focus { border-color: #7463ee; outline: none; box-shadow: 0 0 0 4px rgba(105,87,245,.1); }
.product-edit-form .form-row { align-items: stretch; }
.product-edit-form .form-row > * { flex: 1 1 130px; }

.product-images { overflow: hidden; border-radius: 15px; background: #f2f4f7; }
.product-images img { border: 0; border-radius: 0; }
</style>
