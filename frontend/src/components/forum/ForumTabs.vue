<template>
  <TabBar :tabs="tabs" :model-value="modelValue" @update:model-value="$emit('update:modelValue', $event)" />
</template>

<script setup>
import TabBar from '../../tab/TabBar.vue'

defineProps({
  modelValue: { type: String, default: 'all' },
})

defineEmits(['update:modelValue'])

const tabs = [
  { key: 'all', label: '所有版块' },
  { key: 'my-posts', label: '我的帖子' },
  { key: 'my-forums', label: '我的版块' },
  { key: 'favorites', label: '收藏夹' },
]
</script>
