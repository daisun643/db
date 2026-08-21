<template>
  <div v-if="open" class="detail-backdrop" @click.self="$emit('close')">
    <div class="post-detail-panel folder-picker-panel" role="dialog" aria-label="选择收藏夹">
      <div class="modal-header">
        <button class="icon-button" @click="$emit('close')" aria-label="关闭收藏夹选择">
          <span>×</span>
        </button>
        <span class="muted">选择收藏夹</span>
      </div>
      <ul class="folder-pick-list">
        <li
          v-for="folder in folders"
          :key="folder.folderID"
          class="folder-pick-item"
          role="button"
          tabindex="0"
          @click="$emit('select', folder.folderID)"
          @keydown.enter="$emit('select', folder.folderID)"
        >
          <span>{{ folder.folderName }}</span>
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
        <button class="btn" type="submit">创建并收藏</button>
      </form>
    </div>
  </div>
</template>

<script setup>
defineProps({
  open: { type: Boolean, required: true },
  folders: { type: Array, default: () => [] },
  pickerFolderName: { type: String, default: '' },
})

defineEmits(['close', 'select', 'create', 'update:pickerFolderName'])
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
  justify-content: space-between;
  padding: 0.875rem 1rem;
  transition: background 0.15s;
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
  border-color: #1d9bf0;
  outline: none;
  box-shadow: 0 0 0 3px rgba(29, 155, 240, 0.12);
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
