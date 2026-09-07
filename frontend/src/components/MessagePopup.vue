<template>
  <div v-if="message" class="message-popup" :class="type" role="alert">
    <span class="message-popup-text">{{ message }}</span>
    <span v-if="duration > 0" class="message-popup-countdown">{{ remaining }}s</span>
    <button class="message-popup-close" type="button" aria-label="关闭提示" @click="$emit('close')">×</button>
  </div>
</template>

<script setup>
import { onUnmounted, ref, watch } from 'vue'

const props = defineProps({
  message: { type: String, default: '' },
  type: { type: String, default: 'success' },
  duration: { type: Number, default: 4000 },
})

const emit = defineEmits(['close'])

const remaining = ref(0)
let timer = null

const stopTimer = () => {
  clearInterval(timer)
  timer = null
}

onUnmounted(stopTimer)

watch(
  () => props.message,
  (value) => {
    stopTimer()
    if (value && props.duration > 0) {
      remaining.value = Math.ceil(props.duration / 1000)
      timer = setInterval(() => {
        remaining.value -= 1
        if (remaining.value <= 0) {
          stopTimer()
          emit('close')
        }
      }, 1000)
    }
  },
  { immediate: true },
)
</script>

<style scoped>
.message-popup {
  align-items: flex-start;
  animation: message-popup-in 0.2s ease-out;
  border-radius: 12px;
  box-shadow: 0 16px 40px rgba(15, 23, 42, 0.24);
  display: flex;
  gap: 0.625rem;
  left: 50%;
  max-width: min(480px, calc(100vw - 2rem));
  padding: 0.875rem 1rem;
  position: fixed;
  top: 1.25rem;
  transform: translateX(-50%);
  z-index: 2000;
}

@keyframes message-popup-in {
  from {
    opacity: 0;
    transform: translate(-50%, -8px);
  }
  to {
    opacity: 1;
    transform: translate(-50%, 0);
  }
}

.message-popup.success {
  background: #dcfce7;
  border: 1px solid #86efac;
  color: #166534;
}

.message-popup.error {
  background: #fee2e2;
  border: 1px solid #fca5a5;
  color: #991b1b;
}

.message-popup-text {
  line-height: 1.5;
  word-break: break-word;
}

.message-popup-countdown {
  align-items: center;
  background: rgba(15, 23, 42, 0.08);
  border-radius: 9999px;
  display: inline-flex;
  flex: none;
  font-size: 0.75rem;
  height: 1.375rem;
  justify-content: center;
  margin-top: 1px;
  min-width: 2rem;
  padding: 0 0.375rem;
}

.message-popup-close {
  background: transparent;
  border: none;
  color: inherit;
  cursor: pointer;
  flex: none;
  font: inherit;
  font-size: 1.25rem;
  line-height: 1;
  margin: -0.125rem -0.25rem 0 0;
  opacity: 0.7;
  padding: 0.125rem 0.25rem;
}

.message-popup-close:hover {
  opacity: 1;
}
</style>
