<template>
  <div v-if="open" class="detail-backdrop" @click.self="handleClose">
    <form class="post-detail-panel composer-modal" @submit.prevent="handleUpdatePost">
      <div class="modal-header">
        <button class="icon-button" type="button" @click="handleClose" aria-label="关闭编辑窗口">
          <span>×</span>
        </button>
        <button class="compose-submit" type="submit" :disabled="editingSaving">
          {{ editingSaving ? '保存中...' : '保存' }}
        </button>
      </div>
      <div class="composer-shell">
        <div class="composer-avatar">{{ userInitial }}</div>
        <div class="composer-fields">
          <input v-model="editForm.title" type="text" placeholder="帖子标题" required />
          <textarea v-model="editForm.content" placeholder="帖子内容" required></textarea>
          <p class="field-hint">支持 Markdown：标题、列表、引用、链接、代码块等。</p>
          <div class="composer-row">
            <input v-model="editTagText" type="text" placeholder="标签，用逗号分隔" />
            <input
              type="file"
              multiple
              accept="image/jpeg,image/png,image/gif,image/webp"
              :disabled="editingSaving"
              @change="handlePickEditImages"
            />
          </div>
          <div v-if="combinedEditImageList.length" class="image-strip">
            <div v-for="(item, index) in combinedEditImageList" :key="`${item.source}-${index}`" class="image-preview-item">
              <img :src="item.url" :alt="`图片 ${index + 1}`" loading="lazy" />
              <button type="button" class="image-remove" @click="removeEditImage(index)">移除</button>
            </div>
          </div>
          <p v-if="combinedEditImageList.length" class="field-hint">最多 6 张，编辑时新老图片会按列表顺序提交。</p>
        </div>
      </div>
    </form>
  </div>
</template>

<script setup>
import { computed, ref, watch } from 'vue'
import { useAuthStore } from '../../stores/auth'
import { updatePost, uploadImages } from '../../api'

const props = defineProps({
  open: { type: Boolean, required: true },
  post: { type: Object, default: null },
})

const emit = defineEmits(['close', 'saved', 'error'])

const authStore = useAuthStore()
const userInitial = computed(() => (authStore.user?.username || '用')[0]?.toUpperCase() || '用')

const editForm = ref({ title: '', content: '' })
const editTagText = ref('')
const editImageUrls = ref([])
const editImageNewUrls = ref([])
const editImageFiles = ref([])
const editingSaving = ref(false)

const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/gif', 'image/webp']
const MAX_IMAGE_BYTES = 5 * 1024 * 1024
const MAX_IMAGE_COUNT = 6

const combinedEditImageList = computed(() => {
  const fromRemote = editImageUrls.value.map((url, index) => ({
    source: 'remote',
    key: `remote-${index}`,
    url,
  }))
  const fromLocal = editImageNewUrls.value.map((url, index) => ({
    source: 'local',
    key: `local-${index}`,
    url,
  }))
  return [...fromRemote, ...fromLocal]
})

watch(() => props.post, (post) => {
  if (post) {
    editForm.value = {
      title: post.title || '',
      content: post.content || '',
    }
    editTagText.value = (post.tags || []).join(', ')
    editImageUrls.value = [...(post.imageUrls || [])]
  }
}, { immediate: true })

watch(() => props.open, (isOpen) => {
  if (!isOpen) {
    clearEditImageState()
  }
})

const clearEditImageState = () => {
  editImageNewUrls.value.forEach(url => URL.revokeObjectURL(url))
  editImageFiles.value = []
  editImageNewUrls.value = []
  editImageUrls.value = []
}

