<template>
  <div v-if="open" class="detail-backdrop" @click.self="handleClose">
    <form class="post-detail-panel composer-modal" @submit.prevent="handleCreatePost">
      <div class="composer-header">
        <div>
          <span class="composer-kicker">新建帖子</span>
          <h2>分享你的想法</h2>
          <p>选择合适的版块，清楚地描述你想讨论的内容。</p>
        </div>
        <button class="icon-button" type="button" @click="handleClose" aria-label="关闭发布窗口">×</button>
      </div>
      <div class="composer-shell">
        <div class="composer-avatar">{{ userInitial }}</div>
        <div class="composer-fields">
          <div class="composer-meta-row">
            <label>
              <span>发布到</span>
              <select v-model.number="postForm.forumID" required>
                <option disabled value="">选择版块</option>
                <option v-for="forum in forums" :key="forum.forumID" :value="forum.forumID">
                  {{ forum.forumName }}
                </option>
              </select>
            </label>
          </div>
          <label class="composer-field">
            <span>标题</span>
            <input v-model="postForm.title" type="text" placeholder="用一句话概括你想讨论的内容" required />
          </label>
          <div class="composer-field">
            <div class="composer-field-head">
              <span>正文</span>
              <div class="composer-mode-toggle">
                <button
                  type="button"
                  :class="{ active: !showPreview }"
                  @click="showPreview = false"
                >编辑</button>
                <button
                  type="button"
                  :class="{ active: showPreview }"
                  @click="showPreview = true"
                >预览</button>
              </div>
            </div>
            <textarea
              v-if="!showPreview"
              v-model="postForm.content"
              placeholder="补充背景、细节或你的看法…"
              required
              autofocus
            ></textarea>
            <div v-else class="composer-preview">
              <div
                v-if="postForm.content.trim()"
                class="post-content markdown-body"
                v-html="previewHtml"
              ></div>
              <p v-else class="composer-preview-empty">暂无内容，切换到「编辑」填写正文。</p>
            </div>
            <small class="field-hint">支持 Markdown：标题、列表、引用、链接、代码块等。</small>
          </div>
          <div v-if="createImagePreviewUrls.length" class="image-strip">
            <div v-for="(url, index) in createImagePreviewUrls" :key="url" class="image-preview-item">
              <img :src="url" :alt="`预览图 ${index + 1}`" loading="lazy" />
              <button type="button" class="image-remove" @click="removeCreateImage(index)">移除</button>
            </div>
          </div>
          <div class="composer-footer">
            <div class="composer-tools">
              <label class="upload-button">
                <input
                  type="file"
                  multiple
                  accept="image/jpeg,image/png,image/gif,image/webp"
                  :disabled="submitting"
                  @change="handlePickCreateImages"
                />
                <span>添加图片</span>
              </label>
            </div>
            <button class="compose-submit" type="submit" :disabled="submitting">
              {{ submitting ? '发布中…' : '发布帖子' }}
            </button>
          </div>
        </div>
      </div>
    </form>
  </div>
</template>

<script setup>
import { computed, ref, watch } from 'vue'
import { useAuthStore } from '../../stores/auth'
import { createPost, uploadImages } from '../../api'
import { renderMarkdown } from '../../utils/markdown'

const props = defineProps({
  open: { type: Boolean, required: true },
  forums: { type: Array, default: () => [] },
})

const emit = defineEmits(['close', 'created', 'error'])

const authStore = useAuthStore()
const userInitial = computed(() => (authStore.user?.username || '用')[0]?.toUpperCase() || '用')

const submitting = ref(false)
const postForm = ref({
  forumID: '',
  title: '',
  content: '',
})
const createImageFiles = ref([])
const createImagePreviewUrls = ref([])
const showPreview = ref(false)
const previewHtml = computed(() => renderMarkdown(postForm.value.content))

const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/gif', 'image/webp']
const MAX_IMAGE_BYTES = 5 * 1024 * 1024
const MAX_IMAGE_COUNT = 6

