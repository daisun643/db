<template>
  <TabBar :tabs="tabs" :model-value="modelValue" @update:model-value="$emit('update:modelValue', $event)" />
</template>

<script setup>
import { computed } from 'vue'
import TabBar from '../TabBar.vue'

const props = defineProps({
  modelValue: { type: String, default: 'posts' },
  isSelf: { type: Boolean, default: false },
})

defineEmits(['update:modelValue'])

const owner = computed(() => (props.isSelf ? '我的' : 'TA 的'))
const tabs = computed(() => [
  { key: 'posts', label: `${owner.value}帖子` },
  { key: 'products', label: `${owner.value}商品` },
])
</script>
