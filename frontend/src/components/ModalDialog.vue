<template>
  <Teleport to="body">
    <div
      v-if="visible"
      class="modal-backdrop"
      :class="[`modal-backdrop--${variant}`]"
      @click.self="$emit('close')"
    >
      <component
        :is="tag"
        class="modal-dialog"
        :class="[`modal-dialog--${variant}`, dialogClass]"
        role="dialog"
        aria-modal="true"
        :aria-label="title"
      >
        <header class="modal-dialog-header">
          <div>
            <span v-if="kicker" class="modal-kicker">{{ kicker }}</span>
            <component :is="titleTag">{{ title }}</component>
            <p v-if="subtitle" class="modal-subtitle">{{ subtitle }}</p>
          </div>
          <button
            type="button"
            class="modal-close-btn"
            aria-label="关闭"
            @click="$emit('close')"
          >×</button>
        </header>
        <div class="modal-dialog-body">
          <slot />
        </div>
        <div v-if="$slots.actions" class="modal-dialog-actions">
          <slot name="actions" />
        </div>
      </component>
    </div>
  </Teleport>
</template>

<script setup>
import { computed } from 'vue'

const props = defineProps({
  visible: { type: Boolean, default: false },
  variant: { type: String, default: 'dialog' }, // 'dialog' | 'panel'
  kicker: { type: String, default: '' },
  title: { type: String, default: '' },
  subtitle: { type: String, default: '' },
  tag: { type: String, default: 'section' },
  dialogClass: { type: [String, Array, Object], default: '' },
})

defineEmits(['close'])

const titleTag = computed(() => props.variant === 'panel' ? 'h2' : 'h3')
</script>

<style scoped>
/* Backdrop */
.modal-backdrop {
  position: fixed;
  inset: 0;
  display: flex;
  z-index: 1200;
  background: rgba(17, 22, 39, 0.58);
  backdrop-filter: blur(6px);
}

.modal-backdrop--dialog {
  align-items: center;
  justify-content: center;
  padding: 1rem;
}

.modal-backdrop--panel {
  align-items: flex-start;
  justify-content: center;
  padding: 2rem 1rem;
}

/* Dialog container */
.modal-dialog {
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: #f6f7f9;
}

.modal-dialog--dialog {
  width: 100%;
  max-width: 420px;
  max-height: min(720px, 90vh);
  border-radius: 20px;
  background: var(--surface, #fff);
  box-shadow: 0 28px 70px rgba(13, 10, 40, 0.28);
}

.modal-dialog--panel {
  width: min(1120px, 100%);
  max-height: calc(100vh - 4rem);
  border-radius: 16px;
  box-shadow: 0 24px 70px rgba(11, 15, 29, 0.28);
}

/* Header */
.modal-dialog-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex: 0 0 auto;
  padding: 1rem 1.2rem;
  border-bottom: 1px solid var(--border, #e5e7eb);
  background: #fff;
}

.modal-dialog--dialog .modal-dialog-header {
  padding: 1.25rem 1.25rem 0.75rem;
  border-bottom: none;
  background: transparent;
}

.modal-kicker {
  display: block;
  margin-bottom: 0.1rem;
  color: var(--primary, #6957f5);
  font-size: 0.6rem;
  font-weight: 750;
  letter-spacing: 0.09em;
}

.modal-dialog-header h2 {
  color: #171d2e;
  font-size: 1.05rem;
  margin: 0;
}

.modal-dialog-header h3 {
  color: #171d2e;
  font-size: 1rem;
  margin: 0;
}

.modal-subtitle {
  margin: 0.25rem 0 0;
  color: var(--text-secondary, #6b7280);
  font-size: 0.875rem;
}

.modal-close-btn {
  width: 32px;
  height: 32px;
  display: grid;
  place-items: center;
  border: 0;
  border-radius: 8px;
  color: #687184;
  background: #f0f2f5;
  font-size: 1.2rem;
  cursor: pointer;
  flex-shrink: 0;
}

.modal-close-btn:hover {
  color: #252c3d;
  background: #e5e8ed;
}

/* Body */
.modal-dialog-body {
  min-height: 0;
  overflow: auto;
  padding: 1.2rem;
}

.modal-dialog--dialog .modal-dialog-body {
  padding: 0.75rem 1.25rem 1.25rem;
}

/* Actions */
.modal-dialog-actions {
  display: flex;
  gap: 0.75rem;
  justify-content: flex-end;
  padding: 0 1.25rem 1.25rem;
}

/* Responsive */
@media (max-width: 900px) {
  .modal-backdrop--panel {
    padding: 1rem;
  }
  .modal-dialog--panel {
    max-height: calc(100vh - 2rem);
  }
}

@media (max-width: 600px) {
  .modal-backdrop--panel {
    padding: 0;
  }
  .modal-dialog--panel {
    width: 100%;
    max-height: 100vh;
    height: 100vh;
    border-radius: 0;
  }
}
</style>