watch(() => props.open, (isOpen) => {
  if (isOpen) {
    if (!postForm.value.forumID && props.forums.length > 0) {
      postForm.value.forumID = props.forums[0].forumID
    }
  }
})

const validateImageFile = (file) => {
  if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
    return '仅支持 JPG、PNG、GIF、WebP 图片'
  }
  if (file.size > MAX_IMAGE_BYTES) {
    return '单张图片不能超过5MB'
  }
  return null
}

const clearCreateImageState = () => {
  createImagePreviewUrls.value.forEach(url => URL.revokeObjectURL(url))
  createImageFiles.value = []
  createImagePreviewUrls.value = []
}

const resetForm = () => {
  postForm.value = {
    forumID: props.forums.length > 0 ? props.forums[0].forumID : '',
    title: '',
    content: '',
  }
  showPreview.value = false
  clearCreateImageState()
}

const handleClose = () => {
  if (submitting.value) return
  emit('close')
}

watch(() => props.open, (isOpen) => {
  if (!isOpen) {
    resetForm()
  }
})

const handlePickCreateImages = async (event) => {
  const files = Array.from(event.target.files || [])
  event.target.value = ''
  if (files.length === 0) return

  const nextCount = createImageFiles.value.length + files.length
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

  createImageFiles.value = [...createImageFiles.value, ...files]
  const addedUrls = files.map(file => URL.createObjectURL(file))
  createImagePreviewUrls.value = [...createImagePreviewUrls.value, ...addedUrls]
}

const removeCreateImage = (index) => {
  const removedUrl = createImagePreviewUrls.value[index]
  if (removedUrl) {
    URL.revokeObjectURL(removedUrl)
  }
  createImagePreviewUrls.value.splice(index, 1)
  createImageFiles.value.splice(index, 1)
}

