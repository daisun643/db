<template>
  <div class="tabs">
    <button
      v-for="tab in tabs"
      :key="tab.key"
      type="button"
      :class="['tab', { active: modelValue === tab.key }]"
      @click="$emit('update:modelValue', tab.key)"
    >
      {{ tab.label }}
    </button>
  </div>
</template>

<script setup>
defineProps({
  tabs: { type: Array, required: true }, // [{ key, label }]
  modelValue: { type: String, default: '' },
})

defineEmits(['update:modelValue'])
</script>

<style scoped>
.tabs {
  display: flex;
  flex-wrap: nowrap;
  gap: .35rem;
}

.tab {
  border: 0;
  border-radius: 12px;
  padding: .72rem 1.05rem;
  color: #70778a;
  background: transparent;
  font-weight: 700;
  cursor: pointer;
  white-space: nowrap;
}

.tab.active {
  color: #fff;
  background: linear-gradient(135deg, #5f50dc, #7967f3);
  box-shadow: 0 8px 20px rgba(95, 80, 220, .22);
}
</style>