const handleClose = () => {
  if (editingSaving.value) return
  emit('close')
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

const handlePickEditImages = (event) => {
  const files = Array.from(event.target.files || [])
  event.target.value = ''
  if (files.length === 0) return

  const nextCount = editImageUrls.value.length + editImageNewUrls.value.length + files.length
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

  const addedUrls = files.map(file => URL.createObjectURL(file))
  editImageFiles.value = [...editImageFiles.value, ...files]
  editImageNewUrls.value = [...editImageNewUrls.value, ...addedUrls]
}

const removeEditImage = (index) => {
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

const handleUpdatePost = async () => {
  if (!props.post) return

  try {
    editingSaving.value = true
    const tagNames = editTagText.value.split(/[,，]/).map(tag => tag.trim()).filter(Boolean)
    const uploaded = editImageFiles.value.length > 0
      ? await uploadImages(editImageFiles.value, 'posts')
      : null
    const imageUrls = [
      ...editImageUrls.value,
      ...(uploaded?.data?.urls || []),
    ].slice(0, MAX_IMAGE_COUNT)

    const res = await updatePost(props.post.postID, {
      ...editForm.value,
      tagNames,
      imageUrls,
    })
    emit('saved', res.data)
    clearEditImageState()
    emit('close')
  } catch (e) {
    emit('error', '保存失败: ' + (e.response?.data?.message || e.message))
  } finally {
    editingSaving.value = false
  }
}
</script>

<style scoped>
.detail-backdrop {
  align-items: flex-start;
  background: rgba(91, 112, 131, 0.4);
  bottom: 0;
  display: flex;
  justify-content: center;
  left: 0;
  padding: 3rem 1rem;
  position: fixed;
  right: 0;
  top: 0;
  z-index: 1200;
}

.post-detail-panel {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 16px;
  box-shadow: 0 20px 60px rgba(15, 23, 42, 0.24);
  max-height: calc(100vh - 6rem);
  max-width: 680px;
  overflow-y: auto;
  padding: 0;
  width: min(680px, 100vw);
}

.composer-modal {
  max-width: 620px;
}

.modal-header {
  align-items: center;
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  position: sticky;
  top: 0;
  z-index: 1;
  border-bottom: 1px solid var(--border);
  padding: 1rem;
  background: var(--surface);
}

.icon-button {
  align-items: center;
  background: transparent;
  border: none;
  border-radius: 50%;
  color: #0f1419;
  cursor: pointer;
  display: inline-flex;
  font: inherit;
  font-size: 1.5rem;
  height: 36px;
  justify-content: center;
  line-height: 1;
  width: 36px;
}

.icon-button:hover {
  background: #eff3f4;
}

.composer-shell {
  display: flex;
  gap: 0.75rem;
  padding: 1rem;
}

.composer-avatar {
  align-items: center;
  background: #1d9bf0;
  border-radius: 50%;
  color: white;
  display: flex;
  flex: 0 0 44px;
  font-weight: 800;
  height: 44px;
  justify-content: center;
  width: 44px;
}

.composer-fields {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 0.75rem;
  min-width: 0;
}

.composer-fields input,
.composer-fields textarea {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
  padding: 0.55rem 0.625rem;
  width: 100%;
}

.composer-fields textarea {
  min-height: 160px;
  resize: vertical;
}

.composer-row {
  display: flex;
  gap: 0.75rem;
  align-items: center;
}

.field-hint {
  color: var(--text-secondary);
  font-size: 0.75rem;
  margin: 0;
}

.image-strip {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 0.5rem;
  margin: 0.75rem 0;
}

.image-strip img {
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

.compose-submit {
  align-items: center;
  background: #0f1419;
  border: none;
  border-radius: 9999px;
  color: white;
  cursor: pointer;
  display: inline-flex;
  font: inherit;
  font-weight: 700;
  justify-content: center;
  min-height: 38px;
  padding: 0.625rem 1.25rem;
  white-space: nowrap;
}

.compose-submit:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

@media (max-width: 900px) {
  .detail-backdrop {
    display: block;
    padding: 0;
  }

  .post-detail-panel {
    border: none;
    border-radius: 0;
    max-height: 100vh;
    width: 100vw;
  }

  .composer-row {
    align-items: stretch;
    flex-direction: column;
  }
}
</style>