const handleCreatePost = async () => {
  // 预览模式下 textarea 未渲染，原生 required 校验不生效：先切回编辑再提交
  if (showPreview.value) {
    showPreview.value = false
    return
  }
  try {
    submitting.value = true
    const uploaded = createImageFiles.value.length > 0
      ? await uploadImages(createImageFiles.value, 'posts')
      : null
    const imageUrls = (uploaded?.data?.urls || []).slice(0, MAX_IMAGE_COUNT)

    const res = await createPost({
      ...postForm.value,
      imageUrls,
    })
    const result = res.data
    resetForm()
    emit('close')
    emit('created', result)
  } catch (e) {
    emit('error', '发布失败: ' + (e.response?.data?.message || e.message))
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped>
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

.composer-field-head {
  align-items: center;
  display: flex;
  justify-content: space-between;
}

.composer-mode-toggle {
  display: inline-flex;
  border: 1px solid #dfe2e8;
  border-radius: 9999px;
  overflow: hidden;
}

.composer-mode-toggle button {
  background: #fff;
  border: none;
  color: #566074;
  cursor: pointer;
  font: inherit;
  font-size: 0.72rem;
  font-weight: 600;
  padding: 0.25rem 0.7rem;
}

.composer-mode-toggle button + button {
  border-left: 1px solid #dfe2e8;
}

.composer-mode-toggle button.active {
  background: #0f1419;
  color: #fff;
}

.composer-preview {
  background: #fff;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font-size: 1rem;
  line-height: 1.7;
  min-height: 160px;
  padding: 0.65rem 0.7rem;
  overflow-wrap: break-word;
}

.composer-preview-empty {
  color: var(--text-secondary);
  font-size: 0.8rem;
  margin: 0;
}

.composer-preview :deep(h1),
.composer-preview :deep(h2),
.composer-preview :deep(h3),
.composer-preview :deep(h4) {
  margin: 0.6em 0 0.35em;
  line-height: 1.35;
}

.composer-preview :deep(p) {
  margin: 0.4em 0;
}

.composer-preview :deep(ul),
.composer-preview :deep(ol) {
  margin: 0.4em 0;
  padding-left: 1.4em;
}

.composer-preview :deep(blockquote) {
  border-left: 3px solid var(--border);
  color: var(--text-secondary);
  margin: 0.5em 0;
  padding: 0.1em 0 0.1em 0.75em;
}

.composer-preview :deep(code) {
  background: #f1f3f5;
  border-radius: 4px;
  font-size: 0.9em;
  padding: 0.1em 0.35em;
}

.composer-preview :deep(pre) {
  background: #f1f3f5;
  border-radius: 8px;
  overflow-x: auto;
  padding: 0.6rem 0.75rem;
}

.composer-preview :deep(pre code) {
  background: none;
  padding: 0;
}

.composer-preview :deep(a) {
  color: var(--primary);
}

.field-hint {
  color: var(--text-secondary);
  font-size: 0.75rem;
  margin: 0;
}

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

.composer-header {
  position: sticky;
  top: 0;
  z-index: 2;
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  padding: 1.35rem 1.5rem 1.2rem;
  border-bottom: 1px solid var(--border);
  background: #fff;
}

.composer-kicker {
  display: block;
  margin-bottom: 0.25rem;
  color: var(--primary);
  font-size: 0.66rem;
  font-weight: 750;
  letter-spacing: 0.08em;
}

.composer-header h2 {
  color: #171d2e;
  font-size: 1.25rem;
  letter-spacing: -0.02em;
}

.composer-header p {
  margin-top: 0.25rem;
  color: var(--text-secondary);
  font-size: 0.78rem;
}

.composer-header .icon-button {
  width: 32px;
  height: 32px;
  flex: 0 0 auto;
  color: #697184;
  font-size: 1.2rem;
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

.composer-fields textarea {
  border: none;
  border-bottom: 1px solid var(--border);
  border-radius: 0;
  font-size: 1.125rem;
  min-height: 160px;
  padding: 0.5rem 0;
  resize: vertical;
}

.composer-fields input,
.composer-fields select,
.composer-fields textarea {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
  padding: 0.55rem 0.625rem;
  width: 100%;
}

.composer-fields textarea:focus,
.composer-fields input:focus,
.composer-fields select:focus {
  border-color: #1d9bf0;
  outline: none;
  box-shadow: 0 0 0 3px rgba(29, 155, 240, 0.12);
}

.composer-fields textarea:focus {
  box-shadow: none;
}

.composer-meta-row {
  display: grid;
  grid-template-columns: 1fr;
  gap: 0.8rem;
}

.composer-meta-row label,
.composer-field {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  color: #4c5567;
  font-size: 0.76rem;
  font-weight: 650;
}

.composer-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding-top: 1rem;
  border-top: 1px solid var(--border);
}

.composer-tools {
  display: flex;
  justify-content: flex-start;
  gap: 0.55rem;
}

.upload-button {
  min-height: 36px;
  display: inline-flex;
  align-items: center;
  padding: 0.5rem 0.7rem;
  border: 1px solid #dfe2e8;
  border-radius: 8px;
  color: #566074;
  background: #fff;
  font: inherit;
  font-size: 0.76rem;
  font-weight: 600;
  cursor: pointer;
}

.upload-button input {
  display: none;
}

.upload-button:hover {
  color: var(--primary);
  border-color: #c8c4ee;
  background: #f7f6ff;
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

.compose-submit:hover {
  background: #272c30;
}

.compose-submit:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

@media (max-width: 900px) {
  .composer-meta-row {
    grid-template-columns: 1fr;
  }

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

  .composer-header {
    padding: 1rem;
  }

  .composer-header p {
    display: none;
  }

  .composer-footer {
    align-items: stretch;
    flex-direction: column;
  }

  .composer-tools {
    display: flex;
  }

  .upload-button,
  .compose-submit {
    justify-content: center;
    width: 100%;
  }
}
</style>
