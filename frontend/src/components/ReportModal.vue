<template>
  <div v-if="open" class="detail-backdrop" @click.self="$emit('close')">
    <form class="post-detail-panel report-form" @submit.prevent="$emit('submit')">
      <div class="detail-header">
        <button class="link-button" type="button" @click="$emit('close')">取消举报</button>
        <span class="muted">{{ target?.typeLabel }} #{{ target?.id }}</span>
      </div>
      <h2>举报{{ target?.typeLabel }}：{{ target?.title }}</h2>
      <textarea
        :value="reason"
        @input="$emit('update:reason', $event.target.value)"
        placeholder="描述违规原因"
        required
      ></textarea>
      <button class="btn btn-primary" type="submit">提交举报</button>
    </form>
  </div>
</template>

<script setup>
defineProps({
  open: { type: Boolean, required: true },
  target: { type: Object, default: null },
  reason: { type: String, default: '' },
})

defineEmits(['close', 'submit', 'update:reason'])
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

.report-form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 1rem;
}

.detail-header {
  align-items: center;
  border-bottom: 1px solid var(--border);
  display: flex;
  gap: 1rem;
  justify-content: space-between;
  padding: 1rem;
}

.link-button {
  border: none;
  background: transparent;
  color: #536471;
  cursor: pointer;
  font: inherit;
  padding: 0.125rem 0;
}

.link-button:hover {
  color: #1d9bf0;
}

.muted {
  color: var(--text-secondary);
}

.report-form h2 {
  font-size: 1.25rem;
  margin: 0;
}

.report-form textarea {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
  min-height: 96px;
  padding: 0.75rem 1rem;
  resize: vertical;
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
