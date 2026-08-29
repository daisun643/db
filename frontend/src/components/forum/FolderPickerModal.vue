<template>
  <div v-if="open" class="detail-backdrop" @click.self="$emit('close')">
    <div class="post-detail-panel folder-picker-panel" role="dialog" aria-label="选择收藏夹">
      <div class="modal-header">
        <button class="icon-button" @click="$emit('close')" aria-label="关闭收藏夹选择">
          <span>×</span>
        </button>
        <span class="muted">选择收藏夹（可多选）</span>
      </div>
      <ul class="folder-pick-list">
        <li
          v-for="folder in folders"
          :key="folder.folderID"
          class="folder-pick-item"
          :class="{ checked: selectedIds.includes(folder.folderID) }"
          role="button"
          tabindex="0"
          @click="toggle(folder.folderID)"
          @keydown.enter="toggle(folder.folderID)"
        >
          <input
            type="checkbox"
            class="folder-pick-checkbox"
            :checked="selectedIds.includes(folder.folderID)"
            tabindex="-1"
            @click.stop
            @change="toggle(folder.folderID)"
          />
          <span class="folder-pick-name">{{ folder.folderName }}</span>
          <span class="folder-post-count">{{ folder.postCount || 0 }}</span>
        </li>
        <li v-if="folders.length === 0" class="folder-pick-empty">
          暂无收藏夹，请先创建一个
        </li>
      </ul>
      <form @submit.prevent="$emit('create')" class="folder-picker-form">
        <input
          :value="pickerFolderName"
          @input="$emit('update:pickerFolderName', $event.target.value)"
          type="text"
          placeholder="新收藏夹名称"
          required
        />
        <button class="btn btn-primary" type="submit">创建并收藏</button>
      </form>
      <div class="folder-picker-footer">
        <button
          class="btn btn-primary"
          type="button"
          :disabled="selectedIds.length === 0"
          @click="$emit('save', selectedIds)"
        >保存（已选 {{ selectedIds.length }} 个）</button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue'

const props = defineProps({
  open: { type: Boolean, required: true },
  folders: { type: Array, default: () => [] },
  pickerFolderName: { type: String, default: '' },
})

defineEmits(['close', 'save', 'create', 'update:pickerFolderName'])

const selectedIds = ref([])

const toggle = (folderId) => {
  selectedIds.value = selectedIds.value.includes(folderId)
    ? selectedIds.value.filter(id => id !== folderId)
    : [...selectedIds.value, folderId]
}

// 每次打开时重置勾选状态，避免残留上一次的选择
watch(() => props.open, (opened) => {
  if (opened) selectedIds.value = []
})
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
  border-radius: 14px;
  box-shadow: 0 20px 60px rgba(15, 23, 42, 0.24);
  max-height: calc(100vh - 6rem);
  max-width: 680px;
  overflow-y: auto;
  padding: 0;
  width: min(680px, 100vw);
}

.folder-picker-panel {
  max-width: 400px;
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
  border-radius: 14px 14px 0 0;
  padding: .8rem 1.1rem;
  background: #f8fbff;
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
  font-size: 1.25rem;
  height: 36px;
  justify-content: center;
  line-height: 1;
  width: 36px;
}

.icon-button:hover {
  background: #eff3f4;
}

.muted {
  color: var(--text-secondary);
}

.folder-pick-list {
  list-style: none;
  margin: 0;
  padding: 0;
  max-height: 300px;
  overflow-y: auto;
}

.folder-pick-item {
  align-items: center;
  border-bottom: 1px solid var(--border);
  cursor: pointer;
  display: flex;
  gap: 0.625rem;
  padding: 0.875rem 1rem;
  transition: background 0.15s;
}

.folder-pick-item.checked {
  background: rgba(29, 155, 240, 0.06);
}

.folder-pick-checkbox {
  cursor: pointer;
  flex: none;
  height: 16px;
  width: 16px;
}

.folder-pick-name {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.folder-pick-item:last-child {
  border-bottom: none;
}

.folder-pick-item:hover,
.folder-pick-item:focus-visible {
  background: rgba(29, 155, 240, 0.06);
  outline: none;
}

.folder-post-count {
  color: #536471;
  font-size: 0.8125rem;
}

.folder-pick-empty {
  color: #536471;
  padding: 1.5rem 1rem;
  text-align: center;
}

.folder-picker-form {
  border-top: 1px solid var(--border);
  display: flex;
  gap: 0.5rem;
  padding: 0.75rem 1rem;
}

.folder-picker-form input {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  flex: 1;
  font: inherit;
  min-width: 0;
  padding: 0.5rem 0.75rem;
}

.folder-picker-form input:focus {
  border-color: var(--primary);
  outline: none;
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
}

.folder-picker-footer {
  border-top: 1px solid var(--border);
  display: flex;
  justify-content: flex-end;
  padding: 0.75rem 1rem;
}

.folder-picker-footer .btn:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0.5rem 1rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--surface);
  color: var(--text);
  font: inherit;
  cursor: pointer;
}

.btn-primary {
  background: var(--primary);
  border-color: var(--primary);
  color: #fff;
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
}
</style>
